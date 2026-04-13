using System;

namespace Pilot.SDK
{
    /// <summary>
    /// Text input widget.
    /// </summary>
    public sealed class PilotInput : PilotWidget<PilotInput>
    {
        internal PilotInput(PilotUI ui, string label) : base(ui, "input")
        {
            Put("label", label);
        }

        public PilotInput InputType(string type) { Put("inputType", type); return this; }
        public PilotInput DefaultValue(string value) { Put("defaultValue", value); return this; }
        public PilotInput Placeholder(string placeholder) { Put("placeholder", placeholder); return this; }

        /// <summary>
        /// Register a submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotInput OnSubmit(Action<PilotAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(action);
                PilotSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a submit handler where the caller must acknowledge manually.
        /// </summary>
        public PilotInput OnSubmitAcknowledge(PilotWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
