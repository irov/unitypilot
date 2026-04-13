using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// A tab in the Pilot dashboard UI.
    /// </summary>
    public sealed class PilotTab
    {
        private readonly PilotUI m_ui;
        private readonly int m_internalId;
        private string m_publicId;
        private readonly string m_title;
        private PilotLayout m_layout;

        internal PilotTab(PilotUI ui, string title)
        {
            m_ui = ui;
            m_internalId = ui.NextId();
            m_publicId = "tab-" + m_internalId;
            m_title = title;
        }

        public string Id => m_publicId;

        public PilotTab SetId(string id)
        {
            m_publicId = id;
            return this;
        }

        public int InternalId => m_internalId;
        public string Title => m_title;
        public PilotLayout Layout => m_layout;

        public PilotLayout Vertical()
        {
            m_layout = new PilotLayout(m_ui, PilotLayout.Direction.Vertical);
            return m_layout;
        }

        public PilotLayout Horizontal()
        {
            m_layout = new PilotLayout(m_ui, PilotLayout.Direction.Horizontal);
            return m_layout;
        }

        internal Dictionary<string, object> ToDict()
        {
            var dict = new Dictionary<string, object>
            {
                ["id"] = m_internalId,
                ["title"] = m_title
            };

            if (m_layout != null)
                dict["layout"] = m_layout.ToDict();

            return dict;
        }
    }
}
