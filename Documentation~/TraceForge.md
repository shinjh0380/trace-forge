# TraceForge Documentation

## Architecture

TraceForge uses a **sink-based** architecture:

```
TF (facade) → Logger (internal) → ILogSink[] → UnityConsoleSink
                                              → RingBufferSink
                                              → FileSink
                                              → Your custom sink
```

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
  - `UnityConsoleSink`: ThreadStatic re-entry guard
  - `RingBufferSink`: lock-based
  - `FileSink`: lock-based

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
