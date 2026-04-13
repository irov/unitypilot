# Widgets

Pilot UI is built with tabs, layouts, and widgets. Each service in your app can add its own tab on the dashboard.

## Tabs

```csharp
PilotUI ui = PilotSDK.GetUI();

PilotTab tab = ui.AddTab("Game Controls");

// Retrieve or remove tabs later
PilotTab existing = ui.GetTab("tab-1");
ui.RemoveTab("tab-1");
```

## Layouts

Every tab starts with a layout direction:

```csharp
PilotLayout root = tab.Vertical();   // top-to-bottom
PilotLayout root = tab.Horizontal(); // left-to-right
```

Nest layouts for complex arrangements:

```csharp
PilotLayout row = root.AddHorizontal();
row.AddButton("Left");
row.AddPadding(1.0);                 // flexible spacer
row.AddButton("Right");
```

### Collapsible sections

```csharp
PilotLayout section = root.AddCollapsible("Advanced");
section.AddSwitch("Verbose logging");
```

## Widget types

### Button

```csharp
root.AddButton("Restart")
    .Variant("contained")   // "contained" | "outlined" | "text"
    .Color("error")          // MUI color: "primary" | "error" | "warning" | "info" | "success"
    .Disabled(false)
    .OnClick(action => RestartGame());
```

### Label

Static or dynamic text:

```csharp
root.AddLabel("Idle")
    .Color("info")
    .TextProvider(() => game.GetStatus());  // auto-updated
```

### Stat

Numeric value with unit:

```csharp
root.AddStat("FPS")
    .Unit("fps")
    .Value("60")                            // static
    .ValueProvider(() => game.GetFps());    // or dynamic
```

### Switch

Boolean toggle:

```csharp
root.AddSwitch("God mode")
    .DefaultValue(false)
    .OnChange(action => {
        game.SetGodMode(action.Value);
    });
```

### Input

Single-line text input:

```csharp
root.AddInput("Command")
    .InputType("text")       // "text" | "number" | "password"
    .Placeholder("type a command…")
    .DefaultValue("")
    .OnSubmit(action => {
        ExecuteCommand(action.Value);
    });
```

### Select

Dropdown:

```csharp
root.AddSelect("Level")
    .Options(new[] {
        new[] { "1", "Level 1" },
        new[] { "2", "Level 2" },
        new[] { "3", "Level 3" }
    })
    .DefaultValue("1")
    .OnChange(action => {
        LoadLevel(action.Value);
    });
```

### Textarea

Multi-line text input:

```csharp
root.AddTextarea("Notes")
    .Rows(4)
    .DefaultValue("")
    .OnSubmit(action => {
        SaveNotes(action.Value);
    });
```

### Table

Data table:

```csharp
root.AddTable("Inventory")
    .Columns(new[] {
        new[] { "item", "Item" },
        new[] { "count", "Count" }
    })
    .Rows(new List<Dictionary<string, object>> {
        new Dictionary<string, object> { { "item", "Sword" }, { "count", 1 } },
        new Dictionary<string, object> { { "item", "Potion" }, { "count", 5 } }
    });
```

### Logs

Log output display:

```csharp
root.AddLogs("App Logs")
    .MaxLines(200);
```

## Dynamic values

Use value providers for automatic updates without manual polling:

```csharp
root.AddLabel("Status")
    .TextProvider(() => gameManager.Status);

root.AddStat("Players")
    .ValueProvider(() => connectedPlayers.Count.ToString());
```

Providers are evaluated on each SDK poll cycle. The SDK only sends updates to the server when values actually change.

---

**See also:** [Unity Integration](unity-integration.md) · [Logging & Events](logging.md) · [Metrics](metrics.md)
