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

The settings asset is created on first open and injected into Player preloaded assets for a build; the project's original preloaded asset list is restored afterward. With no settings asset, Release builds create no default sinks. File output is opt-in in every build type. `CaptureUnityLog` routes Unity messages into the `Unity` category (Log → Debug, Warning → Warning, Error/Assert → Error, Exception → Fatal). `StackTracePolicy` controls captured TraceForge stacks; Unity supplied stacks are preserved as received. Sinks added by application code remain application-owned and must be removed and disposed by that code.

Unity capture and the asset's stack policy are applied by the Runtime bootstrap in Play Mode and Players. The existing Edit Mode bootstrap provides a ring buffer and filters only; it does not start Unity capture or apply the asset's stack policy.

### Filtering Pipeline

```
TF.Info(category, message)
  → Logger.IsEnabled(verbosity, category) [category override, otherwise global]
    → No: return (zero allocation)
    → Yes: create LogEntry (stack allocated)
           → dispatch to each ILogSink
```

### Two-Level Filtering

1. **Global**: `TF.SetMinVerbosity(Verbosity.Info)` — filters all categories
2. **Category**: `TF.SetCategoryVerbosity(Categories.Network, Verbosity.Error)` — overrides for a specific category

When an override exists, it completely replaces the global minimum for that category in either direction. Clearing the override restores the global minimum.

`StackTracePolicy.ErrorAndAbove` is the default in Editor and Development builds; Release defaults to `None`. `All` is intended for debugging because stack capture allocates. `None` avoids TraceForge stack capture; Unity's own logging may still allocate before its callback.

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

`TF.Reset()` clears registered sinks, global verbosity, category overrides, and the stack trace policy. Bootstrap-owned Unity capture is stopped during shutdown; application-owned sinks remain the caller's responsibility.
