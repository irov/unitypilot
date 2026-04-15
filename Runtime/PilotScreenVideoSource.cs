#if PILOT_LIVEKIT
using System.Collections;
using LiveKit;
using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Captures the final composited game frame using ScreenCapture.CaptureScreenshotIntoRenderTexture
    /// at WaitForEndOfFrame. This works with any render pipeline and camera configuration.
    /// </summary>
    internal sealed class PilotScreenVideoSource : TextureVideoSource
    {
        private RenderTexture m_captureRT;
        private Coroutine m_captureCoroutine;

        internal PilotScreenVideoSource(int maxDimension)
            : base(CreateRT(maxDimension))
        {
            m_captureRT = (RenderTexture)Texture;
        }

        public override void Start()
        {
            base.Start();

            if (PilotRunner.Instance != null)
            {
                m_captureCoroutine = PilotRunner.Instance.StartCoroutine(CaptureLoop());
            }
        }

        public override void Stop()
        {
            if (m_captureCoroutine != null && PilotRunner.Instance != null)
            {
                PilotRunner.Instance.StopCoroutine(m_captureCoroutine);
                m_captureCoroutine = null;
            }

            base.Stop();

            if (m_captureRT != null)
            {
                m_captureRT.Release();
                m_captureRT = null;
            }
        }

        private IEnumerator CaptureLoop()
        {
            var waitEndOfFrame = new WaitForEndOfFrame();

            while (true)
            {
                yield return waitEndOfFrame;

                if (m_captureRT != null)
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(m_captureRT);
                }
            }
        }

        private static RenderTexture CreateRT(int maxDimension)
        {
            int width, height;
            ComputeDimensions(maxDimension, out width, out height);

            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.name = "PilotCaptureRT";
            rt.filterMode = FilterMode.Bilinear;
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.Create();

            return rt;
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
    }
}
#endif
