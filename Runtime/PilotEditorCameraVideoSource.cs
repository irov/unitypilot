#if PILOT_LIVEKIT && UNITY_EDITOR
using System;
using System.Reflection;
using LiveKit;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Pilot.SDK
{
    internal sealed class PilotEditorCameraVideoSource : ScreenVideoSource
    {
        private static readonly PropertyInfo s_renderTypeProp;
        private static readonly MethodInfo s_renderSingleCameraMethod;

        private readonly int m_maxDimension;
        private TextureFormat m_textureFormat;
        private RenderTexture m_cameraRT;
        private RenderTexture m_readRT;
        private Camera m_baseCamera;
        private long m_frameCounter;
        private long m_readbackOkCounter;
        private long m_readbackErrCounter;
        private long m_lastLoggedFrame;

        static PilotEditorCameraVideoSource()
        {
            var urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraDataType != null)
                s_renderTypeProp = urpCameraDataType.GetProperty("renderType");

            var urpType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipeline, Unity.RenderPipelines.Universal.Runtime");
            if (urpType != null)
                s_renderSingleCameraMethod = urpType.GetMethod("RenderSingleCamera",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(ScriptableRenderContext), typeof(Camera) }, null);
        }

        internal PilotEditorCameraVideoSource(int maxDimension) : base()
        {
            m_maxDimension = maxDimension;
            PilotLog.Info("PilotEditorCameraVideoSource ctor: maxDim=" + maxDimension
                + " urp=" + (s_renderTypeProp != null)
                + " renderSingleCamera=" + (s_renderSingleCameraMethod != null));
        }

        public override int GetWidth() { int w, h; ComputeDimensions(m_maxDimension, out w, out h); return w; }
        public override int GetHeight() { int w, h; ComputeDimensions(m_maxDimension, out w, out h); return h; }

        public override void Start()
        {
            base.Start();
            PilotLog.Info("PilotEditorCameraVideoSource.Start playing=" + _playing);
        }

        public override void Stop()
        {
            PilotLog.Info("PilotEditorCameraVideoSource.Stop frames=" + m_frameCounter
                + " readbackOk=" + m_readbackOkCounter + " readbackErr=" + m_readbackErrCounter);
            base.Stop();
            if (m_cameraRT != null) { m_cameraRT.Release(); m_cameraRT = null; }
            if (m_readRT != null) { m_readRT.Release(); m_readRT = null; }
        }

        protected override bool ReadBuffer()
        {
            if (m_frameCounter - m_lastLoggedFrame >= 60)
            {
                m_lastLoggedFrame = m_frameCounter;
                PilotLog.Info("PilotEditorCameraVideoSource heartbeat: frame=" + m_frameCounter
                    + " reading=" + _reading + " pending=" + _requestPending
                    + " readbackOk=" + m_readbackOkCounter + " readbackErr=" + m_readbackErrCounter);
            }

            if (_reading) return false;
            _reading = true;
            var textureChanged = false;

            try
            {
                var cam = ResolveBaseCamera();
                if (cam == null)
                {
                    PilotLog.Warn("PilotEditorCameraVideoSource: no base camera found");
                    _reading = false;
                    return false;
                }

                int w = GetWidth();
                int h = GetHeight();

                if (m_readRT == null || m_readRT.width != w || m_readRT.height != h)
                {
                    if (m_readRT != null) m_readRT.Release();
                    var targetFormat = GetGraphicsFormat();
                    var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
                    m_textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                    _bufferType = GetVideoBufferType(m_textureFormat);
                    m_readRT = new RenderTexture(w, h, 0, compatibleFormat);
                    m_readRT.name = "PilotEditorReadRT";
                    m_readRT.Create();

                    if (_captureBuffer.IsCreated) _captureBuffer.Dispose();
                    _captureBuffer = new NativeArray<byte>(w * h * GetStrideForBuffer(_bufferType), Allocator.Persistent);

                    if (_previewTexture != null) UnityEngine.Object.Destroy(_previewTexture);
                    _previewTexture = new Texture2D(w, h, m_textureFormat, false);
                    textureChanged = true;

                    PilotLog.Info("PilotEditorCameraVideoSource: created RTs " + w + "x" + h + " format=" + m_textureFormat);
                }

                if (m_cameraRT == null || m_cameraRT.width != w || m_cameraRT.height != h)
                {
                    if (m_cameraRT != null) m_cameraRT.Release();
                    m_cameraRT = new RenderTexture(w, h, 24, m_readRT.graphicsFormat);
                    m_cameraRT.name = "PilotEditorCameraRT";
                    m_cameraRT.Create();
                }

                var prevTarget = cam.targetTexture;
                cam.targetTexture = m_cameraRT;

                if (s_renderSingleCameraMethod != null)
                {
#pragma warning disable CS0618
                    s_renderSingleCameraMethod.Invoke(null, new object[] { default(ScriptableRenderContext), cam });
#pragma warning restore CS0618
                }
                else
                {
                    cam.Render();
                }

                cam.targetTexture = prevTarget;

                Graphics.Blit(m_cameraRT, m_readRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
                Graphics.CopyTexture(m_readRT, _previewTexture);
                AsyncGPUReadback.RequestIntoNativeArray(ref _captureBuffer, m_readRT, 0, m_textureFormat, OnReadbackTracked);

                m_frameCounter++;
            }
            catch (Exception e)
            {
                PilotLog.Error("PilotEditorCameraVideoSource ReadBuffer failed: " + e.Message + "\n" + e.StackTrace);
                _reading = false;
            }

            return textureChanged;
        }

        private void OnReadbackTracked(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                m_readbackErrCounter++;
                PilotLog.Warn("PilotEditorCameraVideoSource AsyncGPUReadback error #" + m_readbackErrCounter);
                _reading = false;
            }
            else
            {
                m_readbackOkCounter++;
                _requestPending = true;
            }
        }

        protected override bool SendFrame()
        {
            try { return base.SendFrame(); }
            catch (Exception e)
            {
                PilotLog.Error("PilotEditorCameraVideoSource SendFrame failed: " + e.Message + "\n" + e.StackTrace);
                _reading = false;
                _requestPending = false;
                return false;
            }
        }

        private Camera ResolveBaseCamera()
        {
            if (m_baseCamera != null && m_baseCamera.isActiveAndEnabled && m_baseCamera.cameraType == CameraType.Game)
                return m_baseCamera;

            Camera fallback = null;
            foreach (var cam in Camera.allCameras)
            {
                if (cam.cameraType != CameraType.Game || !cam.isActiveAndEnabled) continue;
                fallback = cam;
                if (s_renderTypeProp != null)
                {
                    var data = cam.GetComponent("UniversalAdditionalCameraData");
                    if (data != null)
                    {
                        int renderType = (int)s_renderTypeProp.GetValue(data);
                        if (renderType == 0)
                        {
                            m_baseCamera = cam;
                            return m_baseCamera;
                        }
                    }
                }
            }
            m_baseCamera = fallback ?? Camera.main;
            return m_baseCamera;
        }

        private static void ComputeDimensions(int maxDimension, out int width, out int height)
        {
            int sourceWidth = Mathf.Max(2, Screen.width);
            int sourceHeight = Mathf.Max(2, Screen.height);
            if (maxDimension <= 0) maxDimension = Mathf.Max(sourceWidth, sourceHeight);
            float scale = Mathf.Min(1f, (float)maxDimension / Mathf.Max(sourceWidth, sourceHeight));
            width = Mathf.Max(2, Mathf.RoundToInt(sourceWidth * scale));
            height = Mathf.Max(2, Mathf.RoundToInt(sourceHeight * scale));
            width = Mathf.Max(2, (width / 2) * 2);
            height = Mathf.Max(2, (height / 2) * 2);
        }

        private static GraphicsFormat GetGraphicsFormat()
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                switch (SystemInfo.graphicsDeviceType)
                {
                    case GraphicsDeviceType.Direct3D11:
                    case GraphicsDeviceType.Direct3D12:
                    case GraphicsDeviceType.Vulkan: return GraphicsFormat.B8G8R8A8_SRGB;
                    case GraphicsDeviceType.OpenGLCore:
                    case GraphicsDeviceType.OpenGLES2:
                    case GraphicsDeviceType.OpenGLES3: return GraphicsFormat.R8G8B8A8_SRGB;
                    case GraphicsDeviceType.Metal: return GraphicsFormat.B8G8R8A8_SRGB;
                    default: return GraphicsFormat.B8G8R8A8_SRGB;
                }
            }
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Vulkan: return GraphicsFormat.B8G8R8A8_UNorm;
                default: return GraphicsFormat.R8G8B8A8_UNorm;
            }
        }
    }
}
#endif