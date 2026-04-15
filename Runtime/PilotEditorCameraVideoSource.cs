#if PILOT_LIVEKIT && UNITY_EDITOR
using System;
using System.Reflection;
using LiveKit;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Pilot.SDK
{
    /// <summary>
    /// Uses a manually rendered game camera to feed a RenderTexture into LiveKit's
    /// standard TextureVideoSource pipeline. This avoids Simulator chrome in the Editor.
    /// Capture happens inside ReadBuffer() so it is always in sync with the readback.
    /// </summary>
    internal sealed class PilotEditorCameraVideoSource : TextureVideoSource
    {
        private static readonly PropertyInfo s_renderTypeProp;

        private readonly RenderTexture m_cameraRT;
        private readonly RenderTexture m_outputRT;
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

        internal PilotEditorCameraVideoSource(int maxDimension)
            : base(CreateOutputRT(maxDimension))
        {
            m_outputRT = (RenderTexture)Texture;
            m_cameraRT = CreateCameraRT(maxDimension);
        }

        public override void Stop()
        {
            base.Stop();

            if (m_cameraRT != null)
            {
                m_cameraRT.Release();
            }

            if (m_outputRT != null)
            {
                m_outputRT.Release();
            }
        }

        protected override bool ReadBuffer()
        {
            if (_reading)
            {
                return false;
            }

            var baseCamera = ResolveBaseCamera();
            if (baseCamera != null)
            {
                var previousTarget = baseCamera.targetTexture;
                baseCamera.targetTexture = m_cameraRT;
                baseCamera.Render();
                baseCamera.targetTexture = previousTarget;

                Graphics.Blit(m_cameraRT, m_outputRT, new Vector2(1f, -1f), new Vector2(0f, 1f));
            }

            return base.ReadBuffer();
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

        private static RenderTexture CreateCameraRT(int maxDimension)
        {
            int width, height;
            ComputeDimensions(maxDimension, out width, out height);
            return CreateRT("PilotEditorCameraRT", width, height, 24);
        }

        private static RenderTexture CreateOutputRT(int maxDimension)
        {
            int width, height;
            ComputeDimensions(maxDimension, out width, out height);
            return CreateRT("PilotEditorOutputRT", width, height, 0);
        }

        private static RenderTexture CreateRT(string name, int width, int height, int depth)
        {
            var targetFormat = GetGraphicsFormat();
            var compatibleFormat = SystemInfo.GetCompatibleFormat(targetFormat, FormatUsage.ReadPixels);
            var rt = new RenderTexture(width, height, depth, compatibleFormat);
            rt.name = name;
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