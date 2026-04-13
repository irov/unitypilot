# Unity Integration

## Requirements

- Unity 2020.3 or later
- .NET Standard 2.1 or .NET Framework 4.x

## Installation

### Unity Package Manager (Git URL)

In Unity Editor: **Window → Package Manager → + → Add package from git URL** and enter:

```
https://github.com/irov/unitypilot.git
```

Or add directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.pilot.sdk": "https://github.com/irov/unitypilot.git"
  }
}
```

### Local package

Clone or download the repository, then add as a local package:

**Window → Package Manager → + → Add package from disk** and select the `package.json` in the `unitypilot` folder.

## Initialization

Add a script to your first scene or use `[RuntimeInitializeOnLoadMethod]`:

```csharp
using Pilot.SDK;
using UnityEngine;

public class PilotSetup : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://pilot.example.com";
    [SerializeField] private string apiToken = "plt_your_token";

    void Awake()
    {
        var config = new PilotConfig.Builder(serverUrl, apiToken)
            .SetDeviceName(SystemInfo.deviceModel)
            .SetAutoConnect(true)
            .Build();

        PilotSDK.Initialize(config);
    }
}
```

Or without MonoBehaviour:

```csharp
public static class PilotBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var config = new PilotConfig.Builder("https://pilot.example.com", "plt_token")
            .Build();

        PilotSDK.Initialize(config);
    }
}
```

## Configuration

```csharp
var sessionAttrs = new PilotSessionAttributeBuilder()
    .Put("app_version", Application.version)
    .Put("platform", Application.platform.ToString())
    .PutProvider("scene", () => SceneManager.GetActiveScene().name);

var logConfig = new PilotLogConfigBuilder()
    .SetEnabled(true)
    .SetLogLevel(PilotLogLevel.Info)
    .SetAttributes(new PilotLogAttributeBuilder()
        .Put("build", Application.version)
        .PutProvider("scene", () => SceneManager.GetActiveScene().name));

var metricConfig = new PilotMetricConfigBuilder()
    .SetEnabled(true)
    .SetSampleIntervalMs(200);

var config = new PilotConfig.Builder(url, token)
    .SetDeviceId(SystemInfo.deviceUniqueIdentifier)
    .SetDeviceName(SystemInfo.deviceModel + " (Unity " + Application.unityVersion + ")")
    .SetSessionAttributes(sessionAttrs)
    .SetLogConfig(logConfig)
    .SetMetricConfig(metricConfig)
    .SetAutoConnect(true)
    .Build();

PilotSDK.Initialize(config);
```

## Built-in Metrics

The Unity SDK automatically collects:

| Metric | Source |
|--------|--------|
| FPS | `1 / Time.unscaledDeltaTime` |
| Frame Time | `Time.unscaledDeltaTime * 1000` |
| Memory | `Profiler.GetTotalAllocatedMemoryLong()` |
| Video Memory | `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| Battery Level | `SystemInfo.batteryLevel` |
| Battery Charging | `SystemInfo.batteryStatus` |

## Thread Safety

- All public API calls (`PilotSDK.Log()`, `PilotSDK.Event()`, etc.) are thread-safe and can be called from any thread.
- Action callbacks and session listener methods are dispatched on the **Unity main thread**.
- Widget creation and UI building should ideally be done from the main thread.

## Shutdown

```csharp
void OnApplicationQuit()
{
    PilotSDK.Shutdown();
}
```

The SDK also hooks `Application.quitting` automatically via the internal `PilotRunner` MonoBehaviour.

---

**See also:** [Widgets](widgets.md) · [Logging & Events](logging.md) · [Metrics](metrics.md)
