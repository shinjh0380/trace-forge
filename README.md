# TraceForge

Lightweight, zero-allocation logging for Unity 6.3 LTS.

## Features

- **Sink-based** — route logs to an asynchronous file, ring buffer, or your own sink
- **Asynchronous file output** — formatting and file I/O run on a dedicated background thread
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
using System.IO;
using TraceForge;
using UnityEngine;

// Initialize sinks (call once on startup)
var fileSink = new FileSink(
    Path.Combine(Application.persistentDataPath, "traceforge.log"));
var ringBuffer = new RingBufferSink(capacity: 512);

TF.AddSink(fileSink);
TF.AddSink(ringBuffer);

// Log at various verbosity levels
TF.Info("Game started");
TF.Warning(Categories.Network, "Connection slow");
TF.Error("Something went wrong");

// Remove and dispose the owned file sink during shutdown.
TF.RemoveSink(fileSink);
fileSink.Dispose();
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

### RingBufferSink
In-memory circular buffer. Viewable via **Window > TraceForge > Log Viewer**.

```csharp
var ringBuffer = new RingBufferSink(capacity: 512);
TF.AddSink(ringBuffer);
// ... later ...
LogEntry[] recent = ringBuffer.GetEntries();
```

### FileSink
Writes asynchronously to a file. `Write()` enqueues each entry into a fixed-capacity bounded queue (4,096 entries by default). A dedicated worker thread formats and writes queued entries. When the queue is full, the caller blocks until capacity becomes available instead of dropping the new entry, providing lossless backpressure during normal operation.

`Flush()` waits until every entry accepted before the call has been written and then flushes the underlying writer. `Dispose()` stops accepting entries, drains the queue, joins the worker thread, and closes the file. It does not promise a physical-disk `fsync`.

`FileSink` implements `IDisposable`; the code that creates it owns it and must remove it from `TF` before disposing it during shutdown.

```csharp
var fileSink = new FileSink(logPath, append: true, queueCapacity: 4096);
TF.AddSink(fileSink);

// ... logs written ...

TF.RemoveSink(fileSink);
fileSink.Dispose();
```

The threaded `FileSink` supports Windows, macOS, Linux, Android, and iOS. It is not supported on WebGL.

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

## License

MIT — see [LICENSE.md](LICENSE.md)
