namespace Pilot.SDK
{
    /// <summary>
    /// Logs display widget.
    /// </summary>
    public sealed class PilotLogs : PilotWidget<PilotLogs>
    {
        internal PilotLogs(PilotUI ui, string label) : base(ui, "logs")
        {
            Put("label", label);
        }

        public PilotLogs MaxLines(int maxLines) { Put("maxLines", maxLines); return this; }
    }
}
