using System;

namespace Pilot.SDK
{
    /// <summary>
    /// Multi-line text input widget.
    /// </summary>
    public sealed class PilotTextarea : PilotWidget<PilotTextarea>
    {
        internal PilotTextarea(PilotUI ui, string label) : base(ui, "textarea")
        {
            Put("label", label);
        }

        public PilotTextarea Rows(int rows) { Put("rows", rows); return this; }
        public PilotTextarea DefaultValue(string value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a typed submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotTextarea OnSubmit(Action<PilotTextareaAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new PilotTextareaAction(action));
                PilotSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public PilotTextarea OnSubmit(Action<PilotAction> callback)
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
        public PilotTextarea OnSubmitAcknowledge(PilotWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
