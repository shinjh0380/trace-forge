# TraceForge

A lightweight, zero-allocation logging system for Unity 6 with no third-party dependencies.

## Requirements

- Unity 6.x (6000.0 or later)
- API Compatibility Level: **.NET Standard 2.1**

## Features

- **Sink-based** — route logs to Unity Console, ring buffer, file, or your own sink
- **Zero-allocation** on disabled log paths — verbosity check before any string creation
- **Category filtering** — per-category verbosity overrides
- **Compile-time stripping** — remove Trace/Debug levels from release builds
- **Thread-safe** — lock-free hot path with copy-on-write sink array

## Installation

Add via Unity Package Manager using Git URL:
```
https://github.com/shinjh0380/trace-forge.git
```

## Quick Start

```csharp
using TraceForge;

// Initialize sinks (call once on startup)
TF.AddSink(new UnityConsoleSink());

// Log at various verbosity levels
TF.Info("Game started");
TF.Warning(Categories.Network, "Connection slow");
TF.Error("Something went wrong");

// Avoid expensive formatting when disabled
if (TF.IsEnabled(Verbosity.Trace))
    TF.Trace($"Position: {transform.position}");
```

## API Reference

### TF (Static Facade)

| Method | Description |
|--------|-------------|
| `TF.Info(string)` | Log at Info level (default category) |
| `TF.Info(LogCategory, string)` | Log at Info level with category |
| `TF.IsEnabled(Verbosity)` | Check if verbosity would be logged |
| `TF.IsEnabled(Verbosity, LogCategory)` | Check with category override |
| `TF.SetMinVerbosity(Verbosity)` | Set global minimum verbosity |
| `TF.SetCategoryVerbosity(LogCategory, Verbosity)` | Override per category |
| `TF.AddSink(ILogSink)` | Register a sink |
| `TF.RemoveSink(ILogSink)` | Unregister a sink |
| `TF.Reset()` | Reset to default state (useful in tests) |

Same pattern applies to: `Trace`, `Debug`, `Warning`, `Error`, `Fatal`.

### Predefined Categories

`Categories.Default`, `Categories.Gameplay`, `Categories.Network`, `Categories.UI`, `Categories.Audio`, `Categories.Physics`, `Categories.AI`, `Categories.Performance`

### Custom Categories

```csharp
var myCategory = new LogCategory("MySystem");
TF.Info(myCategory, "Custom category log");
```

## Sinks

### UnityConsoleSink
Routes to Unity Console. Verbosity maps to `Debug.Log` / `LogWarning` / `LogError`.

### RingBufferSink
In-memory circular buffer. Viewable via **Window > TraceForge > Log Viewer**.

```csharp
var ringBuffer = new RingBufferSink(capacity: 512);
TF.AddSink(ringBuffer);
// ... later ...
LogEntry[] recent = ringBuffer.GetEntries();
```

### FileSink
Writes to a file. Implements `IDisposable`.

```csharp
using (var fileSink = new FileSink(Application.persistentDataPath + "/game.log"))
{
    TF.AddSink(fileSink);
    // ... logs written ...
} // file flushed and closed
```

### Custom Sinks

```csharp
public class MySink : ILogSink
{
    public void Write(in LogEntry entry)
    {
        // entry.Verbosity, entry.Category, entry.Message, entry.Exception, entry.TimestampTicks
    }
    public void Flush() { }
}
```

## Compile-Time Stripping

Add to your `asmdef` or scripting define symbols:

| Symbol | Effect |
|--------|--------|
| `TRACEFORGE_STRIP_TRACE` | Removes all Trace() calls |
| `TRACEFORGE_STRIP_DEBUG` | Removes all Debug() calls |
| `TRACEFORGE_DISABLE` | Removes all logging |

## Editor Tools

- **Project Settings > TraceForge** — configure global verbosity
- **Window > TraceForge > Log Viewer** — view recent logs from RingBufferSink
- **Window > TraceForge > Reset Logger** — reset to defaults

## Contributing

1. Fork this repository.
2. Create a feature branch (`git checkout -b feat/my-feature`)
3. Commit your changes (`git commit -m 'feat: add new feature'`)
4. Push the branch to your fork (`git push origin feat/my-feature`)
5. Open a Pull Request.

For bug reports or feature requests, please open an issue in the [Issues](https://github.com/shinjh0380/trace-forge/issues) section.

## License

MIT — see [LICENSE.md](LICENSE.md)
