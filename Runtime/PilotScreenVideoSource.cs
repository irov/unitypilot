#if PILOT_LIVEKIT
using LiveKit;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pilot.SDK
{
    /// <summary>
    /// Captures the camera output via URP's endCameraRendering callback,
    /// blitting CameraTarget into a small RenderTexture.
    /// This bypasses ScreenCapture which in the editor captures the full
    /// Game View including Device Simulator chrome.
    /// Uses TextureVideoSource so LiveKit reads from our RT each frame.
    /// </summary>
    internal sealed class PilotScreenVideoSource : TextureVideoSource
    {
        private readonly RenderTexture m_captureRT;

        internal PilotScreenVideoSource(int maxDimension)
            : base(CreateRT(maxDimension))
        {
            m_captureRT = (RenderTexture)Texture;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != Camera.main)
                return;

            var cmd = new CommandBuffer();
            cmd.name = "PilotCapture";
            cmd.Blit(BuiltinRenderTextureType.CameraTarget, m_captureRT);
            ctx.ExecuteCommandBuffer(cmd);
            ctx.Submit();
            cmd.Dispose();
        }

        public override void Stop()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            base.Stop();

            if (m_captureRT != null)
            {
                m_captureRT.Release();
            }
        }

        private static RenderTexture CreateRT(int maxDimension)
        {
            int width, height;
            ComputeDimensions(maxDimension, out width, out height);
            return new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        }

        private static void ComputeDimensions(int maxDimension, out int width, out int height)
        {
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
