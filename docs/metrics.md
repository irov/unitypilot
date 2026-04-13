# Metrics

Pilot collects time-series performance metrics from your app and displays them as charts on the dashboard.

## Configuration

```csharp
var metricConfig = new PilotMetricConfigBuilder()
    .SetEnabled(true)                // default: true
    .SetSampleIntervalMs(200)        // 100–1000 ms (default: 200)
    .SetBufferSize(2000)             // max buffered samples (default: 2000)
    .SetBatchSize(200)               // samples per flush (default: 200)
    .AddCollector(myCollector);

var config = new PilotConfig.Builder(url, token)
    .SetMetricConfig(metricConfig)
    .Build();
```

## Built-in metric types

| Type | Unit | Aggregation | Description |
|------|------|-------------|-------------|
| `FPS` | fps | Gauge | Frames per second |
| `FrameTime` | ms | Gauge | Frame duration |
| `Memory` | bytes | Gauge | Total allocated memory |
| `VideoMemory` | bytes | Gauge | GPU driver allocated memory |
| `CpuUsage` | % | Gauge | CPU utilization |
| `NetworkRx` | bytes/s | Rate | Network received |
| `NetworkTx` | bytes/s | Rate | Network transmitted |
| `BatteryLevel` | % | Gauge | Battery charge |
| `BatteryCharging` | — | Gauge | Charging state |
| `DrawCalls` | — | Gauge | Render draw calls |
| `ThreadCount` | — | Gauge | Active threads |

## Unity built-in collectors

The Unity SDK automatically provides:

| Metric | Source |
|--------|--------|
| FPS | `1 / Time.unscaledDeltaTime` |
| Frame Time | `Time.unscaledDeltaTime * 1000` |
| Memory | `Profiler.GetTotalAllocatedMemoryLong()` |
| Video Memory | `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| Battery Level | `SystemInfo.batteryLevel * 100` |
| Battery Charging | `SystemInfo.batteryStatus == Charging` |

## Custom metric types

```csharp
var loadTime = PilotMetricType.Create("scene_load_time");
var loadTimeMs = PilotMetricType.Create("scene_load_time", "ms");
var errors = PilotMetricType.Create("error_count", "errors", PilotMetricAggregation.Counter);
```

### Aggregation modes

| Mode | Behaviour |
|------|-----------|
| `Gauge` | Last value in the window (FPS, memory) |
| `Counter` | Sum of values (error counts, events) |
| `Rate` | Average over the window (bytes/s) |

## Recording metrics

### Manual recording

```csharp
PilotMetrics metrics = PilotSDK.GetMetrics();

metrics.Record(PilotMetricType.FPS, 60.0);
metrics.Record(PilotMetricType.Memory, Profiler.GetTotalAllocatedMemoryLong());
metrics.Record(myCustomType, 42.0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
```

### Collectors

Collectors are called automatically on each sample interval:

```csharp
public class GameMetricCollector : IPilotMetricCollector
{
    public void Collect(List<PilotMetricEntry> output)
    {
        output.Add(new PilotMetricEntry(PilotMetricType.DrawCalls, GetDrawCalls()));
        output.Add(new PilotMetricEntry(
            PilotMetricType.Create("entities"), GetEntityCount()));
    }
}

// Add via config
new PilotMetricConfigBuilder().AddCollector(new GameMetricCollector());

// Or at runtime
PilotSDK.GetMetrics().AddCollector(new GameMetricCollector());
PilotSDK.GetMetrics().RemoveCollector(collector);
```

## Runtime adjustments

```csharp
PilotMetrics metrics = PilotSDK.GetMetrics();

metrics.SetSampleIntervalMs(500);  // slow down sampling
metrics.SetBufferSize(5000);       // increase buffer
metrics.SetBatchSize(500);         // larger batches
```

---

**See also:** [Unity Integration](unity-integration.md) · [Widgets](widgets.md) · [Logging & Events](logging.md)
