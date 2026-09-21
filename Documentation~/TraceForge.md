# TraceForge Documentation

## Architecture

TraceForge uses a **sink-based** architecture:

```
TF (facade) -> Logger (internal) -> ILogSink[] -> RingBufferSink
                                             -> FileSink queue -> writer thread -> file
                                             -> Your custom sink
```

## Bootstrap and settings

After installation, `TF.Info` works without setup code. The Editor and Development builds create a ring buffer for the Log Viewer according to the settings asset at `ProjectSettings/TraceForgeSettings.asset`. Enable the file sink in **Project Settings > TraceForge** to write under `Application.persistentDataPath`; the bootstrap flushes and disposes sinks it created when the application quits.

The settings asset is created on first open and injected into Player preloaded assets for a build; the project's original preloaded asset list is restored afterward. With no settings asset, Release builds create no default sinks. File output is opt-in in every build type. `CaptureUnityLog` and `StackTracePolicy` are reserved for the Phase 3 Unity log bridge. Sinks added by application code remain application-owned and must be removed and disposed by that code.

### Filtering Pipeline

```
TF.Info(category, message)
  → Logger.IsEnabled(verbosity, category)?
    → No: return (zero allocation)
    → Yes: create LogEntry (stack allocated)
           → dispatch to each ILogSink
```

### Two-Level Filtering

1. **Global**: `TF.SetMinVerbosity(Verbosity.Info)` — filters all categories
2. **Category**: `TF.SetCategoryVerbosity(Categories.Network, Verbosity.Error)` — overrides for a specific category

Category filter takes precedence if set.

## Thread Safety

- Sink array: lock-free reads (Interlocked.Exchange writes)
- Category verbosity: copy-on-write dictionary, volatile reference, lock on write
- Individual sinks: must be thread-safe themselves
  - `RingBufferSink`: lock-based
  - `FileSink`: bounded `Monitor` queue with a single writer thread

## Performance Notes

- `IsEnabled()` is `[AggressiveInlining]` — inlined at call site
- All log methods check `IsEnabled` before any string creation
- Use `if (TF.IsEnabled(Verbosity.Trace)) TF.Trace($"...")` for expensive interpolations
- `LogEntry` is a `readonly struct` — stack allocated
- `LogCategory` is a `readonly struct` — no heap allocation

## Verbosity Reference

| Level | Value | Use case |
|-------|-------|----------|
| Trace | 0 | Extremely detailed, usually stripped in builds |
| Debug | 1 | Development diagnostics |
| Info | 2 | Normal operational events |
| Warning | 3 | Something unexpected but handled |
| Error | 4 | Error that needs attention |
| Fatal | 5 | Unrecoverable error |
| Off | 6 | Disable a category completely |

## Testing

TraceForge is test-friendly:

```csharp
[SetUp]
public void SetUp()
{
    TF.Reset(); // clears sinks, resets verbosity and category overrides
    TF.AddSink(_testSink);
}
```

`TF.Reset()` resets everything: sinks, global verbosity, category overrides.
