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
    /// Editor-only video source that captures a specific game camera via Camera.Render(),
    /// avoiding Device Simulator / Editor chrome.
    ///
    /// Inherits from ScreenVideoSource (like the device path) and mirrors its ReadBuffer
    /// pattern exactly: try/catch, async readback from RenderTexture, RT release after send.
    /// </summary>
    internal sealed class PilotEditorCameraVideoSource : ScreenVideoSource
    {
        private static readonly PropertyInfo s_renderTypeProp;

        private readonly int m_maxDimension;
        private TextureFormat m_textureFormat;
        private RenderTexture m_cameraRT;
        private RenderTexture m_readRT;
        private Camera m_baseCamera;

        static PilotEditorCameraVideoSource()
        {
            var urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

            if (urpCameraDataType != null)
            {
                s_renderTypeProp = urpCameraDataType.GetProperty("renderType");
            }
        }

        internal PilotEditorCameraVideoSource(int maxDimension) : base()
        {
            m_maxDimension = maxDimension;
        }

        public override int GetWidth()
        {
            int width, height;
            ComputeDimensions(m_maxDimension, out width, out height);
            return width;
        }

        public override int GetHeight()
        {
            int width, height;
            ComputeDimensions(m_maxDimension, out width, out height);
            return height;
        }

        public override void Stop()
        {
            base.Stop();

            if (m_cameraRT != null)
            {
                m_cameraRT.Release();
                m_cameraRT = null;
            }

            ClearReadRT();
        }

        protected override bool ReadBuffer()
        {
            if (_reading)
            {
                return false;
            }

            _reading = true;
            var textureChanged = false;

            try
            {
                var cam = ResolveBaseCamera();
                if (cam == null)
                {
                    _reading = false;
                    return false;
                }

                // Recreate readback RT if needed (matches ScreenVideoSource pattern)
                if (m_readRT == null || m_readRT.width != GetWidth() || m_readRT.height != GetHeight())
                {
                    ClearReadRT();

                    var targetFormat = GetGraphicsFormat();
                    var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
                    m_textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                    _bufferType = GetVideoBufferType(m_textureFormat);
                    m_readRT = new RenderTexture(GetWidth(), GetHeight(), 0, compatibleFormat);

                    if (_captureBuffer.IsCreated)
                    {
                        _captureBuffer.Dispose();
                    }

                    _captureBuffer = new NativeArray<byte>(
                        GetWidth() * GetHeight() * GetStrideForBuffer(_bufferType),
                        Allocator.Persistent);

                    if (_previewTexture != null)
                    {
                        UnityEngine.Object.Destroy(_previewTexture);
                    }

                    _previewTexture = new Texture2D(GetWidth(), GetHeight(), m_textureFormat, false);
                    textureChanged = true;
                }

                // Ensure camera RT exists
                if (m_cameraRT == null || m_cameraRT.width != GetWidth() || m_cameraRT.height != GetHeight())
                {
                    if (m_cameraRT != null)
                    {
                        m_cameraRT.Release();
                    }

                    m_cameraRT = new RenderTexture(GetWidth(), GetHeight(), 24, m_readRT.graphicsFormat);
                    m_cameraRT.name = "PilotEditorCameraRT";
                    m_cameraRT.Create();
                }

                // Camera.Render → cameraRT
                var prevTarget = cam.targetTexture;
                cam.targetTexture = m_cameraRT;
                cam.Render();
                cam.targetTexture = prevTarget;

                // Flip Y → readRT
                Graphics.Blit(m_cameraRT, m_readRT, new Vector2(1f, -1f), new Vector2(0f, 1f));

                // Copy for preview + async readback from RT (same as ScreenVideoSource)
                Graphics.CopyTexture(m_readRT, _previewTexture);
                AsyncGPUReadback.RequestIntoNativeArray(
                    ref _captureBuffer, m_readRT, 0, m_textureFormat, OnReadback);
            }
            catch (Exception e)
            {
                PilotLog.Error("PilotEditorCameraVideoSource ReadBuffer failed", e);
                _reading = false;
            }

            return textureChanged;
        }

        protected override bool SendFrame()
        {
            try
            {
                var result = base.SendFrame();

                if (result)
                {
                    ClearReadRT();
                }

                return result;
            }
            catch (Exception e)
            {
                PilotLog.Error("PilotEditorCameraVideoSource SendFrame failed", e);
                _reading = false;
                _requestPending = false;
                return false;
            }
        }

        private void ClearReadRT()
        {
            if (m_readRT != null)
            {
                m_readRT.Release();
                m_readRT = null;
            }
        }

        private Camera ResolveBaseCamera()
        {
            if (m_baseCamera != null && m_baseCamera.isActiveAndEnabled && m_baseCamera.cameraType == CameraType.Game)
            {
                return m_baseCamera;
            }

            Camera fallback = null;

            foreach (var cam in Camera.allCameras)
            {
                if (cam.cameraType != CameraType.Game || !cam.isActiveAndEnabled)
                {
                    continue;
                }

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

            if (maxDimension <= 0)
            {
                maxDimension = Mathf.Max(sourceWidth, sourceHeight);
            }

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
                    case GraphicsDeviceType.Vulkan:
                        return GraphicsFormat.B8G8R8A8_SRGB;
                    case GraphicsDeviceType.OpenGLCore:
                    case GraphicsDeviceType.OpenGLES2:
                    case GraphicsDeviceType.OpenGLES3:
                        return GraphicsFormat.R8G8B8A8_SRGB;
                    case GraphicsDeviceType.Metal:
                        return GraphicsFormat.B8G8R8A8_SRGB;
                    default:
                        return GraphicsFormat.B8G8R8A8_SRGB;
                }
            }

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Vulkan:
                    return GraphicsFormat.B8G8R8A8_UNorm;
                default:
                    return GraphicsFormat.R8G8B8A8_UNorm;
            }
        }
    }
}
#endif