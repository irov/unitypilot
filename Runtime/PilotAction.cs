namespace Pilot.SDK
{
    /// <summary>
    /// An action dispatched from the Pilot dashboard (button click, switch toggle, etc.).
    /// </summary>
    public sealed class PilotAction
    {
        public string Id { get; }
        public string SessionId { get; }
        public int WidgetId { get; }
        public PilotActionType ActionType { get; }
        public PilotActionStatus Status { get; }
        public SimpleJson Payload { get; }

        internal PilotAction(string id, string sessionId, int widgetId,
            PilotActionType actionType, PilotActionStatus status, SimpleJson payload)
        {
            Id = id;
            SessionId = sessionId;
            WidgetId = widgetId;
            ActionType = actionType;
            Status = status;
            Payload = payload;
        }

        internal static PilotAction FromDict(SimpleJson json)
        {
            return new PilotAction(
                json.GetString("id", ""),
                json.GetString("session_id", ""),
                json.GetInt("widget_id", 0),
                PilotActionTypeExtensions.FromValue(json.GetString("action_type", "")),
                PilotActionStatusExtensions.FromValue(json.GetString("status", "")),
                json.GetObject("payload")
            );
        }
    }
}
