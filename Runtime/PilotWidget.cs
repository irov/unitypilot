using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Base class for all Pilot UI widgets.
    /// Stores properties as a dictionary for direct serialization.
    /// </summary>
    public class PilotWidget<T> where T : PilotWidget<T>
    {
        protected readonly PilotUI m_ui;
        protected readonly int m_internalId;
        protected string m_publicId;
        protected readonly string m_type;
        protected readonly Dictionary<string, object> m_json;

        private string m_providerKey;
        private PilotValueProvider m_provider;
        private string m_cachedValue;

        internal PilotWidget(PilotUI ui, string type)
        {
            m_ui = ui;
            m_internalId = ui.NextId();
            m_publicId = type + "-" + m_internalId;
            m_type = type;
            m_json = new Dictionary<string, object>
            {
                ["type"] = type,
                ["id"] = m_internalId
            };
        }

        public string Id => m_publicId;
        public int InternalId => m_internalId;
        public string Type => m_type;

        protected void Put(string key, object value)
        {
            m_json[key] = value;
            m_ui.IncrementRevision();
        }

        public T SetId(string id)
        {
            m_publicId = id;
            return (T)this;
        }

        protected void SetProvider(string key, PilotValueProvider provider)
        {
            m_providerKey = key;
            m_provider = provider;
            if (provider != null)
                m_ui.RegisterProvider(this);
            else
                m_ui.UnregisterProvider(this);
        }

        internal bool PollProvider()
        {
            if (m_provider == null) return false;
            try
            {
                string newValue = m_provider()?.ToString();
                if (newValue != m_cachedValue)
                {
                    m_cachedValue = newValue;
                    m_json[m_providerKey] = newValue;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                PilotLog.Error("Value provider failed for widget " + m_internalId + ": " + e.Message);
            }
            return false;
        }

        internal Dictionary<string, object> ToDict() => m_json;
    }
}
