#if PILOT_LIVEKIT
using System;
using System.Collections.Generic;
using System.Reflection;
using LiveKit;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Pilot.SDK
{
    /// <summary>
    /// Captures the final URP game frame through CameraCaptureBridge.
    /// This hooks into URP's built-in capture pass, which runs on the final
    /// resolved output of the last camera in the stack, avoiding Device
    /// Simulator editor chrome and camera stack ordering issues.
    /// </summary>
    internal sealed class PilotScreenVideoSource : TextureVideoSource
    {
        private static readonly Type s_cameraCaptureBridgeType =
            Type.GetType("UnityEngine.Rendering.CameraCaptureBridge, Unity.RenderPipelines.Core.Runtime");

        private static readonly MethodInfo s_addCaptureActionMethod =
            s_cameraCaptureBridgeType?.GetMethod("AddCaptureAction", BindingFlags.Public | BindingFlags.Static);

        private static readonly MethodInfo s_removeCaptureActionMethod =
            s_cameraCaptureBridgeType?.GetMethod("RemoveCaptureAction", BindingFlags.Public | BindingFlags.Static);

        private readonly RenderTexture m_captureRT;
        private readonly HashSet<Camera> m_registeredCameras = new HashSet<Camera>();
        private readonly Action<RenderTargetIdentifier, CommandBuffer> m_captureAction;

        internal PilotScreenVideoSource(int maxDimension)
            : base(CreateRT(maxDimension))
        {
            m_captureRT = (RenderTexture)Texture;
            m_captureAction = CaptureFrame;

            RefreshRegisteredCameras();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        public override void Stop()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            UnregisterAllCameras();
            base.Stop();

            if (m_captureRT != null)
            {
                m_captureRT.Release();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshRegisteredCameras();
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            RefreshRegisteredCameras();
        }

        private void RefreshRegisteredCameras()
        {
            if (s_addCaptureActionMethod == null)
            {
                PilotLog.Warn("CameraCaptureBridge is unavailable. Live capture requires URP camera capture support.");
                return;
            }

            foreach (var camera in Camera.allCameras)
            {
                if (camera == null || camera.cameraType != CameraType.Game)
                {
                    continue;
                }

                if (!m_registeredCameras.Add(camera))
                {
                    continue;
                }

                s_addCaptureActionMethod.Invoke(null, new object[] { camera, m_captureAction });
            }
        }

        private void UnregisterAllCameras()
        {
            if (s_removeCaptureActionMethod == null)
            {
                m_registeredCameras.Clear();
                return;
            }

            foreach (var camera in m_registeredCameras)
            {
                if (camera == null)
                {
                    continue;
                }

                s_removeCaptureActionMethod.Invoke(null, new object[] { camera, m_captureAction });
            }

            m_registeredCameras.Clear();
        }

        private void CaptureFrame(RenderTargetIdentifier source, CommandBuffer cmd)
        {
            if (m_captureRT == null)
            {
                return;
            }

            cmd.Blit(source, new RenderTargetIdentifier(m_captureRT));
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
