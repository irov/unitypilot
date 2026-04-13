using System;

namespace Pilot.SDK
{
    /// <summary>
    /// Button widget. Triggers a "click" action when pressed on the dashboard.
    /// </summary>
    public sealed class PilotButton : PilotWidget<PilotButton>
    {
        internal PilotButton(PilotUI ui, string label) : base(ui, "button")
        {
            Put("label", label);
        }

        public PilotButton Variant(string variant) { Put("variant", variant); return this; }
        public PilotButton Color(string color) { Put("color", color); return this; }
        public PilotButton Disabled(bool disabled) { Put("disabled", disabled); return this; }

        /// <summary>
        /// Register a click handler that auto-acknowledges the action after the callback completes.
        /// </summary>
        public PilotButton OnClick(Action callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback();
                PilotSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a click handler that receives the full PilotAction.
        /// The caller is responsible for calling PilotSDK.AcknowledgeAction when ready.
        /// </summary>
        public PilotButton OnClickAcknowledge(PilotWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
