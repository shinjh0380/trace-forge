# TraceForge Documentation

## Unity compatibility

Minimum Unity version remains **6000.0**. The compatibility update targets **6000.6.6f1**; prior validation used **6000.3.8f1**. `LogEntry.ContextInstanceId` and the extended constructor's context argument are now `ulong` (Breaking); the original five-argument constructor remains available. On 6000.6+, `GetEntityId()` and `EntityId.ToULong`/`FromULong` preserve all 64 bits. Earlier versions encode an instance ID's unsigned 32-bit pattern and recover its original signed value for lookup, including negative IDs. The entry retains no Unity object reference, and missing/destroyed context is recorded as zero. An ID already stored in an entry remains unchanged when its object is destroyed. IDs are session-local, not persistent identifiers.

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
    → No: return before creating a LogEntry
    → Yes: create a LogEntry value
           → dispatch to each ILogSink
```

### Two-Level Filtering

1. **Global**: `TF.SetMinVerbosity(Verbosity.Info)` — filters all categories
2. **Category**: `TF.SetCategoryVerbosity(Categories.Network, Verbosity.Error)` — overrides for a specific category

When an override exists, it completely replaces the global minimum for that category in either direction. Clearing the override restores the global minimum.

`StackTracePolicy.ErrorAndAbove` is the default in Editor and Development builds; Release defaults to `None`. `All` is intended for debugging because stack capture allocates. `None` avoids TraceForge stack capture; Unity's own logging may still allocate before its callback.

The default policy is read once at Logger initialization from `Debug.isDebugBuild`; the bootstrap uses that runtime build flag when applying ring-buffer defaults. This preserves the Editor/Development behavior without the deprecated `DEVELOPMENT_BUILD` directive, independently of Unity 6000.6's Managed Code Variant setting. No build-flag query is added to each log write.

## Thread Safety

- Sink array: lock-free reads (Interlocked.Exchange writes)
- Category verbosity: copy-on-write dictionary, volatile reference, lock on write
- Individual sinks: must be thread-safe themselves
  - `RingBufferSink`: lock-based
  - `FileSink`: bounded `Monitor` queue with a single writer thread

While a thread is dispatching an entry, further logs on that same thread are dropped for all sinks, including captured Unity logs. The original entry continues to the remaining sinks even if a sink throws, and the guard is released in `finally`. Other threads dispatch independently. This guard covers sink `Write` callbacks and failure reporting, not `Flush` or configuration callbacks. `IsEnabled` still describes filtering/stripping/sink presence; it does not predict this reentrant drop. Caller argument evaluation and entry/stack creation occur before the dispatch guard.

## Performance Notes

- `IsEnabled()` is marked `[AggressiveInlining]`; this is an inlining hint to the compiler, not a runtime guarantee.
- `Logger` checks verbosity, category overrides, and the published sink array before creating a `LogEntry` or dispatching.
- Use `if (TF.IsEnabled(Verbosity.Trace)) TF.Trace($"...")` for expensive interpolations
- Use `if (TF.IsEnabled(Verbosity.Trace, Categories.Network))` for expensive category-specific messages.
- C# evaluates method arguments before entering `TF.Trace` or `TF.Debug`. Compile-time stripping removes the method body, but does not erase the call or its argument evaluation.
- Both `IsEnabled` overloads return `false` when no sinks are registered; the matching Trace or Debug overload also returns `false` when its strip symbol is active.
- `LogEntry` and `LogCategory` are `readonly struct` value types; their storage and any sink or queue allocations depend on the surrounding call path.
- Disabled-path and strip behavior is covered by Runtime filtering tests. Allocation and throughput claims require measurements from the Unity host benchmark described in the [Phase 4 performance contract](Plans/2026-09-22-debug-log-replacement/04-performance-contract.md) and recorded in the [benchmark record](Benchmarks/2026-09-22-native-logger-overhead.md).

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
