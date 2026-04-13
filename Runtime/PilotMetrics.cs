using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Metrics subsystem for the Pilot SDK.
    /// </summary>
    public sealed class PilotMetrics
    {
        private readonly List<IPilotMetricCollector> m_collectors = new List<IPilotMetricCollector>();
        private readonly List<PilotMetricEntry> m_buffer = new List<PilotMetricEntry>();
        private readonly object m_lock = new object();

        private long m_sampleIntervalMs = 200;
        private int m_bufferSize = 2000;
        private int m_batchSize = 200;

        internal PilotMetrics() { }

        public void SetSampleIntervalMs(long intervalMs)
        {
            m_sampleIntervalMs = System.Math.Max(100, System.Math.Min(1000, intervalMs));
        }

        public long GetSampleIntervalMs() => m_sampleIntervalMs;

        public void SetBufferSize(int size) { m_bufferSize = size; }
        public void SetBatchSize(int size) { m_batchSize = size; }
        internal int GetBatchSize() => m_batchSize;

        public void AddCollector(IPilotMetricCollector collector)
        {
            lock (m_lock) { m_collectors.Add(collector); }
        }

        public void RemoveCollector(IPilotMetricCollector collector)
        {
            lock (m_lock) { m_collectors.Remove(collector); }
        }

        public void Record(PilotMetricType metricType, double value)
        {
            BufferEntry(new PilotMetricEntry(metricType, value));
        }

        public void Record(PilotMetricType metricType, double value, long timestampMs)
        {
            BufferEntry(new PilotMetricEntry(metricType, value, timestampMs));
        }

        internal void Sample()
        {
            List<IPilotMetricCollector> collectors;
            lock (m_lock) { collectors = new List<IPilotMetricCollector>(m_collectors); }

            var collected = new List<PilotMetricEntry>();
            foreach (var collector in collectors)
            {
                try { collector.Collect(collected); }
                catch (System.Exception e) { PilotLog.Error("Metric collector threw exception", e); }
            }

            foreach (var entry in collected)
                BufferEntry(entry);
        }

        internal List<PilotMetricEntry> Drain()
        {
            lock (m_lock)
            {
                if (m_buffer.Count == 0)
                    return new List<PilotMetricEntry>();

                int count = System.Math.Min(m_buffer.Count, m_batchSize);
                var chunk = new List<PilotMetricEntry>(m_buffer.GetRange(0, count));
                m_buffer.RemoveRange(0, count);
                return chunk;
            }
        }

        internal void Requeue(List<PilotMetricEntry> entries)
        {
            lock (m_lock)
            {
                m_buffer.InsertRange(0, entries);
                while (m_buffer.Count > m_bufferSize)
                    m_buffer.RemoveAt(m_buffer.Count - 1);
            }
        }

        internal bool HasData()
        {
            lock (m_lock) { return m_buffer.Count > 0; }
        }

        internal void Clear()
        {
            lock (m_lock)
            {
                m_buffer.Clear();
                m_collectors.Clear();
            }
        }

        private void BufferEntry(PilotMetricEntry entry)
        {
            lock (m_lock)
            {
                if (m_buffer.Count >= m_bufferSize)
                    m_buffer.RemoveAt(0);
                m_buffer.Add(entry);
            }
        }
    }
}
