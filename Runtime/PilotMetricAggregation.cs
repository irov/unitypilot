namespace Pilot.SDK
{
    public enum PilotMetricAggregation
    {
        Gauge,
        Counter,
        Rate
    }

    internal static class PilotMetricAggregationExtensions
    {
        public static string ToKey(this PilotMetricAggregation aggregation)
        {
            switch (aggregation)
            {
                case PilotMetricAggregation.Gauge: return "gauge";
                case PilotMetricAggregation.Counter: return "counter";
                case PilotMetricAggregation.Rate: return "rate";
                default: return "gauge";
            }
        }
    }
}
