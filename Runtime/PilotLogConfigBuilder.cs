namespace Pilot.SDK
{
    /// <summary>
    /// Builder for log subsystem configuration.
    /// </summary>
    public sealed class PilotLogConfigBuilder
    {
        private bool m_enabled = true;
        private PilotLogLevel m_logLevel = PilotLogLevel.Info;
        private int m_batchSize = 100;
        private int m_bufferSize = 1000;
        private PilotLogAttributeBuilder m_attributes = new PilotLogAttributeBuilder();

        public PilotLogConfigBuilder SetEnabled(bool enabled) { m_enabled = enabled; return this; }
        public PilotLogConfigBuilder SetLogLevel(PilotLogLevel level) { m_logLevel = level; return this; }
        public PilotLogConfigBuilder SetBatchSize(int size) { m_batchSize = size; return this; }
        public PilotLogConfigBuilder SetBufferSize(int size) { m_bufferSize = size; return this; }
        public PilotLogConfigBuilder SetAttributes(PilotLogAttributeBuilder attributes) { m_attributes = attributes; return this; }

        internal bool IsEnabled() => m_enabled;
        internal PilotLogLevel GetLogLevel() => m_logLevel;
        internal int GetBatchSize() => m_batchSize;
        internal int GetBufferSize() => m_bufferSize;
        internal PilotLogAttributeBuilder GetAttributes() => m_attributes;
    }
}
