namespace Pilot.SDK
{
    /// <summary>
    /// Describes a type of metric that can be recorded.
    /// Use the built-in constants for standard system metrics,
    /// or create custom types for application-specific metrics.
    /// </summary>
    public sealed class PilotMetricType
    {
        public string Key { get; }
        public string Unit { get; }
        public PilotMetricAggregation Aggregation { get; }

        // Built-in metric types
        public static readonly PilotMetricType FPS = new PilotMetricType("fps", "", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType FrameTime = new PilotMetricType("frame_time", "ms", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType Memory = new PilotMetricType("memory", "bytes", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType VideoMemory = new PilotMetricType("video_memory", "bytes", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType CpuUsage = new PilotMetricType("cpu_usage", "%", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType NetworkRx = new PilotMetricType("network_rx", "bytes/s", PilotMetricAggregation.Rate);
        public static readonly PilotMetricType NetworkTx = new PilotMetricType("network_tx", "bytes/s", PilotMetricAggregation.Rate);
        public static readonly PilotMetricType BatteryLevel = new PilotMetricType("battery_level", "%", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType BatteryCharging = new PilotMetricType("battery_charging", "", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType DrawCalls = new PilotMetricType("draw_calls", "", PilotMetricAggregation.Gauge);
        public static readonly PilotMetricType ThreadCount = new PilotMetricType("thread_count", "", PilotMetricAggregation.Gauge);

        private PilotMetricType(string key, string unit, PilotMetricAggregation aggregation)
        {
            Key = key;
            Unit = unit;
            Aggregation = aggregation;
        }

        public static PilotMetricType Create(string key)
        {
            return new PilotMetricType(key, "", PilotMetricAggregation.Gauge);
        }

        public static PilotMetricType Create(string key, string unit)
        {
            return new PilotMetricType(key, unit, PilotMetricAggregation.Gauge);
        }

        public static PilotMetricType Create(string key, string unit, PilotMetricAggregation aggregation)
        {
            return new PilotMetricType(key, unit, aggregation);
        }

        public override bool Equals(object obj)
        {
            return obj is PilotMetricType other && Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override string ToString()
        {
            return Key;
        }
    }
}
