namespace Pilot.SDK
{
    /// <summary>
    /// Listener for actions dispatched from the Pilot dashboard.
    /// </summary>
    public interface IPilotActionListener
    {
        void OnPilotActionReceived(PilotAction action);
    }
}
