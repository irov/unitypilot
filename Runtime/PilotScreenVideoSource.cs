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
    /// Overrides ScreenVideoSource to use maxDimension-based RT sizing
    /// instead of Screen.width/height (which returns simulated resolution
    /// in Device Simulator). ScreenCapture auto-downscales to the RT size.
    /// </summary>
    internal sealed class PilotScreenVideoSource : ScreenVideoSource
    {
        private readonly int m_width;
        private readonly int m_height;

        internal PilotScreenVideoSource(int maxDimension)
            : base()
        {
            ComputeDimensions(maxDimension, out m_width, out m_height);
        }

        public override int GetWidth()
        {
            return m_width;
        }

        public override int GetHeight()
        {
            return m_height;
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
