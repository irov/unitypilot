using System.Collections.Generic;
using System.Threading;

namespace Pilot.SDK
{
    /// <summary>
    /// Central UI data source for the Pilot SDK.
    /// </summary>
    public sealed class PilotUI
    {
        private readonly List<PilotTab> m_tabs = new List<PilotTab>();
        private readonly Dictionary<int, PilotWidgetCallback> m_callbacks = new Dictionary<int, PilotWidgetCallback>();
        private readonly HashSet<object> m_providers = new HashSet<object>();
        private int m_idCounter;
        private int m_version = 2;
        private int m_revision = 1;
        private int m_sentRevision;
        private readonly object m_lock = new object();

        internal PilotUI() { }

        internal int NextId()
        {
            return Interlocked.Increment(ref m_idCounter);
        }

        // ── Tab management ──

        public PilotTab AddTab(string title)
        {
            lock (m_lock)
            {
                m_tabs.RemoveAll(t => t.Title == title);
                var tab = new PilotTab(this, title);
                m_tabs.Add(tab);
                Interlocked.Increment(ref m_revision);
                return tab;
            }
        }

        public PilotTab GetTab(string id)
        {
            lock (m_lock)
            {
                foreach (var tab in m_tabs)
                {
                    if (tab.Id == id)
                        return tab;
                }
                return null;
            }
        }

        public void RemoveTab(string id)
        {
            lock (m_lock)
            {
                m_tabs.RemoveAll(t => t.Id == id);
                Interlocked.Increment(ref m_revision);
            }
        }

        // ── Widget callbacks ──

        internal void RegisterCallback(int widgetId, PilotWidgetCallback callback)
        {
            lock (m_lock)
            {
                if (callback != null)
                    m_callbacks[widgetId] = callback;
                else
                    m_callbacks.Remove(widgetId);
            }
        }

        internal bool DispatchAction(PilotAction action)
        {
            PilotWidgetCallback cb;
            lock (m_lock)
            {
                if (!m_callbacks.TryGetValue(action.WidgetId, out cb))
                    return false;
            }
            cb(action);
            return true;
        }

        // ── Value providers ──

        internal void RegisterProvider<T>(PilotWidget<T> widget) where T : PilotWidget<T>
        {
            lock (m_lock) { m_providers.Add(widget); }
        }

        internal void UnregisterProvider<T>(PilotWidget<T> widget) where T : PilotWidget<T>
        {
            lock (m_lock) { m_providers.Remove(widget); }
        }

        internal void PollValues()
        {
            lock (m_lock)
            {
                foreach (var provider in m_providers)
                {
                    var method = provider.GetType().GetMethod("PollProvider",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (method != null)
                    {
                        bool changed = (bool)method.Invoke(provider, null);
                        if (changed)
                            Interlocked.Increment(ref m_revision);
                    }
                }
            }
        }

        // ── Revision tracking ──

        internal void IncrementRevision()
        {
            Interlocked.Increment(ref m_revision);
        }

        internal bool HasUnsent()
        {
            return m_revision != m_sentRevision;
        }

        internal void MarkSent()
        {
            m_sentRevision = m_revision;
        }

        internal bool HasTabs()
        {
            lock (m_lock) { return m_tabs.Count > 0; }
        }

        public int Revision => m_revision;

        // ── Serialization ──

        internal Dictionary<string, object> ToDict()
        {
            lock (m_lock)
            {
                var tabsList = new List<object>();
                foreach (var tab in m_tabs)
                    tabsList.Add(tab.ToDict());

                return new Dictionary<string, object>
                {
                    ["version"] = m_version,
                    ["revision"] = m_revision,
                    ["tabs"] = tabsList
                };
            }
        }

        internal string ToJson()
        {
            return SimpleJson.Serialize(ToDict());
        }
    }
}
