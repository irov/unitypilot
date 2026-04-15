#if PILOT_LIVEKIT
using LiveKit;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#endif

namespace Pilot.SDK
{
    /// <summary>
    /// Extends LiveKit's built-in ScreenVideoSource with dimension scaling
    /// for bandwidth efficiency.
    /// In Editor: uses Camera.Render() to capture game content without editor/simulator chrome.
    /// On device: inherits ScreenCapture-based capture from ScreenVideoSource.
    /// </summary>
    internal sealed class PilotScreenVideoSource : ScreenVideoSource
    {
        private readonly int m_maxDimension;

#if UNITY_EDITOR
        private TextureFormat m_editorTextureFormat;
        private RenderTexture m_editorCameraRT;
        private RenderTexture m_editorFlippedRT;

        private static readonly PropertyInfo s_renderTypeProp;

        static PilotScreenVideoSource()
        {
            var urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");

            if (urpCameraDataType != null)
            {
                s_renderTypeProp = urpCameraDataType.GetProperty("renderType");
            }
        }
#endif

        internal PilotScreenVideoSource(int maxDimension)
            : base()
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

#if UNITY_EDITOR
        public override void Stop()
        {
            base.Stop();

            if (m_editorCameraRT != null)
            {
                m_editorCameraRT.Release();
                m_editorCameraRT = null;
            }

            if (m_editorFlippedRT != null)
            {
                m_editorFlippedRT.Release();
                m_editorFlippedRT = null;
            }
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
                var baseCamera = FindBaseCamera();

                if (baseCamera == null)
                {
                    _reading = false;
                    return false;
                }

                int w = GetWidth();
                int h = GetHeight();

                if (m_editorCameraRT == null || m_editorCameraRT.width != w || m_editorCameraRT.height != h)
                {
                    if (m_editorCameraRT != null)
                    {
                        m_editorCameraRT.Release();
                    }

                    if (m_editorFlippedRT != null)
                    {
                        m_editorFlippedRT.Release();
                    }

                    var targetFormat = GetEditorGraphicsFormat();
                    var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
                    m_editorTextureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                    _bufferType = GetVideoBufferType(m_editorTextureFormat);
                    m_editorCameraRT = new RenderTexture(w, h, 24, compatibleFormat);
                    m_editorFlippedRT = new RenderTexture(w, h, 0, compatibleFormat);

                    if (_captureBuffer.IsCreated)
                    {
                        _captureBuffer.Dispose();
                    }

                    _captureBuffer = new NativeArray<byte>(w * h * GetStrideForBuffer(_bufferType), Allocator.Persistent);

                    if (_previewTexture != null)
                    {
                        UnityEngine.Object.Destroy(_previewTexture);
                    }

                    _previewTexture = new Texture2D(w, h, m_editorTextureFormat, false);
                    textureChanged = true;
                }

                // Render camera stack into RT
                var prevTarget = baseCamera.targetTexture;
                baseCamera.targetTexture = m_editorCameraRT;
                baseCamera.Render();
                baseCamera.targetTexture = prevTarget;

                // Flip vertically — Camera.Render() output is Y-flipped on D3D
                Graphics.Blit(m_editorCameraRT, m_editorFlippedRT, new Vector2(1f, -1f), new Vector2(0f, 1f));

                // Synchronous readback — reliable in Editor unlike AsyncGPUReadback after Camera.Render
                var prevActive = RenderTexture.active;
                RenderTexture.active = m_editorFlippedRT;
                _previewTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                _previewTexture.Apply(false, false);
                RenderTexture.active = prevActive;

                // Copy pixel data into capture buffer for WebRTC
                var raw = _previewTexture.GetRawTextureData<byte>();
                NativeArray<byte>.Copy(raw, _captureBuffer, raw.Length);

                _requestPending = true;
            }
            catch (Exception e)
            {
                PilotLog.Error("PilotScreenVideoSource editor ReadBuffer failed", e);
                _reading = false;
            }

            return textureChanged;
        }

        private static Camera FindBaseCamera()
        {
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

                        if (renderType == 0) // CameraRenderType.Base
                        {
                            return cam;
                        }
                    }
                }
            }

            return fallback ?? Camera.main;
        }

        private static GraphicsFormat GetEditorGraphicsFormat()
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
#endif

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

            // Ensure even dimensions for video encoding.
            width = Mathf.Max(2, (width / 2) * 2);
            height = Mathf.Max(2, (height / 2) * 2);
        }
    }
}
#endif
