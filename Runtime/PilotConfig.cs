namespace Pilot.SDK
{
    /// <summary>
    /// Configuration for Pilot SDK initialization.
    /// </summary>
    public sealed class PilotConfig
    {
        internal readonly string BaseUrl;
        internal readonly string ApiToken;
        internal readonly string DeviceId;
        internal readonly string DeviceName;
        internal readonly long PollIntervalMs;
        internal readonly long ActionPollIntervalMs;
        internal readonly bool AutoConnect;
        internal readonly IPilotLoggerListener LoggerListener;
        internal readonly IPilotSessionListener SessionListener;
        internal readonly IPilotActionListener ActionListener;
        internal readonly PilotSessionAttributeBuilder SessionAttributes;
        internal readonly PilotLogConfigBuilder LogConfig;
        internal readonly PilotMetricConfigBuilder MetricConfig;

        private PilotConfig(Builder builder)
        {
            BaseUrl = builder.m_baseUrl;
            ApiToken = builder.m_apiToken;
            DeviceId = builder.m_deviceId;
            DeviceName = builder.m_deviceName;
            PollIntervalMs = builder.m_pollIntervalMs;
            ActionPollIntervalMs = builder.m_actionPollIntervalMs;
            AutoConnect = builder.m_autoConnect;
            LoggerListener = builder.m_loggerListener;
            SessionListener = builder.m_sessionListener;
            ActionListener = builder.m_actionListener;
            SessionAttributes = builder.m_sessionAttributes;
            LogConfig = builder.m_logConfig;
            MetricConfig = builder.m_metricConfig;
        }

        public string GetBaseUrl() => BaseUrl;
        public string GetApiToken() => ApiToken;
        public string GetDeviceId() => DeviceId;
        public string GetDeviceName() => DeviceName;

        public sealed class Builder
        {
            internal string m_baseUrl;
            internal string m_apiToken;
            internal string m_deviceId;
            internal string m_deviceName;
            internal long m_pollIntervalMs = 10000;
            internal long m_actionPollIntervalMs = 2000;
            internal bool m_autoConnect = true;
            internal IPilotLoggerListener m_loggerListener;
            internal IPilotSessionListener m_sessionListener;
            internal IPilotActionListener m_actionListener;
            internal PilotSessionAttributeBuilder m_sessionAttributes = new PilotSessionAttributeBuilder();
            internal PilotLogConfigBuilder m_logConfig = new PilotLogConfigBuilder();
            internal PilotMetricConfigBuilder m_metricConfig = new PilotMetricConfigBuilder();

            /// <param name="baseUrl">Pilot server URL, e.g. "https://pilot.example.com"</param>
            /// <param name="apiToken">Project API token starting with "plt_"</param>
            public Builder(string baseUrl, string apiToken)
            {
                m_baseUrl = baseUrl.EndsWith("/") ? baseUrl.Substring(0, baseUrl.Length - 1) : baseUrl;
                m_apiToken = apiToken;
            }

            public Builder SetDeviceId(string deviceId) { m_deviceId = deviceId; return this; }
            public Builder SetDeviceName(string deviceName) { m_deviceName = deviceName; return this; }
            public Builder SetPollIntervalMs(long ms) { m_pollIntervalMs = ms; return this; }
            public Builder SetActionPollIntervalMs(long ms) { m_actionPollIntervalMs = ms; return this; }
            public Builder SetAutoConnect(bool autoConnect) { m_autoConnect = autoConnect; return this; }
            public Builder SetLoggerListener(IPilotLoggerListener listener) { m_loggerListener = listener; return this; }
            public Builder SetSessionListener(IPilotSessionListener listener) { m_sessionListener = listener; return this; }
            public Builder SetActionListener(IPilotActionListener listener) { m_actionListener = listener; return this; }
            public Builder SetSessionAttributes(PilotSessionAttributeBuilder builder) { m_sessionAttributes = builder; return this; }
            public Builder SetLogConfig(PilotLogConfigBuilder config) { m_logConfig = config; return this; }
            public Builder SetMetricConfig(PilotMetricConfigBuilder config) { m_metricConfig = config; return this; }

            public PilotConfig Build()
            {
                if (string.IsNullOrEmpty(m_baseUrl))
                    throw new System.ArgumentException("baseUrl is required");
                if (string.IsNullOrEmpty(m_apiToken))
                    throw new System.ArgumentException("apiToken is required");
                return new PilotConfig(this);
            }
        }
    }
}
