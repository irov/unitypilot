using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pilot.SDK
{
    /// <summary>
    /// A single log entry to be sent to the Pilot server.
    /// </summary>
    public sealed class PilotLogEntry
    {
        public string Level { get; }
        public string Message { get; }
        public string Category { get; }
        public string Thread { get; }
        public Dictionary<string, object> Metadata { get; }
        public Dictionary<string, object> Attributes { get; }
        public string ClientTimestamp { get; }

        private static string FormatTimestamp(long timestampMs)
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
            return dt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public PilotLogEntry(PilotLogLevel level, string message, Dictionary<string, object> metadata = null)
            : this(level.ToValue(), message, null, null, metadata, null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
        }

        public PilotLogEntry(PilotLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata = null, Dictionary<string, object> attributes = null)
            : this(level.ToValue(), message, category, thread, metadata, attributes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
        }

        public PilotLogEntry(PilotLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata, Dictionary<string, object> attributes, long clientTimestampMs)
            : this(level.ToValue(), message, category, thread, metadata, attributes, clientTimestampMs)
        {
        }

        public PilotLogEntry(string level, string message, string category, string thread,
            Dictionary<string, object> metadata, Dictionary<string, object> attributes, long clientTimestampMs)
        {
            Level = level;
            Message = message;
            Category = category;
            Thread = thread;
            Metadata = metadata;
            Attributes = attributes;
            ClientTimestamp = FormatTimestamp(clientTimestampMs);
        }

        public static PilotLogEntry Debug(string message) => new PilotLogEntry(PilotLogLevel.Debug, message);
        public static PilotLogEntry Info(string message) => new PilotLogEntry(PilotLogLevel.Info, message);
        public static PilotLogEntry Warning(string message) => new PilotLogEntry(PilotLogLevel.Warning, message);
        public static PilotLogEntry Error(string message) => new PilotLogEntry(PilotLogLevel.Error, message);
        public static PilotLogEntry Critical(string message) => new PilotLogEntry(PilotLogLevel.Critical, message);

        internal Dictionary<string, object> ToDict()
        {
            var dict = new Dictionary<string, object>
            {
                ["level"] = Level,
                ["message"] = Message,
                ["client_timestamp"] = ClientTimestamp
            };

            if (Category != null)
                dict["category"] = Category;

            if (Thread != null)
                dict["thread"] = Thread;

            if (Metadata != null && Metadata.Count > 0)
                dict["metadata"] = Metadata;

            if (Attributes != null && Attributes.Count > 0)
                dict["attributes"] = Attributes;

            return dict;
        }
    }
}
