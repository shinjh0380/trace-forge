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

- [ ] Add `SinkRegistry` and wire it from `Logger` (write the registry tests first)
- [ ] Add `InternalsVisibleTo("TraceForge.Editor")`
- [ ] Rewrite Viewer sink acquisition; add the dropdown for multiple sinks
- [ ] Replace the `Debug.Log` in `TraceForgeMenuItems`
- [ ] Extend the source scan test to `Editor/`
- [ ] Run the sample scene in the test host and confirm the Viewer shows entries
- [ ] Run `traceforge-api-consistency` (no public API change expected — verify)

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
- PR merged to `dev`.

## Handoff to Phase 2

Phase 2 will register a `RingBufferSink` automatically from the bootstrap. Phase 1 must leave the registry API stable (`Register` / `Unregister` / `Changed`) so the bootstrap can rely on it without touching the Viewer again.
