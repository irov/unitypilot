using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Builder for metric subsystem configuration.
    /// </summary>
    public sealed class PilotMetricConfigBuilder
    {
        private bool m_enabled = true;
        private long m_sampleIntervalMs = 200;
        private int m_bufferSize = 2000;
        private int m_batchSize = 200;
        private readonly List<IPilotMetricCollector> m_collectors = new List<IPilotMetricCollector>();

        public PilotMetricConfigBuilder SetEnabled(bool enabled) { m_enabled = enabled; return this; }

        public PilotMetricConfigBuilder SetSampleIntervalMs(long ms)
        {
            m_sampleIntervalMs = System.Math.Max(100, System.Math.Min(1000, ms));
            return this;
        }

        public PilotMetricConfigBuilder SetBufferSize(int size) { m_bufferSize = size; return this; }
        public PilotMetricConfigBuilder SetBatchSize(int size) { m_batchSize = size; return this; }

        public PilotMetricConfigBuilder AddCollector(IPilotMetricCollector collector)
        {
            m_collectors.Add(collector);
            return this;
        }

        internal bool IsEnabled() => m_enabled;
        internal long GetSampleIntervalMs() => m_sampleIntervalMs;
        internal int GetBufferSize() => m_bufferSize;
        internal int GetBatchSize() => m_batchSize;
        internal List<IPilotMetricCollector> GetCollectors() => m_collectors;
    }
}
