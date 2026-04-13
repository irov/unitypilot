namespace Pilot.SDK
{
    /// <summary>
    /// Listener for session lifecycle events.
    /// All methods are called on the main thread.
    /// </summary>
    public interface IPilotSessionListener
    {
        void OnPilotSessionConnecting() { }
        void OnPilotSessionWaitingApproval(string requestId) { }
        void OnPilotSessionStarted(string sessionToken) { }
        void OnPilotSessionClosed() { }
        void OnPilotSessionRejected() { }
        void OnPilotSessionAuthFailed() { }
        void OnPilotSessionError(PilotException exception) { }
    }
}
