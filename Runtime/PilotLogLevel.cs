namespace Pilot.SDK
{
    public enum PilotLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical,
        Exception
    }

    internal static class PilotLogLevelExtensions
    {
        public static string ToValue(this PilotLogLevel level)
        {
            switch (level)
            {
                case PilotLogLevel.Debug: return "debug";
                case PilotLogLevel.Info: return "info";
                case PilotLogLevel.Warning: return "warning";
                case PilotLogLevel.Error: return "error";
                case PilotLogLevel.Critical: return "critical";
                case PilotLogLevel.Exception: return "exception";
                default: return "info";
            }
        }
    }
}
