# Migrating from `Debug.Log` to TraceForge

This guide moves an existing Unity project from direct `Debug.Log` calls to
TraceForge (`TF`). The package supports Unity 6000.0 and later and is tested on
Unity 6000.3. TraceForge writes through registered sinks, applies category and
verbosity filters, and can retain entries in the Editor Log Viewer or write
them asynchronously to a file.

## 1. Why migrate

TraceForge gives a project one logging API for Editor development, Development
builds, and configured release builds. Categories let teams filter one system
without changing every other system's minimum verbosity. The ring buffer makes
recent entries available to the TraceForge Log Viewer, while `FileSink` provides
bounded asynchronous output. TraceForge does not write back to Unity's Console
window; Unity engine and third-party messages can instead be captured under
`Categories.Unity`.

## 2. Install and verify

Add the Git URL `https://github.com/shinjh0380/trace-forge.git` through Unity
Package Manager. The canonical `main` URL is the stable release channel; while
the current pre-release work is not yet merged, use the reviewed development
branch or local package checkout for these new bootstrap features. Open
**Window > TraceForge > Log Viewer**, enter Play Mode, and call
`TF.Info("TraceForge is ready")`. In the Editor and Development builds, the
bootstrap always supplies a ring buffer; settings control its capacity.
`EnableRingBuffer` controls whether release Players create one. If the entry is not visible, open
**Project Settings > TraceForge** and verify that the active minimum verbosity
permits `Info`.

## 3. Replace calls

Use `TF.Info`, `TF.Warning`, `TF.Error`, and `TF.Fatal` for the corresponding
application messages. Use `TF.Log(category, verbosity, message)` when the
verbosity or category is selected dynamically. Context overloads are available
on `Log`, `Warning`, `Error`, and `Fatal`; they store the Unity object's
instance ID without retaining the object. `TF.Error(message, exception)` stores
the exception, and the file sink writes its `ToString()` representation.

| Debug.Log call | TF equivalent | Notes |
| --- | --- | --- |
| `Debug.Log(msg)` | `TF.Info(msg)` | Choose a category where one applies: `TF.Info(Categories.Gameplay, msg)` |
| `Debug.Log(msg, ctx)` | `TF.Log(Categories.Default, Verbosity.Info, msg, ctx)` | Context overload exists on `Log` and on `Warning/Error/Fatal` |
| `Debug.LogWarning(msg)` | `TF.Warning(msg)` | |
| `Debug.LogWarning(msg, ctx)` | `TF.Warning(msg, ctx)` | |
| `Debug.LogError(msg)` | `TF.Error(msg)` | |
| `Debug.LogError(msg, ctx)` | `TF.Error(msg, ctx)` | |
| `Debug.LogException(ex)` | `TF.Error(ex.Message, ex)` | Exception is stored on the entry; `FileSink` writes `ex.ToString()` |
| `Debug.LogFormat(fmt, args)` | `if (TF.IsEnabled(Verbosity.Info)) TF.Info(string.Format(fmt, args))` | Guard avoids the format allocation when disabled |
| `Debug.Log($"...{x}")` | `if (TF.IsEnabled(Verbosity.Info)) TF.Info($"...{x}")` | Same reason; see Phase 4 contract |
| `Debug.Assert(cond)` | Leave as is, or `if (!cond) TF.Fatal(...)` | Assertions are out of scope; `Debug.Assert` output is captured via `LogType.Assert → Error` anyway |

`Debug.LogFormat` and interpolated messages should use a caller-side guard when
formatting is expensive:

```csharp
if (TF.IsEnabled(Verbosity.Info))
    TF.Info(string.Format("Loaded {0} items", count));

if (TF.IsEnabled(Verbosity.Debug, Categories.Network))
    TF.Debug(Categories.Network, $"Packet {packetId} has {bytes.Length} bytes");
```

The guard prevents message construction when the category, verbosity, sink
state, or compile-time strip setting disables the call.

## 4. Adopt categories

Start with `Categories.Default` for code that has no clear subsystem. For
project code, define a small category class or use the predefined
`Gameplay`, `Network`, `UI`, `Audio`, `Physics`, `AI`, `Performance`, and
`Unity` categories. Configure `MinVerbosity` and category overrides in
**Project Settings > TraceForge**. A category override replaces the global
minimum for that category in both directions, so a `Network` override of
`Trace` can intentionally open more detail than a global `Info` minimum.

## 5. What does not need migration

When `CaptureUnityLog` is enabled during Play Mode or in a Player, Unity engine,
package, and third-party messages received by
`Application.logMessageReceivedThreaded` are captured
under `Categories.Unity`. Unity `LogType.Log` maps to `Debug`; warnings map to
`Warning`, errors and assertions map to `Error`, and exceptions map to
`Fatal`. This lets a project migrate its own calls incrementally while still
retaining external diagnostics in configured sinks.

## 6. Release builds

Release builds create no default output sink unless the settings asset enables
one. For QA or another build that must retain a file, enable **File Sink** in
the TraceForge settings asset and choose its relative path, append behavior,
and queue capacity. The path is resolved below
`Application.persistentDataPath`. The bootstrap owns sinks it creates from
settings and flushes/disposes them at shutdown; sinks created and registered by
your code remain your responsibility.

## 7. Stripping

Set `TRACEFORGE_STRIP_TRACE` or `TRACEFORGE_STRIP_DEBUG` in the Unity build's
scripting define symbols to remove the matching `TF` method body. The call and
its arguments still follow normal C# evaluation rules, so interpolation and
side effects remain unless the caller uses `TF.IsEnabled` first. The matching
`IsEnabled` overload returns `false` when the symbol is active. Use
`TRACEFORGE_DISABLE` to disable all TraceForge logging method bodies; it also makes
`IsEnabled` return `false`. Verify the symbols in the build configuration that
compiles the package Runtime assembly.
