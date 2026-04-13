using System;

namespace Pilot.SDK
{
    /// <summary>
    /// Switch (toggle) widget.
    /// </summary>
    public sealed class PilotSwitch : PilotWidget<PilotSwitch>
    {
        internal PilotSwitch(PilotUI ui, string label) : base(ui, "switch")
        {
            Put("label", label);
        }

        public PilotSwitch DefaultValue(bool value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a typed change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotSwitch OnChange(Action<PilotSwitchAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new PilotSwitchAction(action));
                PilotSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotSwitch OnChange(Action<PilotAction> callback)
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
        public PilotSwitch OnChangeAcknowledge(PilotWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
