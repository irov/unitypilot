namespace Pilot.SDK
{
    /// <summary>
    /// Label widget. Displays text with an optional color indicator.
    /// </summary>
    public sealed class PilotLabel : PilotWidget<PilotLabel>
    {
        internal PilotLabel(PilotUI ui, string text) : base(ui, "label")
        {
            Put("text", text);
        }

        public PilotLabel Text(string text) { Put("text", text); return this; }
        public PilotLabel Color(string color) { Put("color", color); return this; }

        public PilotLabel TextProvider(PilotValueProvider provider)
        {
            SetProvider("text", provider);
            return this;
        }
    }
}
