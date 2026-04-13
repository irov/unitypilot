using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Pilot.SDK
{
    /// <summary>
    /// Built-in metric collector that automatically gathers Unity system metrics:
    /// FPS, frame time, memory, battery level/charging.
    /// All Unity API values are cached from the main thread via UpdateFromMainThread().
    /// </summary>
    internal sealed class PilotDefaultMetricCollector : IPilotMetricCollector
    {
        private float m_lastDeltaTime;
        private long m_totalAllocatedMemory;
        private long m_gpuMemory;
        private float m_batteryLevel;
        private BatteryStatus m_batteryStatus;

        public void Collect(List<PilotMetricEntry> output)
        {
            // FPS & frame time (using Time.unscaledDeltaTime cached from main thread)
            float dt = m_lastDeltaTime;
            if (dt > 0f)
            {
                output.Add(new PilotMetricEntry(PilotMetricType.FPS, 1f / dt));
                output.Add(new PilotMetricEntry(PilotMetricType.FrameTime, dt * 1000f));
            }

            // Memory (cached from main thread)
            long totalMemory = m_totalAllocatedMemory;
            output.Add(new PilotMetricEntry(PilotMetricType.Memory, totalMemory));

            // Video memory (cached from main thread)
            long gpuMemory = m_gpuMemory;
            if (gpuMemory > 0)
                output.Add(new PilotMetricEntry(PilotMetricType.VideoMemory, gpuMemory));

            // Battery (cached from main thread)
            float batteryLevel = m_batteryLevel;
            if (batteryLevel >= 0f)
            {
                output.Add(new PilotMetricEntry(PilotMetricType.BatteryLevel, batteryLevel * 100f));
                bool charging = m_batteryStatus == BatteryStatus.Charging;
                output.Add(new PilotMetricEntry(PilotMetricType.BatteryCharging, charging ? 1.0 : 0.0));
            }
        }

        internal void UpdateDeltaTime(float dt)
        {
            m_lastDeltaTime = dt;
        }

        internal void UpdateFromMainThread()
        {
            m_totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            m_gpuMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            m_batteryLevel = SystemInfo.batteryLevel;
            m_batteryStatus = SystemInfo.batteryStatus;
        }
    }
}
