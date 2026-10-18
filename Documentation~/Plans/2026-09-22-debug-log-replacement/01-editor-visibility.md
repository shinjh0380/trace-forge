# Phase 1: Editor Visibility

- Parent: `00-orchestration.md`
- Closes: G1
- Depends on: Phase 0 (branch convergence, passing test baseline)
- Working branch: `feature/native-logger` (based on `dev`)

> **For agentic workers:** Read `00-orchestration.md` first. This phase has no open decisions; it can start immediately after Phase 0.

## Goal

Install the package, press Play, and see logs in **Window > TraceForge > Log Viewer** — with no `Debug.Log` anywhere in the repository.

## Why This Is First

`UnityConsoleSink` was removed in the async file logging work, and `TraceForgeLogViewerWindow.SetRingBuffer()` has never had a caller. Today the Viewer is always empty, so a developer who stops using `Debug.Log` sees nothing. Every later phase builds on the Viewer being the primary development-time output.

## Files

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Runtime/SinkRegistry.cs` | `internal static` registry of live `RingBufferSink` instances. Exposed to the Editor assembly through the existing `InternalsVisibleTo` (add `TraceForge.Editor` to `Runtime/AssemblyInfo.cs`) |
| Modify | `Runtime/Logger.cs` | `AddSink` / `RemoveSink` / `ClearSinks` / `Reset` update the registry |
| Modify | `Runtime/AssemblyInfo.cs` | Add `[assembly: InternalsVisibleTo("TraceForge.Editor")]` |
| Modify | `Editor/TraceForgeLogViewerWindow.cs` | Re-acquire the sink from the registry in `OnEnable`, on `EditorApplication.playModeStateChanged`, and after domain reload. If several ring buffers exist, offer a dropdown |
| Modify | `Editor/TraceForgeMenuItems.cs` | Remove the `Debug.Log` call. Report the reset via `ShowNotification` on the Viewer window if open, else `EditorUtility.DisplayDialog` |
| Create | `Tests/Editor/LogViewerWindowTests.cs` | Viewer wiring tests |
| Modify | `Tests/Runtime/SinkDispatchTests.cs` | Extend the "no `Debug.Log*` in Runtime" source scan to `Editor/` |

## Design Notes

### SinkRegistry

```csharp
namespace TraceForge
{
    internal static class SinkRegistry
    {
        // Copy-on-write, same pattern as Logger._sinks
        internal static RingBufferSink[] RingBuffers { get; }
        internal static void Register(ILogSink sink);   // no-op unless RingBufferSink
        internal static void Unregister(ILogSink sink);
        internal static void Clear();
        internal static event Action Changed;            // Editor Viewer subscribes
    }
}
```

- The registry must never keep a sink alive that `Logger` no longer dispatches to. `Logger` is the single writer to both structures, under `_sinksLock`.
- `Changed` is raised outside the lock. The Viewer only uses it to schedule a refresh; it never mutates registry state.

### Viewer re-acquisition

- `OnEnable`: subscribe to `SinkRegistry.Changed` and `EditorApplication.playModeStateChanged`; call `Reacquire()`.
- `Reacquire()`: pick the previously selected sink if it is still registered; otherwise the first one; otherwise `null` (empty state with a hint: "No RingBufferSink registered. Phase 2 bootstrap registers one automatically.").
- `OnDisable`: unsubscribe.
- Keep the existing 0.25 s poll for entries; the registry event only handles sink identity.

### MenuItems

The comment on the current `Debug.Log` says it exists because `TF.Log` would be silent after `Reset()`. That reasoning is correct, so the replacement must not go through `TF` either — use an Editor UI notification instead.

## Steps

- [x] Add `SinkRegistry` and wire it from `Logger` (write the registry tests first)
- [x] Add `InternalsVisibleTo("TraceForge.Editor")`
- [x] Rewrite Viewer sink acquisition; add the dropdown for multiple sinks
- [x] Replace the `Debug.Log` in `TraceForgeMenuItems`
- [x] Extend the source scan test to `Editor/`
- [x] Run the sample scene in the test host and confirm the Viewer shows entries
- [x] Run `traceforge-api-consistency` (no public API change expected — verify)

## Tests

| Test | Expectation |
|------|-------------|
| Register a `RingBufferSink` via `TF.AddSink` | Viewer shows its entries within one refresh interval |
| Enter and exit Play Mode | Viewer shows the sink registered in the new session, not a stale reference |
| `TF.RemoveSink` / `TF.Reset` | Viewer returns to the empty state; registry is empty |
| Two ring buffers registered | Dropdown lists both; switching changes the displayed entries |
| Source scan over `Runtime/` and `Editor/` | Zero matches for `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, `Debug.LogException`, `Debug.LogFormat` |

## Completion Criteria

- Sample scene Play → entries appear in the Log Viewer.
- `grep -rn "Debug\.Log" Runtime Editor Samples~` returns nothing.
- All Phase 0 baseline tests still pass; new Editor tests pass.
- Committed on `feature/native-logger`; merge to `dev` is deferred to Session 7 per the execution request (2026-09-22).

## Validation (2026-09-22)

- Unity `6000.3.8f1`: EditMode **13/13** (baseline 5 + 8 new tests); PlayMode **53/53** (baseline 51 + source scan + host-only sample test), no skipped tests.
- Test-first runs: EditMode 5 passed / 4 failed and PlayMode 51 passed / 1 failed before implementation. Expanded selection/Play Mode tests also failed before the repair (10 passed / 2 failed).
- Primary verification: `run-tests.ps1 -Platform EditMode -Out phase1-final-editmode-verified` and `run-tests.ps1 -Platform PlayMode -Out phase1-final-playmode-no-domain-reload`. Default domain-reload PlayMode startup intermittently lost the Unity Test Framework controller script before any tests ran. One early retry passed, but the final rerun remained blocked after retries and cache reimport. The ignored host now uses `EnterPlayModeOptions.DisableDomainReload` with scene reload enabled; the final full suite passes in that configuration. Default domain-reload startup remains an environment limitation, not a passing validation claim.
- The ignored host imports `Samples~/BasicUsage` under `Assets/Phase1SampleVerification`. Its PlayMode test runs the sample in a scene, asserts `SinkRegistry.RingBuffers.Length == 1` after `Awake`, and verifies initialization and gameplay entries in the Viewer.
- `rg -n -F "Debug.Log" Runtime Editor Samples~`: zero matches. Literal matching is required: the unescaped regex also matches the existing `Debug(LogCategory...)` declaration. The source-scan test is new because the baseline had no scan to extend.
- Public API declarations and Runtime asmdef dependencies are unchanged. Phase 2 and CI workflow changes are outside this session.
- Independent review identified a reentrant `Flush()` callback retaining the outer Logger lock. A regression test failed (12 passed / 1 failed); `Reset` now snapshots and clears under the lock while invoking `Flush` outside it. The final test also observes same-thread nested registration and verifies all notifications run without the lock.
- Execution: one `astra_luna_worker` (GPT-5.6 Luna, medium; `phase1_luna`) owned the listed implementation/test files; the primary inspected and integrated the diff and ran validation. Independent `astra_review` (GPT-6 Astra, low; `phase1_astra_review`) reviewed API 23/performance 27 criteria and returned **accept** after the test-quality repair and reentrant-lock repair. Native token usage is unavailable.

## Handoff to Phase 2

Phase 2 will register a `RingBufferSink` automatically from the bootstrap. Phase 1 must leave the registry API stable (`Register` / `Unregister` / `Changed`) so the bootstrap can rely on it without touching the Viewer again.
