namespace Pilot.SDK
{
    /// <summary>
    /// Listener for Pilot SDK internal diagnostic logs.
    /// </summary>
    public interface IPilotLoggerListener
    {
        void OnPilotLoggerMessage(PilotLogLevel level, string tag, string message, System.Exception exception);
    }
}
