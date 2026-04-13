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
    /// Captures the game screen via ScreenCapture.CaptureScreenshotIntoRenderTexture
    /// using a RenderTexture sized to maxDimension (not Screen.width/height which
    /// returns the simulated resolution in Device Simulator).
    /// ScreenCapture automatically downscales to the RT size when the RT is smaller
    /// than the actual screen.
    /// Extends RtcVideoSource directly to control Init() timing.
    /// </summary>
    internal sealed class PilotScreenVideoSource : RtcVideoSource
    {
        private readonly int m_width;
        private readonly int m_height;
        private TextureFormat m_textureFormat;
        private RenderTexture m_renderTexture;

        internal PilotScreenVideoSource(int maxDimension)
            : base(VideoStreamSource.Screen, VideoBufferType.Rgba)
        {
            ComputeDimensions(maxDimension, out m_width, out m_height);
            base.Init();
        }

        public override int GetWidth()
        {
            return m_width;
        }

        public override int GetHeight()
        {
            return m_height;
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

        ~PilotScreenVideoSource()
        {
            Dispose(false);
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
                return false;
            _reading = true;
            var textureChanged = false;

            try
            {
                if (m_renderTexture == null || m_renderTexture.width != m_width || m_renderTexture.height != m_height)
                {
                    ClearRenderTexture();

                    var targetFormat = Utils.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
                    var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
                    m_textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                    _bufferType = GetVideoBufferType(m_textureFormat);
                    m_renderTexture = new RenderTexture(m_width, m_height, 0, compatibleFormat);
                    _captureBuffer = new NativeArray<byte>(m_width * m_height * GetStrideForBuffer(_bufferType), Allocator.Persistent);
                    _previewTexture = new Texture2D(m_width, m_height, m_textureFormat, false);
                    textureChanged = true;
                }

                ScreenCapture.CaptureScreenshotIntoRenderTexture(m_renderTexture);
                Graphics.CopyTexture(m_renderTexture, _previewTexture);
                AsyncGPUReadback.RequestIntoNativeArray(ref _captureBuffer, m_renderTexture, 0, m_textureFormat, OnReadback);
            }
            catch (Exception e)
            {
                Utils.Error(e);
                _reading = false;
            }

            return textureChanged;
        }

        private static void ComputeDimensions(int maxDimension, out int width, out int height)
        {
            // Use camera aspect ratio — it reflects the actual game viewport
            // regardless of Device Simulator frame
            var cam = Camera.main;
            float aspect = cam != null
                ? (float)cam.pixelWidth / cam.pixelHeight
                : (float)Screen.width / Screen.height;

            if (aspect >= 1f)
            {
                width = maxDimension;
                height = Mathf.Max(2, Mathf.RoundToInt(maxDimension / aspect));
            }
            else
            {
                height = maxDimension;
                width = Mathf.Max(2, Mathf.RoundToInt(maxDimension * aspect));
            }

            // Ensure even dimensions for video encoding
            width = (width / 2) * 2;
            height = (height / 2) * 2;
        }
    }
}
#endif
