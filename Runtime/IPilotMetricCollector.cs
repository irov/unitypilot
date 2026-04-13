using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Interface for custom metric collectors.
    /// Collectors are called on a background thread at the configured sample interval.
    /// </summary>
    public interface IPilotMetricCollector
    {
        void Collect(List<PilotMetricEntry> output);
    }
}
