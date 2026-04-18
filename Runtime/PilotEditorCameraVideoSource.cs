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
    /// <summary>
    /// Editor-only video source. Hooks RenderPipelineManager.endCameraRendering to capture
    /// the URP base camera's output AFTER URP has rendered it (so we get real scene content,
    /// not just clear color). Then streams via LiveKit ScreenVideoSource pipeline.
    /// </summary>
    internal sealed class PilotEditorCameraVideoSource : ScreenVideoSource
    {
        private static readonly PropertyInfo s_renderTypeProp;

        private readonly int m_maxDimension;
        private TextureFormat m_textureFormat;
        private RenderTexture m_captureRT;
        private long m_capturedFrames;
        private long m_readbackOk;
        private long m_readbackErr;
        private long m_lastLogged;
        private bool m_subscribed;

        static PilotEditorCameraVideoSource()
        {
            var urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraDataType != null)
                s_renderTypeProp = urpCameraDataType.GetProperty("renderType");
        }

        internal PilotEditorCameraVideoSource(int maxDimension) : base()
        {
            m_maxDimension = maxDimension;
            PilotLog.Info("PilotEditorCameraVideoSource ctor: maxDim=" + maxDimension
                + " urp=" + (s_renderTypeProp != null));
        }

        public override int GetWidth() { int w, h; ComputeDimensions(m_maxDimension, out w, out h); return w; }
        public override int GetHeight() { int w, h; ComputeDimensions(m_maxDimension, out w, out h); return h; }

        public override void Start()
        {
            base.Start();
            if (!m_subscribed)
            {
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                m_subscribed = true;
            }
            PilotLog.Info("PilotEditorCameraVideoSource.Start playing=" + _playing);
        }

        public override void Stop()
        {
            if (m_subscribed)
            {
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                m_subscribed = false;
            }
            PilotLog.Info("PilotEditorCameraVideoSource.Stop captured=" + m_capturedFrames
                + " readbackOk=" + m_readbackOk + " readbackErr=" + m_readbackErr);
            base.Stop();
            if (m_captureRT != null) { m_captureRT.Release(); m_captureRT = null; }
        }

        // Called by URP after each camera finishes rendering to its target.
        private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            if (!_playing || cam == null) return;
            if (cam.cameraType != CameraType.Game) return;

            // Only capture the URP BASE camera (renderType==0). Skip overlays.
            if (s_renderTypeProp != null)
            {
                var data = cam.GetComponent("UniversalAdditionalCameraData");
                if (data != null)
                {
                    int rt = (int)s_renderTypeProp.GetValue(data);
                    if (rt != 0) return;
                }
            }

            try
            {
                int w = GetWidth();
                int h = GetHeight();
                EnsureCaptureRT(w, h);
                // The camera has just rendered to screen. ScreenCapture is not safe here,
                // but we can blit from the active backbuffer via a temporary RT bound to the camera.
                // Instead, use Graphics.Blit from the camera's targetTexture if set, otherwise
                // copy the current RenderTexture.active.
                var src = cam.activeTexture;
                if (src != null)
                {
                    Graphics.Blit(src, m_captureRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
                    m_capturedFrames++;
                }
            }
            catch (Exception e)
            {
                PilotLog.Error("OnEndCameraRendering failed: " + e.Message);
            }
        }

        protected override bool ReadBuffer()
        {
            if (m_capturedFrames - m_lastLogged >= 60)
            {
                m_lastLogged = m_capturedFrames;
                PilotLog.Info("Pilot heartbeat: captured=" + m_capturedFrames
                    + " reading=" + _reading + " pending=" + _requestPending
                    + " readbackOk=" + m_readbackOk + " readbackErr=" + m_readbackErr);
            }

            if (_reading) return false;
            if (m_captureRT == null) return false;

            _reading = true;
            var textureChanged = false;

            try
            {
                int w = GetWidth();
                int h = GetHeight();

                if (_previewTexture == null || _previewTexture.width != w || _previewTexture.height != h)
                {
                    if (_captureBuffer.IsCreated) _captureBuffer.Dispose();
                    _captureBuffer = new NativeArray<byte>(w * h * GetStrideForBuffer(_bufferType), Allocator.Persistent);
                    if (_previewTexture != null) UnityEngine.Object.Destroy(_previewTexture);
                    _previewTexture = new Texture2D(w, h, m_textureFormat, false);
                    textureChanged = true;
                }

                Graphics.CopyTexture(m_captureRT, _previewTexture);
                AsyncGPUReadback.RequestIntoNativeArray(ref _captureBuffer, m_captureRT, 0, m_textureFormat, OnReadbackTracked);
            }
            catch (Exception e)
            {
                PilotLog.Error("ReadBuffer failed: " + e.Message);
                _reading = false;
            }

            return textureChanged;
        }

        private void OnReadbackTracked(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                m_readbackErr++;
                _reading = false;
            }
            else
            {
                m_readbackOk++;
                _requestPending = true;
            }
        }

        protected override bool SendFrame()
        {
            try { return base.SendFrame(); }
            catch (Exception e)
            {
                PilotLog.Error("SendFrame failed: " + e.Message);
                _reading = false;
                _requestPending = false;
                return false;
            }
        }

        private void EnsureCaptureRT(int w, int h)
        {
            if (m_captureRT != null && m_captureRT.width == w && m_captureRT.height == h) return;
            if (m_captureRT != null) m_captureRT.Release();
            var targetFormat = GetGraphicsFormat();
            var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
            m_textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
            _bufferType = GetVideoBufferType(m_textureFormat);
            m_captureRT = new RenderTexture(w, h, 0, compatibleFormat);
            m_captureRT.name = "PilotEditorCaptureRT";
            m_captureRT.Create();
            PilotLog.Info("Created captureRT " + w + "x" + h + " format=" + m_textureFormat);
        }

        private static void ComputeDimensions(int maxDimension, out int width, out int height)
        {
            int sw = Mathf.Max(2, Screen.width);
            int sh = Mathf.Max(2, Screen.height);
            if (maxDimension <= 0) maxDimension = Mathf.Max(sw, sh);
            float scale = Mathf.Min(1f, (float)maxDimension / Mathf.Max(sw, sh));
            width = Mathf.Max(2, Mathf.RoundToInt(sw * scale));
            height = Mathf.Max(2, Mathf.RoundToInt(sh * scale));
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