# Phase 3: Debug.Log Feature Parity

- Parent: `00-orchestration.md`
- Closes: G4, G5, G6, G8 (deferred issue 2)
- Depends on: Phase 2 (`Bootstrap`, `TraceForgeSettings.CaptureUnityLog`, `StackTracePolicy`)
- Decisions applied: D1, D5 (with `Log → Debug`), D6, D7

> **For agentic workers:** Read `00-orchestration.md` first. All decisions for this phase are confirmed. This phase changes `LogEntry` and public `TF` overloads; run `traceforge-api-consistency` before opening the PR.

## Goal

Bring into the TF pipeline the three things only `Debug.Log` used to provide — engine and third-party logs, the context object, and stack traces — and make category filtering behave the way the documentation already promises.

## Files

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Runtime/UnityLogCapture.cs` | Subscribes to `Application.logMessageReceivedThreaded`; maps `LogType` → `Verbosity`; forwards to `Logger.Write` with the Unity-provided stack string. Started/stopped by `Bootstrap` |
| Modify | `Runtime/Categories.cs` | Add `Categories.Unity` |
| Modify | `Runtime/LogEntry.cs` | Add `int ContextInstanceId` and `string StackTrace`. Keep the existing constructor; add an extended one |
| Modify | `Runtime/TF.cs` | Add `Log(category, verbosity, message, UnityEngine.Object context)` and `Warning / Error / Fatal (…, context)` overloads (D6) |
| Modify | `Runtime/Logger.cs` | (a) `IsEnabled(verbosity, category)` becomes a true override (D1); (b) capture `Environment.StackTrace` according to `StackTracePolicy` (D7); (c) accept an externally supplied stack string (from `UnityLogCapture`) without re-capturing |
| Modify | `Runtime/Bootstrap.cs` | Start `UnityLogCapture` when `settings.CaptureUnityLog`; stop in `Shutdown()`. Pass `StackTracePolicy` to `Logger` |
| Modify | `Runtime/Sinks/FileSink.cs` | Write `StackTrace` indented under the entry when present. Do not write the context id |
| Modify | `Editor/TraceForgeLogViewerWindow.cs` | Click on an entry → `EditorUtility.InstanceIDToObject` → `EditorGUIUtility.PingObject`. Foldout for the stack trace |
| Modify | `Documentation~/TraceForge.md`, `README.md` | Document capture, context, stack policy, and the new filter semantics |
| Create | `Tests/Runtime/UnityLogCaptureTests.cs` | Mapping, routing, threading |
| Modify | `Tests/Runtime/CategoryFilteringTests.cs` | Override in both directions |
| Modify | `Tests/Runtime/LogEntryTests.cs` | New fields, backward-compatible constructor |
| Create | `Tests/Runtime/StackTraceTests.cs` | Policy behavior and allocation |
| Modify | `Tests/Runtime/FileSinkTests.cs` | Stack trace formatting |

## Design Notes

### UnityLogCapture (G4)

```csharp
internal static class UnityLogCapture
{
    private static readonly LogCategory Category = Categories.Unity;
    internal static void Start()  => Application.logMessageReceivedThreaded += OnMessage;
    internal static void Stop()   => Application.logMessageReceivedThreaded -= OnMessage;

