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
    /// Editor-only video source. Creates a hidden clone camera that mirrors the URP base
    /// camera and renders to our RenderTexture every frame. URP renders this clone as part
    /// of its normal pipeline, so we get full scene content without Editor/Simulator chrome.
    /// </summary>
    internal sealed class PilotEditorCameraVideoSource : ScreenVideoSource
    {
        private static readonly PropertyInfo s_renderTypeProp;
        private static readonly Type s_urpCameraDataType;

        private readonly int m_maxDimension;
        private TextureFormat m_textureFormat;
        private RenderTexture m_captureRT;
        private Camera m_cloneCamera;
        private GameObject m_cloneGO;
        private Camera m_baseCamera;
        private long m_readbackOk;
        private long m_readbackErr;
        private long m_lastLogged;

        static PilotEditorCameraVideoSource()
        {
            s_urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (s_urpCameraDataType != null)
                s_renderTypeProp = s_urpCameraDataType.GetProperty("renderType");
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
            PilotLog.Info("PilotEditorCameraVideoSource.Start playing=" + _playing);
        }

        public override void Stop()
        {
            PilotLog.Info("PilotEditorCameraVideoSource.Stop readbackOk=" + m_readbackOk + " readbackErr=" + m_readbackErr);
            base.Stop();
            DestroyClone();
            if (m_captureRT != null) { m_captureRT.Release(); m_captureRT = null; }
        }

        protected override bool ReadBuffer()
        {
            if (m_readbackOk - m_lastLogged >= 60)
            {
                m_lastLogged = m_readbackOk;
                PilotLog.Info("Pilot heartbeat: readbackOk=" + m_readbackOk + " readbackErr=" + m_readbackErr
                    + " reading=" + _reading + " pending=" + _requestPending
                    + " clone=" + (m_cloneCamera != null));
            }

            if (_reading) return false;
            _reading = true;
            var textureChanged = false;

            try
            {
                int w = GetWidth();
                int h = GetHeight();
                EnsureCaptureRT(w, h);
                EnsureCloneCamera();

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
            if (req.hasError) { m_readbackErr++; _reading = false; }
            else { m_readbackOk++; _requestPending = true; }
        }

        protected override bool SendFrame()
        {
            try { return base.SendFrame(); }
            catch (Exception e)
            {
                PilotLog.Error("SendFrame failed: " + e.Message);
                _reading = false; _requestPending = false;
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
            m_captureRT = new RenderTexture(w, h, 24, compatibleFormat);
            m_captureRT.name = "PilotEditorCaptureRT";
            m_captureRT.Create();
            PilotLog.Info("Created captureRT " + w + "x" + h + " format=" + m_textureFormat);

            // Update existing clone targetTexture if camera was already created.
            if (m_cloneCamera != null) m_cloneCamera.targetTexture = m_captureRT;
        }

        private void EnsureCloneCamera()
        {
            var baseCam = ResolveBaseCamera();
            if (baseCam == null) return;

            // Recreate clone if base camera changed or clone was destroyed.
            if (m_cloneCamera == null || m_baseCamera != baseCam)
            {
                DestroyClone();
                m_baseCamera = baseCam;

                m_cloneGO = new GameObject("[PilotSDK_CaptureCamera]");
                m_cloneGO.hideFlags = HideFlags.HideAndDontSave;
                m_cloneGO.transform.SetParent(baseCam.transform, false);

                m_cloneCamera = m_cloneGO.AddComponent<Camera>();
                m_cloneCamera.CopyFrom(baseCam);
                m_cloneCamera.targetTexture = m_captureRT;
                m_cloneCamera.depth = baseCam.depth - 1; // render before main, doesn't matter
                m_cloneCamera.enabled = true;

                // Force URP to treat this as a Base camera (renderType=0) with no overlays.
                if (s_urpCameraDataType != null)
                {
                    var data = m_cloneGO.GetComponent(s_urpCameraDataType)
                        ?? m_cloneGO.AddComponent(s_urpCameraDataType);
                    if (data != null && s_renderTypeProp != null && s_renderTypeProp.CanWrite)
                    {
                        s_renderTypeProp.SetValue(data, 0);
                    }
                }

                PilotLog.Info("Created clone capture camera under '" + baseCam.name + "'");
            }
            else
            {
                // Keep clone synced (CopyFrom each frame is cheap and handles fov/clip changes)
                m_cloneCamera.CopyFrom(baseCam);
                m_cloneCamera.targetTexture = m_captureRT;
                m_cloneCamera.enabled = true;
            }
        }

        private void DestroyClone()
        {
            if (m_cloneCamera != null) m_cloneCamera.targetTexture = null;
            if (m_cloneGO != null)
            {
                UnityEngine.Object.DestroyImmediate(m_cloneGO);
                m_cloneGO = null;
                m_cloneCamera = null;
            }
            m_baseCamera = null;
        }

        private Camera ResolveBaseCamera()
        {
            Camera fallback = null;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == m_cloneCamera) continue;
                if (cam.cameraType != CameraType.Game || !cam.isActiveAndEnabled) continue;
                fallback = cam;
                if (s_renderTypeProp != null)
                {
                    var data = cam.GetComponent("UniversalAdditionalCameraData");
                    if (data != null)
                    {
                        int rt = (int)s_renderTypeProp.GetValue(data);
                        if (rt == 0) return cam;
                    }
                }
            }
            return fallback ?? Camera.main;
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