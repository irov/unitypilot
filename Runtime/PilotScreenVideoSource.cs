#if PILOT_LIVEKIT
using System;
using LiveKit;
using LiveKit.Proto;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Pilot.SDK
{
    /// <summary>
    /// Captures the composited game frame via ScreenCapture.CaptureScreenshotIntoRenderTexture
    /// following the same pattern as LiveKit's built-in ScreenVideoSource, but with dimension
    /// scaling for bandwidth efficiency.
    /// </summary>
    internal sealed class PilotScreenVideoSource : RtcVideoSource
    {
        private TextureFormat m_textureFormat;
        private RenderTexture m_renderTexture;
        private readonly int m_maxDimension;

        internal PilotScreenVideoSource(int maxDimension)
            : base(VideoStreamSource.Screen, VideoBufferType.Rgba)
        {
            m_maxDimension = maxDimension;
            base.Init();
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

        protected override VideoRotation GetVideoRotation()
        {
            return VideoRotation._0;
        }

        public override void Stop()
        {
            base.Stop();
            ClearRenderTexture();
        }

        private void ClearRenderTexture()
        {
            if (m_renderTexture != null)
            {
                m_renderTexture.Release();
                m_renderTexture = null;
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
                if (m_renderTexture == null || m_renderTexture.width != GetWidth() || m_renderTexture.height != GetHeight())
                {
                    ClearRenderTexture();

                    var targetFormat = GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
                    var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
                    m_textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                    _bufferType = GetVideoBufferType(m_textureFormat);
                    m_renderTexture = new RenderTexture(GetWidth(), GetHeight(), 0, compatibleFormat);
                    _captureBuffer = new NativeArray<byte>(GetWidth() * GetHeight() * GetStrideForBuffer(_bufferType), Allocator.Persistent);
                    _previewTexture = new Texture2D(GetWidth(), GetHeight(), m_textureFormat, false);
                    textureChanged = true;
                }

                ScreenCapture.CaptureScreenshotIntoRenderTexture(m_renderTexture);
                Graphics.CopyTexture(m_renderTexture, _previewTexture);
                AsyncGPUReadback.RequestIntoNativeArray(ref _captureBuffer, m_renderTexture, 0, m_textureFormat, OnReadback);
            }
            catch (Exception e)
            {
                PilotLog.Error("PilotScreenVideoSource ReadBuffer failed", e);
                _reading = false;
            }

            return textureChanged;
        }

        protected override bool SendFrame()
        {
            var result = base.SendFrame();
            if (result)
            {
                ClearRenderTexture();
            }
            return result;
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

            // Ensure even dimensions for video encoding.
            width = Mathf.Max(2, (width / 2) * 2);
            height = Mathf.Max(2, (height / 2) * 2);
        }

        private static GraphicsFormat GetSupportedGraphicsFormat(GraphicsDeviceType type)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                switch (type)
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

            switch (type)
            {
                case GraphicsDeviceType.Vulkan:
                    return GraphicsFormat.B8G8R8A8_UNorm;
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.OpenGLCore:
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                case GraphicsDeviceType.Metal:
                    return GraphicsFormat.R8G8B8A8_UNorm;
                default:
                    return GraphicsFormat.R8G8B8A8_UNorm;
            }
        }
    }
}
#endif
