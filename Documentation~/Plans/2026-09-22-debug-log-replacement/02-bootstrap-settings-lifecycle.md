# Phase 2: Bootstrap, Settings, and Lifecycle

- Parent: `00-orchestration.md`
- Closes: G2, G3, G7 (deferred issues 3 and 5)
- Depends on: Phase 1 (`SinkRegistry`)
- Decisions applied: D2, D3, D4, D9, D10 (resolved question #2: no default file sink in release builds)

> **For agentic workers:** Read `00-orchestration.md` first. Phase 1 is committed on `feature/native-logger`. Per the execution request, continue on that branch and defer the merge to `dev` until Session 7.

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

- [x] Add `StackTracePolicy` enum and `TraceForgeSettings` (tests: default values, serialization round-trip)
- [x] Implement `Bootstrap` with owned-sink tracking; hook `Application.quitting`
- [x] Hook `Bootstrap.Shutdown()` into `Logger.Reset()`
- [x] Rewrite `TraceForgeSettingsProvider` against the asset; remove all `EditorPrefs` usage
- [x] Implement the build processor with restore-on-post-build
- [x] Implement the Editor bootstrap
- [x] Update sample and docs
- [x] Run `traceforge-api-consistency` (new public types: `TraceForgeSettings`, `StackTracePolicy`)
- [x] Run `traceforge-performance-review` (bootstrap must not touch the hot path)

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
- Committed on `feature/native-logger`; merge to `dev` is deferred to Session 7 per the execution request (2026-09-22).

## Validation (2026-09-22)

- Unity `6000.3.8f1`, primary runs: `run-tests.ps1 -Platform EditMode -Out phase2-primary-editmode-final` **21/21**, and `-Platform PlayMode -Out phase2-primary-playmode-final` **61/61**, with no skipped tests. Compared with Phase 1, EditMode adds seven package tests and one host test; PlayMode adds eight bootstrap tests.
- The ignored host test enters/exits Play Mode three times, checks one ring buffer each time, verifies the last file entry, and reopens the file with exclusive write access between sessions. The host retains Phase 1's domain-reload-disabled configuration with scene reload enabled; default domain-reload Test Framework startup is not claimed as validated.
- Persisted build recovery initially failed with a nested ScriptableObject state object. JSON plus stable `GlobalObjectId` references now restores the exact ordered list, including nulls and duplicates, without a postprocess callback. The idle-update test waits for the asynchronous callback rather than assuming one test-runner yield is an Editor update.
- `LoggerConfig` had no consumers in Runtime, Editor, tests, samples, or user documentation and was removed as authorized. `Logger.Write` and both `IsEnabled` overloads are unchanged. The `ILogSink.Flush` XML contract now distinguishes automatic bootstrap cleanup from user ownership.
- `rg -n "EditorPrefs" Editor` and `rg -n -F "Debug.Log" Runtime Editor Samples~`: zero matches. No Runtime assembly references or CI workflows changed.
- Windows x64 Mono Development build: `Unity.exe -batchmode -quit -executeMethod Phase2BuildVerification.BuildWindows` with the local host and explicit log file. `phase2-build-final.log` records exit **0**; the build report is **Succeeded, Errors=0, PreloadedRestored=True, BeforeCount=0, AfterCount=0**. A temporary imported copy carries the ProjectSettings configuration into the Player and is deleted after restoration. The first build succeeded but the host-only cleanup helper tried to restore an empty scene list; the helper was corrected and the complete invocation rerun successfully.
- The ignored sample scene runs the copied Basic Usage sample and quits five seconds after startup. Player invocation `TraceForgePhase2.exe -batchmode -nographics -logFile ...` exits **0**; the configured ring capacity of 37 is verified in `Awake`. `Application.persistentDataPath/traceforge.log` ends with **PHASE2_LAST_ENTRY_20260922**, confirming the configured file sink receives and flushes the last entry on quit. Build, scene, helper scripts, XML, and logs stay under the ignored host; Player logs use its dedicated persistent-data directory.

## Handoff to Phase 3

Execution record: two concurrent `astra_luna_worker` agents (GPT-5.6 Luna, medium; `phase2_runtime` and `phase2_editor`) owned the Runtime and Editor/sample/documentation contracts respectively. The primary inspected and repaired integration details, ran the full suites and real build/Player verification, and clarified the `ILogSink` ownership comment. Independent `astra_review` (GPT-6 Astra, low; `phase2_astra_review`) returned **accept**, checking the actual XML/build/Player evidence and API 23/performance 27 criteria. The explicitly specified `TraceForgeSettings` name is an approved naming exception; unchanged or inapplicable checklist items are not claimed as new coverage. Native token usage is unavailable. Final staging also removed trailing whitespace from new Unity metas without changing GUIDs.

Phase 3 adds `UnityLogCapture` started from `Bootstrap.Initialize()` and stopped from `Shutdown()`, and reads `CaptureUnityLog` / `StackTracePolicy` from the settings asset. Leave a clearly marked extension point in `Bootstrap` for both.
