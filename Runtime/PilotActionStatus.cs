namespace Pilot.SDK
{
    public enum PilotActionStatus
    {
        Pending,
        Delivered,
        Acknowledged,
        Unknown
    }

    internal static class PilotActionStatusExtensions
    {
        public static string ToValue(this PilotActionStatus status)
        {
            switch (status)
            {
                case PilotActionStatus.Pending: return "pending";
                case PilotActionStatus.Delivered: return "delivered";
                case PilotActionStatus.Acknowledged: return "acknowledged";
                default: return "";
            }
        }

        public static PilotActionStatus FromValue(string value)
        {
            switch (value)
            {
                case "pending": return PilotActionStatus.Pending;
                case "delivered": return PilotActionStatus.Delivered;
                case "acknowledged": return PilotActionStatus.Acknowledged;
                default: return PilotActionStatus.Unknown;
            }
        }
    }
}
