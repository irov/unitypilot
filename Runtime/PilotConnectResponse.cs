namespace Pilot.SDK
{
    /// <summary>
    /// Response from connect / poll-status endpoints.
    /// </summary>
    internal sealed class PilotConnectResponse
    {
        public string RequestId { get; }
        public string Status { get; }
        public string SessionToken { get; }

        public PilotConnectResponse(string requestId, string status, string sessionToken)
        {
            RequestId = requestId;
            Status = status;
            SessionToken = sessionToken;
        }

        public bool IsPending => Status == "pending";
        public bool IsApproved => Status == "approved";
        public bool IsRejected => Status == "rejected";

        public static PilotConnectResponse FromDict(SimpleJson json)
        {
            string token = json.IsNull("session_token") ? null : json.GetString("session_token", null);
            return new PilotConnectResponse(
                json.GetString("request_id", ""),
                json.GetString("status", ""),
                token
            );
        }
    }
}
