namespace Pilot.SDK
{
    /// <summary>
    /// Typed action for <see cref="PilotSwitch"/> change events.
    /// Provides a strongly-typed <see cref="Value"/> instead of raw payload access.
    /// </summary>
    public sealed class PilotSwitchAction
    {
        public PilotAction Action { get; }
        public bool Value { get; }

        internal PilotSwitchAction(PilotAction action)
        {
            Action = action;
            Value = action.Payload?.GetBool("value") ?? false;
        }
    }

    /// <summary>
    /// Typed action for <see cref="PilotInput"/> submit events.
    /// </summary>
    public sealed class PilotInputAction
    {
        public PilotAction Action { get; }
        public string Value { get; }

        internal PilotInputAction(PilotAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    /// <summary>
    /// Typed action for <see cref="PilotSelect"/> change events.
    /// </summary>
    public sealed class PilotSelectAction
    {
        public PilotAction Action { get; }
        public string Value { get; }

        internal PilotSelectAction(PilotAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    /// <summary>
    /// Typed action for <see cref="PilotTextarea"/> submit events.
    /// </summary>
    public sealed class PilotTextareaAction
    {
        public PilotAction Action { get; }
        public string Value { get; }

        internal PilotTextareaAction(PilotAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }
}
