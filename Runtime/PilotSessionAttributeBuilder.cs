using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Builder for session attributes — both static values and dynamic providers.
    /// </summary>
    public sealed class PilotSessionAttributeBuilder
    {
        private readonly Dictionary<string, object> m_static = new Dictionary<string, object>();
        private readonly Dictionary<string, PilotValueProvider> m_dynamic = new Dictionary<string, PilotValueProvider>();

        public PilotSessionAttributeBuilder Put(string key, object value)
        {
            m_static[key] = value;
            return this;
        }

        public PilotSessionAttributeBuilder PutProvider(string key, PilotValueProvider provider)
        {
            m_dynamic[key] = provider;
            return this;
        }

        internal Dictionary<string, object> GetStaticAttributes() => m_static;
        internal Dictionary<string, PilotValueProvider> GetDynamicAttributes() => m_dynamic;
    }
}
