using System;

namespace Pilot.SDK
{
    /// <summary>
    /// Pilot SDK exception.
    /// </summary>
    public class PilotException : Exception
    {
        public int HttpCode { get; }

        public PilotException(string message) : base(message)
        {
            HttpCode = 0;
        }

        public PilotException(string message, Exception innerException) : base(message, innerException)
        {
            HttpCode = 0;
        }

        public PilotException(int httpCode, string message) : base(message)
        {
            HttpCode = httpCode;
        }

        public bool IsNetworkError => HttpCode == 0 && InnerException != null;
        public bool IsSessionGone => HttpCode == 410;
        public bool IsUnauthorized => HttpCode == 401;
    }
}
