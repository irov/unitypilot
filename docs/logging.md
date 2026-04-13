# Logging & Events

Pilot streams structured logs from your app to the dashboard in real time. Logs are buffered locally and flushed in batches.

## Log configuration

```csharp
var logConfig = new PilotLogConfigBuilder()
    .SetEnabled(true)                    // default: true
    .SetLogLevel(PilotLogLevel.Info)     // minimum level to capture
    .SetBatchSize(100)                   // logs per flush (default: 100)
    .SetBufferSize(1000)                 // max buffered logs (default: 1000)
    .SetAttributes(logAttrs);            // attributes attached to every entry

var config = new PilotConfig.Builder(url, token)
    .SetLogConfig(logConfig)
    .Build();
```

## Log levels

`Debug` · `Info` · `Warning` · `Error` · `Critical` · `Exception`

## Basic logging

```csharp
PilotSDK.Log(PilotLogLevel.Info, "Player spawned");
PilotSDK.Log(PilotLogLevel.Error, "Failed to load asset");
```

With category and thread:

```csharp
PilotSDK.Log(PilotLogLevel.Warning, "Texture missing", "rendering", "GLThread");
```

With metadata (arbitrary key-value data per entry):

```csharp
var meta = new Dictionary<string, object> {
    { "asset", "player.png" },
    { "size", 1024 }
};
PilotSDK.Log(PilotLogLevel.Error, "Load failed", meta);
```

## Structured log entries

For full control, build a `PilotLogEntry`:

```csharp
PilotSDK.Log(PilotLogEntry.Error("Crash detected"));
PilotSDK.Log(PilotLogEntry.Info("Level loaded"));
```

Static factory methods: `Debug()`, `Info()`, `Warning()`, `Error()`, `Critical()`.

## Log attributes

Attributes are key-value pairs attached to **every** log entry:

```csharp
var logAttrs = new PilotLogAttributeBuilder()
    .Put("app_version", Application.version)          // static
    .PutProvider("scene", () => SceneManager.GetActiveScene().name)  // dynamic
    .PutProvider("fps", () => (1f / Time.unscaledDeltaTime).ToString("F0"));
```

## Events

Events are high-level structured messages:

```csharp
PilotSDK.Event("level_completed");
PilotSDK.Event("level_completed", new Dictionary<string, object> {
    { "level", 5 }, { "time", 42.3 }
});
PilotSDK.Event("level_completed", "gameplay", new Dictionary<string, object> {
    { "level", 5 }
});
```

## Revenue events

Track monetization:

```csharp
PilotSDK.Revenue("purchase_completed", new Dictionary<string, object> {
    { "amount", 4.99 }, { "currency", "USD" }
});
```

## Screen changes

Record scene/screen transitions:

```csharp
PilotSDK.ChangeScreen("gameplay", "Level 5");
PilotSDK.ChangeScreen("menu", "Main Menu");
```

## In-App Purchases

```csharp
// Publish product catalog
PilotSDK.SetInAppProducts(new List<Dictionary<string, object>> {
    new Dictionary<string, object> {
        { "product_id", "gems_100" }, { "price", "$0.99" }
    }
});

// Set owned products
PilotSDK.SetOwnedInAppProducts(new List<string> { "remove_ads" });

// Record purchase
PilotSDK.PurchaseInApp("txn_abc", new List<string> { "gems_100" });
```

---

**See also:** [Unity Integration](unity-integration.md) · [Widgets](widgets.md) · [Metrics](metrics.md)
