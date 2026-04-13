#if PILOT_LIVEKIT
using LiveKit;
using UnityEngine;

namespace Pilot.SDK
{
    internal sealed class PilotScreenVideoSource : ScreenVideoSource
    {
        public override int GetWidth()
        {
            return Display.main.renderingWidth;
        }

        public override int GetHeight()
        {
            return Display.main.renderingHeight;
        }
    }
}
#endif
