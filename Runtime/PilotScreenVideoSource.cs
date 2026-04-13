#if PILOT_LIVEKIT
using System.Collections;
using LiveKit;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pilot.SDK
{
    /// <summary>
    /// Captures the game camera output (not the full Game View / Device Simulator chrome)
    /// by rendering Camera.main into a dedicated RenderTexture each frame.
    /// </summary>
    internal sealed class PilotScreenVideoSource : TextureVideoSource
    {
        private readonly RenderTexture m_renderTexture;
        private readonly Camera m_camera;

        internal PilotScreenVideoSource(int maxDimension)
            : this(ResolveCamera(), maxDimension)
        {
        }

        private PilotScreenVideoSource(Camera camera, int maxDimension)
            : base(CreateRenderTexture(camera, maxDimension))
        {
            m_camera = camera;
            m_renderTexture = (RenderTexture)Texture;
        }

        internal new IEnumerator Update()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (m_camera == null)
                    continue;

                var prev = m_camera.targetTexture;
                m_camera.targetTexture = m_renderTexture;
                m_camera.Render();
                m_camera.targetTexture = prev;
            }
        }

        public override void Stop()
        {
            base.Stop();

            if (m_renderTexture != null)
            {
                m_renderTexture.Release();
            }
        }

        private static Camera ResolveCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = Camera.current;
            }
            if (cam == null && Camera.allCamerasCount > 0)
            {
                var cameras = Camera.allCameras;
                cam = cameras[0];
            }
            return cam;
        }

        private static RenderTexture CreateRenderTexture(Camera camera, int maxDimension)
        {
            int width, height;

            if (camera != null)
            {
                width = camera.pixelWidth;
                height = camera.pixelHeight;
            }
            else
            {
                width = Screen.width;
                height = Screen.height;
            }

            if (maxDimension > 0)
            {
                float scale = Mathf.Min(1f, (float)maxDimension / Mathf.Max(width, height));
                width = Mathf.Max(2, Mathf.RoundToInt(width * scale));
                height = Mathf.Max(2, Mathf.RoundToInt(height * scale));
            }

            // Ensure even dimensions for video encoding
            width = (width / 2) * 2;
            height = (height / 2) * 2;

            return new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        }
    }
}
#endif
