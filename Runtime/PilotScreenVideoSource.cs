#if PILOT_LIVEKIT
using LiveKit;
using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Overrides ScreenVideoSource to use maxDimension-based RT sizing
    /// instead of Screen.width/height (which returns simulated resolution
    /// in Device Simulator). ScreenCapture auto-downscales to the RT size.
    ///
    /// Uses a static field to pass dimensions before the base constructor
    /// calls GetWidth()/GetHeight() via Init().
    /// </summary>
    internal sealed class PilotScreenVideoSource : ScreenVideoSource
    {
        // Thread-confined to main thread (Unity API), so static is safe here.
        private static int s_pendingWidth;
        private static int s_pendingHeight;

        private readonly int m_width;
        private readonly int m_height;

        internal PilotScreenVideoSource(int maxDimension)
            : base(Prepare(maxDimension))
        {
            m_width = s_pendingWidth;
            m_height = s_pendingHeight;
        }

        /// <summary>
        /// Computes dimensions and stores them in static fields before the base
        /// constructor runs Init() → GetWidth()/GetHeight().
        /// Returns the buffer type to pass through to base(bufferType).
        /// </summary>
        private static VideoBufferType Prepare(int maxDimension)
        {
            ComputeDimensions(maxDimension, out s_pendingWidth, out s_pendingHeight);
            return VideoBufferType.Rgba;
        }

        public override int GetWidth()
        {
            // During base constructor: m_width is 0, use s_pendingWidth
            return m_width != 0 ? m_width : s_pendingWidth;
        }

        public override int GetHeight()
        {
            return m_height != 0 ? m_height : s_pendingHeight;
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
