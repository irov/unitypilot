using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Select (dropdown) widget.
    /// </summary>
    public sealed class PilotSelect : PilotWidget<PilotSelect>
    {
        internal PilotSelect(PilotUI ui, string label) : base(ui, "select")
        {
            Put("label", label);
        }

        public PilotSelect Options(string[][] options)
        {
            var list = new List<object>();
            foreach (var opt in options)
            {
                list.Add(new Dictionary<string, object>
                {
                    ["value"] = opt[0],
                    ["label"] = opt[1]
                });
            }
            Put("options", list);
            return this;
        }

        public PilotSelect DefaultValue(string value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotSelect OnChange(System.Action<PilotAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(action);
                PilotSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler where the caller must acknowledge manually.
        /// </summary>
        public PilotSelect OnChangeAcknowledge(PilotWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