    private static void OnMessage(string condition, string stackTrace, LogType type)
    {
        var verbosity = type switch
        {
            LogType.Exception => Verbosity.Fatal,
            LogType.Error     => Verbosity.Error,
            LogType.Assert    => Verbosity.Error,
            LogType.Warning   => Verbosity.Warning,
            _                 => Verbosity.Debug   // LogType.Log — lowered per resolved question #3
        };
        Logger.WriteCaptured(verbosity, in Category, condition, stackTrace);
    }
}
```

- `logMessageReceivedThreaded` fires on whichever thread called `Debug.Log`. `Logger.Write` is already thread-safe; sinks must remain so.
- Recursion: TF never calls `Debug.Log*`, so a captured message cannot re-enter the capture. Add a `[ThreadStatic]` re-entry guard anyway, as cheap insurance against a user sink that does call `Debug.Log`.
- `Logger.WriteCaptured` bypasses `StackTracePolicy` and stores the Unity-provided stack string as-is (no second capture).

### Context object (G5)

- `LogEntry.ContextInstanceId` is `0` when absent. `UnityEngine.Object.GetInstanceID()` is main-thread-safe in practice for live objects; overloads taking `context` document "call from the main thread" and pass `context != null ? context.GetInstanceID() : 0`.
- The struct never holds the reference, so a destroyed object is harmless; the Viewer handles `InstanceIDToObject` returning `null`.
- Overload set is deliberately limited (D6): `Log(category, verbosity, message, context)`, `Warning(message, context)`, `Warning(category, message, context)`, and the same for `Error` and `Fatal`. `Error(message, exception, context)` variants follow the "exception before context" order so `context` stays the last parameter (guideline #12 and #19).

### Stack trace (G6)

- `Logger.Write` consults a static `StackTracePolicy` (set by `Bootstrap` from settings; default `ErrorAndAbove` in Editor/Development, `None` otherwise).
- Capture uses `Environment.StackTrace` (allocates). This is acceptable on the error path, matching the `Exception.ToString()` precedent. `All` is documented as a debugging-only setting.
- `[HideInCallstack]` already marks `TF` and `Logger.Write`; for our own capture, trim the leading frames belonging to `TraceForge.*` so the first frame is the caller.

### Category override (G8, D1)

```csharp
internal static bool IsEnabled(Verbosity verbosity, in LogCategory category)
{
    var dict = _categoryVerbosities;
    if (dict.Count > 0 && dict.TryGetValue(category.Name ?? string.Empty, out var categoryMin))
        return (int)verbosity >= (int)categoryMin;      // category wins, in both directions
    return (int)verbosity >= _globalMinVerbosityInt;
}
```

`IsEnabled(Verbosity)` (no category) keeps using the global value only. Mark the change as **Breaking** in CHANGELOG.

## Steps

- [ ] Add `Categories.Unity`, `LogEntry` fields, extended constructor (tests first)
- [ ] Rewrite `IsEnabled(verbosity, category)` per D1; update `CategoryFilteringTests` for both directions
- [ ] Add `StackTracePolicy` handling and frame trimming in `Logger`
- [ ] Implement `UnityLogCapture` with re-entry guard; wire into `Bootstrap`
- [ ] Add `TF` context overloads
- [ ] `FileSink`: stack trace formatting
- [ ] Viewer: ping on click, stack foldout
- [ ] Docs and CHANGELOG (Breaking: category override semantics)
- [ ] Run `traceforge-api-consistency` and `traceforge-performance-review`

## Tests

| Test | Expectation |
|------|-------------|
| `Debug.LogWarning("x")` inside the test (the only place `Debug.Log*` is allowed; wrap with `LogAssert.Expect`) | Arrives at a test sink as `Categories.Unity` / `Warning` with a non-null `StackTrace` |
| `Debug.Log("x")` | Arrives as `Verbosity.Debug` (resolved question #3) |
| Thrown, unhandled exception in a coroutine (`LogAssert.Expect(LogType.Exception, …)`) | Arrives as `Fatal` |
| `Debug.Log` from a worker thread | Arrives exactly once; no exception; sink `Write` observed off the main thread |
| Source scan | Still zero `Debug.Log*` in `Runtime/` and `Editor/`; tests are excluded from the scan |
| Global `Info`, category `Trace` | Trace passes for that category |
| Global `Trace`, category `Error` | Info is blocked for that category |
| `ClearCategoryVerbosity` | Category falls back to the global value |
| `StackTracePolicy.None` | `Write()` per-call allocation is 0 B on the producer thread (extend the existing allocation regression test) |
| `StackTracePolicy.ErrorAndAbove` | Info has `StackTrace == null`; Error has a stack whose first frame is the test method, not `TraceForge.*` |
| Context overload with a live object | `ContextInstanceId == obj.GetInstanceID()` |
| Context object destroyed after logging | Reading the entry is safe; Viewer ping is a no-op |
| `FileSink` with stack trace | Stack lines are indented under the entry; entries without a stack are unchanged byte-for-byte versus Phase 2 output |

## Completion Criteria

- An exception thrown by a third-party package appears in `traceforge.log` with its stack trace.
- Clicking an entry in the Viewer selects the context object.
- `CategoryFilteringTests` cover override in both directions and pass.
- Allocation regression test still reports 0 B on the non-error path.
- Deferred issue 2 marked resolved in `Documentation~/DeferredIssues.md`.
- PR merged to `dev`.

## Handoff to Phase 4

Phase 4 re-measures the producer path after this phase's structural changes (`LogEntry` growth, policy check in `Write`, capture subscription). Do not tune performance here; record any suspected regression in the PR description so Phase 4 can target it.
