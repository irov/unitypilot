namespace Pilot.SDK
{
    public enum PilotActionType
    {
        Click,
        Change,
        Toggle,
        LiveStart,
        LiveUpdate,
        LiveStop,
        LiveTap,
        LiveLongPress,
        Unknown
    }

    internal static class PilotActionTypeExtensions
    {
        public static string ToValue(this PilotActionType type)
        {
            switch (type)
            {
                case PilotActionType.Click: return "click";
                case PilotActionType.Change: return "change";
                case PilotActionType.Toggle: return "toggle";
                case PilotActionType.LiveStart: return "live_start";
                case PilotActionType.LiveUpdate: return "live_update";
                case PilotActionType.LiveStop: return "live_stop";
                case PilotActionType.LiveTap: return "live_tap";
                case PilotActionType.LiveLongPress: return "live_long_press";
                default: return "";
            }
        }

        public static PilotActionType FromValue(string value)
        {
            switch (value)
            {
                case "click": return PilotActionType.Click;
                case "change": return PilotActionType.Change;
                case "toggle": return PilotActionType.Toggle;
                case "live_start": return PilotActionType.LiveStart;
                case "live_update": return PilotActionType.LiveUpdate;
                case "live_stop": return PilotActionType.LiveStop;
                case "live_tap": return PilotActionType.LiveTap;
                case "live_long_press": return PilotActionType.LiveLongPress;
                default: return PilotActionType.Unknown;
            }
        }
    }
}
