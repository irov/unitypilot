namespace Pilot.SDK
{
    /// <summary>
    /// Stat widget. Displays a numeric value with a label and unit.
    /// </summary>
    public sealed class PilotStat : PilotWidget<PilotStat>
    {
        internal PilotStat(PilotUI ui, string label) : base(ui, "stat")
        {
            Put("label", label);
        }

        public PilotStat Value(string value) { Put("value", value); return this; }
        public PilotStat Unit(string unit) { Put("unit", unit); return this; }

        public PilotStat ValueProvider(PilotValueProvider provider)
        {
            SetProvider("value", provider);
            return this;
        }
    }
}
