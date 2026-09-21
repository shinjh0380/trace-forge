# Phase 2: Bootstrap, Settings, and Lifecycle

- Parent: `00-orchestration.md`
- Closes: G2, G3, G7 (deferred issues 3 and 5)
- Depends on: Phase 1 (`SinkRegistry`)
- Decisions applied: D2, D3, D4, D9, D10 (resolved question #2: no default file sink in release builds)

> **For agentic workers:** Read `00-orchestration.md` first. All decisions for this phase are confirmed; it can start as soon as Phase 1 is merged to `dev`.

## Goal

A project that installs the package and writes `TF.Info("hello")` with **no setup code** gets the message in the Log Viewer (Editor / Development) and, when configured, in a file — and nothing is lost when the application quits.

## Files

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Runtime/TraceForgeSettings.cs` | `ScriptableObject` holding all configuration (see schema below) |
| Create | `Runtime/Bootstrap.cs` | Loads settings, creates and registers default sinks, subscribes to `Application.quitting`, tracks owned sinks |
| Modify | `Runtime/Logger.cs` | `Reset()` (SubsystemRegistration) flushes and disposes bootstrap-owned sinks before clearing (D10) |
| Modify | `Runtime/LoggerConfig.cs` | Delete if unused; otherwise redefine as the runtime snapshot of `TraceForgeSettings` |
| Modify | `Editor/TraceForgeSettingsProvider.cs` | Drop `EditorPrefs`. Edit `ProjectSettings/TraceForgeSettings.asset` through a `SerializedObject`; apply to `Logger` on change |
| Create | `Editor/TraceForgeBuildProcessor.cs` | `IPreprocessBuildWithReport` adds the settings asset to Preloaded Assets; `IPostprocessBuildWithReport` restores the previous list |
| Create | `Editor/TraceForgeEditorBootstrap.cs` | `[InitializeOnLoadMethod]`: register a `RingBufferSink` in Edit Mode if none is registered, so editor scripts can use `TF` |
| Modify | `Samples~/BasicUsage/TraceForgeBasicUsage.cs` | Reframe as "bootstrap defaults + how to add an extra sink"; remove the manual ring buffer registration |
| Modify | `Samples~/BasicUsage/README.md` | Match the new sample |
| Modify | `README.md`, `Documentation~/TraceForge.md` | Document zero-setup behavior, settings asset, ownership rules |
| Create | `Tests/Runtime/BootstrapTests.cs` | Default and configured bootstrap behavior, quitting, Play Mode repeats |
| Create | `Tests/Editor/SettingsAssetTests.cs` | Settings provider reads/writes the asset and applies to `Logger` |
| Create | `Tests/Editor/BuildProcessorTests.cs` | Preloaded Assets add/restore |

## Settings Schema

```csharp
namespace TraceForge
{
    public sealed class TraceForgeSettings : ScriptableObject
    {
        public Verbosity MinVerbosity = Verbosity.Debug;
        public CategoryOverride[] CategoryOverrides = Array.Empty<CategoryOverride>();

        public bool EnableRingBuffer = true;          // ignored in release unless explicitly true
        public int RingBufferCapacity = 1024;

        public bool EnableFileSink = false;           // off by default in all build types (D4)
        public string FilePath = "traceforge.log";    // relative to Application.persistentDataPath
        public bool AppendToFile = true;
        public int FileQueueCapacity = 4096;

        public bool CaptureUnityLog = true;           // consumed in Phase 3
        public StackTracePolicy StackTracePolicy = StackTracePolicy.ErrorAndAbove; // Phase 3

        [Serializable] public struct CategoryOverride { public string Category; public Verbosity Verbosity; }
    }
}
```

- `CaptureUnityLog` and `StackTracePolicy` are declared now so the asset schema does not change again in Phase 3, but Phase 2 does not act on them. Add the `StackTracePolicy` enum in this phase (`None`, `ErrorAndAbove`, `All`).
- Location: `ProjectSettings/TraceForgeSettings.asset`. The provider creates it on first open. Assets under `ProjectSettings/` are not included in builds, which is why the build processor injects it via Preloaded Assets.

## Bootstrap Behavior

```text
[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]
Bootstrap.Initialize():
  settings = first TraceForgeSettings among preloaded assets (Player)
             or the ProjectSettings asset (Editor)
             or built-in defaults
  Logger.SetMinVerbosity(settings.MinVerbosity)
  apply CategoryOverrides
  if (Editor || Development || settings.EnableRingBuffer) register RingBufferSink   ← owned
  if (settings.EnableFileSink) register FileSink(persistentDataPath/FilePath)      ← owned
  Application.quitting += Shutdown

Bootstrap.Shutdown():
  for each owned sink: TF.RemoveSink; Flush; Dispose if IDisposable
  clear owned list

Logger.Reset() (SubsystemRegistration, domain reload):
  Bootstrap.Shutdown() first, then existing reset logic
```

Ordering note: `SubsystemRegistration` runs before `AfterAssembliesLoaded`, so on every Play Mode entry the sequence is `Logger.Reset` (cleanup) → `Bootstrap.Initialize` (fresh sinks). This is what prevents FileSink handle leaks.

Ownership (D9): the bootstrap disposes only what it created. User-added sinks are untouched by `Shutdown` and by `TF.Reset()`.

## Editor Bootstrap

`[InitializeOnLoadMethod]` in the Editor assembly registers a `RingBufferSink` when `SinkRegistry.RingBuffers` is empty and the editor is not in Play Mode. It is owned by the Editor bootstrap and released on `AssemblyReloadEvents.beforeAssemblyReload`. This makes `TF` usable from editor tooling and keeps the Viewer non-empty in Edit Mode.

## Steps

- [ ] Add `StackTracePolicy` enum and `TraceForgeSettings` (tests: default values, serialization round-trip)
- [ ] Implement `Bootstrap` with owned-sink tracking; hook `Application.quitting`
- [ ] Hook `Bootstrap.Shutdown()` into `Logger.Reset()`
- [ ] Rewrite `TraceForgeSettingsProvider` against the asset; remove all `EditorPrefs` usage
- [ ] Implement the build processor with restore-on-post-build
- [ ] Implement the Editor bootstrap
- [ ] Update sample and docs
- [ ] Run `traceforge-api-consistency` (new public types: `TraceForgeSettings`, `StackTracePolicy`)
- [ ] Run `traceforge-performance-review` (bootstrap must not touch the hot path)

## Tests

| Test | Expectation |
|------|-------------|
| No settings asset present | Default sinks match D4: ring buffer in Editor/Development, nothing else |
| Settings asset present | `MinVerbosity`, category overrides, and sink configuration are applied before any user `Awake` |
| Simulated `Application.quitting` | FileSink is flushed and disposed; the last logged entry is present in the file |
| Enter Play Mode three times in a row | No FileSink handle leak (the file can be reopened for write between runs); exactly one ring buffer registered each time |
| `TF.Reset()` | Bootstrap-owned sinks are removed from `Logger` but user-added sinks that were removed are not disposed by TF |
| Build preprocess then postprocess | Settings asset is in Preloaded Assets during the build and the list is restored afterwards; an asset already present is left untouched |
| Settings provider edit | Changing `MinVerbosity` in the UI updates the asset and `Logger.GetMinVerbosity()` immediately |

## Completion Criteria

- Fresh project + package install → `TF.Info()` reaches the Viewer with zero user code; with `EnableFileSink` on, it reaches the file and survives quit.
- `Editor/TraceForgeSettingsProvider.cs` contains no `EditorPrefs` reference.
- Deferred issues 3 and 5 marked resolved in `Documentation~/DeferredIssues.md` (with commit references).
- PR merged to `dev`.

## Handoff to Phase 3

Phase 3 adds `UnityLogCapture` started from `Bootstrap.Initialize()` and stopped from `Shutdown()`, and reads `CaptureUnityLog` / `StackTracePolicy` from the settings asset. Leave a clearly marked extension point in `Bootstrap` for both.
