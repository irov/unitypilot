#if PILOT_LIVEKIT
using LiveKit;
using UnityEngine;

namespace Pilot.SDK
{
    /// <summary>
    /// Extends LiveKit's built-in ScreenVideoSource with dimension scaling
    /// for bandwidth efficiency.
    /// </summary>
    internal sealed class PilotScreenVideoSource : ScreenVideoSource
    {
        private readonly int m_maxDimension;

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
