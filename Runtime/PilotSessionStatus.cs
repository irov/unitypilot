namespace Pilot.SDK
{
    public enum PilotSessionStatus
    {
        Disconnected,
        Connecting,
        WaitingApproval,
        Active,
        AuthFailed,
        Rejected,
        Closed,
        Error
    }
}
