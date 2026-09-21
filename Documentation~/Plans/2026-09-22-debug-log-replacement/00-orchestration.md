# TraceForge Debug.Log Replacement — Orchestration

- Status: Draft (awaiting user review)
- Date: 2026-09-22
- Baseline commits: `main` `3baeb05`, `dev` `de3fafc`, `feature/async-file-sink` `f95dcc2`
- Prior documents: `Designs/2026-07-22-async-file-logging-design.md`, `DeferredIssues.md`

> **For agentic workers:** This file is the map. Read it fully before opening any phase file. Each phase file is self-contained for execution but relies on the decisions (D1–D10) and the gap table (G1–G10) defined here. Execute phases strictly in order; do not start a phase until the previous phase's completion criteria are met on `dev`.

## Phase Index

| Phase | File | Goal | Gaps closed | Depends on |
|-------|------|------|-------------|------------|
| 0 | this file, section "Phase 0" | Converge branches, restore test baseline | — | — |
| 1 | `01-editor-visibility.md` | Logs visible in the Editor without Debug.Log | G1 | Phase 0 |
| 2 | `02-bootstrap-settings-lifecycle.md` | Zero-setup bootstrap, settings asset, shutdown flush | G2, G3, G7 | Phase 1 (SinkRegistry) |
| 3 | `03-debug-log-parity.md` | Unity log capture, context object, stack trace, category override semantics | G4, G5, G6, G8 | Phase 2 (Bootstrap), D1/D5/D6/D7 confirmed |
| 4 | `04-performance-contract.md` | Truthful performance guarantees, re-benchmark | G9 | Phase 3 (structural changes done) |
| 5 | `05-migration-and-release.md` | Migration guide, metadata cleanup, CI, v0.3.0 | G10, deferred issue 6 | Phase 4 |

```text
Phase 0 ─► Phase 1 ─► Phase 2 ─► Phase 3 ─► Phase 4 ─► Phase 5
              │           │
              │           └─ Phase 3 UnityLogCapture depends on Bootstrap
              └─ Phase 2 default Viewer wiring depends on SinkRegistry
```

Phases 1 and 2 are merged to `dev` as independent PRs. Phase 3 starts only after D1, D5, D6, D7 are confirmed. Phase 4 measures once, after Phase 3's structural changes are complete.

## Purpose

Make TraceForge sufficient for logging across the entire **Editor development → Development build → Release build** range without any call to `UnityEngine.Debug.Log*`. In other words, promote TraceForge from "a secondary logger next to Debug.Log" to "the default logger that replaces Debug.Log".

The async file logging work (2026-07-22) removed Debug.Log from the **output path**. This plan fills the resulting gap: everything Debug.Log used to provide for free must now be provided by TraceForge itself.

## Scope

### In scope

- Changes to this repository: `Runtime/`, `Editor/`, `Tests/`, `Documentation~/`, `Samples~/`
- Resolution of deferred issues 1–6
- A migration guide for game projects switching from Debug.Log to TF

### Out of scope

- A C/C++ native plugin backend. The "native logger" is TraceForge itself in pure C# (see Resolved Questions, #5)
- Actually replacing Debug.Log calls inside consuming game projects
- WebGL support; file rotation, compression, upload
- Re-displaying TF logs in the Unity Console window (the Log Viewer takes that role)

## Current State Diagnosis

### Code

| Area | State | Problem |
|------|-------|---------|
| `TF` / `Logger` | Mature | `Reset()` only runs at SubsystemRegistration. No shutdown hook |
| `FileSink` | Async, lossless, tested | Ownership and dispose are entirely the user's responsibility |
| `RingBufferSink` | Works | Nothing connects it to the Viewer |
| `TraceForgeLogViewerWindow` | UI only | `SetRingBuffer()` has no call path → **always empty** |
| `TraceForgeSettingsProvider` | UI only | Stores in `EditorPrefs`, never auto-applied, not propagated to Player |
| `TraceForgeMenuItems` | — | One remaining direct `Debug.Log` call (`Editor/TraceForgeMenuItems.cs:19`) |
| Category filtering | Contradicts docs | Global minimum is checked first, so a category can never open a *lower* verbosity |
| Stripping | Overstated | `#if` empties the body only; argument evaluation and interpolation allocations remain. `IsEnabled()` is unaware of stripping |

**Conclusion: a user who installs `main` today and turns off `Debug.Log` sees no logs at all in the Editor.** `UnityConsoleSink` was removed and the Log Viewer is not wired. This is the number-one problem.

### Branches

- `dev` (`de3fafc`) is an ancestor of `feature/async-file-sink` (`f95dcc2`). **The async FileSink work never landed on `dev`.**
- `feature/async-file-sink` was merged directly into `main` (`8527b36`), violating the `dev → main` one-way rule in CLAUDE.md.
- `main` has content commits absent from the `dev` lineage: `4cee0ee` (`[HideInCallstack]`, 0.2.0 bump), `ad5d5eb` / `902843f` / `5ac6ef1` / `61e5420` (README).
- `Tests/` exists in its latest form only on `feature/async-file-sink`. `main` is the distribution branch (tests removed); `dev` has an outdated copy.
- `.worktrees/async-file-sink` is a live, clean worktree of `feature/async-file-sink`; it must be removed with `git worktree remove` before the branch can be deleted.
- `main` itself has two `.meta` defects: `Runtime/Sinks/UnityConsoleSink.cs.meta` is an orphan (its source was deleted in `d98821c`) and `Editor/AssemblyInfo.cs.meta` is missing. Both are fixed on `dev` in Phase 0 and reach `main` through the 0.3.0 release merge; they are the only expected `dev`/`main` difference after Phase 0.
- Untracked Codex tooling at the root (`.agents/`, `.codex/`, `AGENTS.md`) is tracked on `dev` and excluded from `main` the same way as the Claude tooling.

Starting new work in this state means either building on a test-less base or hitting modify/delete conflicts at merge time again. Hence Phase 0 converges the branches first.

## Gap Analysis vs. Debug.Log

What developers lose when Debug.Log is removed, and what TraceForge must provide instead.

| # | Provided by Debug.Log | TF today | Replacement | Phase |
|---|----------------------|----------|-------------|-------|
| G1 | Logs immediately visible in the Editor | None (Viewer unwired) | Auto-wire Log Viewer ↔ RingBufferSink; auto-register a default Editor sink | 1 |
| G2 | Works with zero setup | `AddSink` required | Bootstrap: default sinks configured from a settings asset | 2 |
| G3 | No log loss on shutdown | No shutdown hook | `Application.quitting` → Flush; dispose bootstrap-owned sinks | 2 |
| G4 | Logs and unhandled exceptions from the engine and third-party packages | Not captured | `Application.logMessageReceivedThreaded` bridge → `Categories.Unity` | 3 |
| G5 | `context` object → click to select | None | `LogEntry.ContextInstanceId` + ping from the Viewer | 3 |
| G6 | Stack trace | None | Policy-based capture (default: Error and above in Editor/Development) | 3 |
| G7 | Editor settings match Player settings | EditorPrefs only | `TraceForgeSettings` ScriptableObject + Preloaded Assets | 2 |
| G8 | Predictable filter semantics | Category cannot override | Unify on true override | 3 |
| G9 | Honest performance claims | Docs overstate | Redefine guarantees, align `IsEnabled`, allocation regression tests | 4 |
| G10 | Migration path for existing code | None | Migration guide + (optional) residual `Debug.Log` scanner | 5 |

## Decisions (recommended; require confirmation)

| ID | Decision | Recommendation | Rationale |
|----|----------|----------------|-----------|
| D1 | Category verbosity semantics | **True override**: when a category value exists, ignore the global value | README/XML docs already describe it this way. "Global Info, Network at Trace" is the core debugging use case |
| D2 | Settings storage | `TraceForgeSettings : ScriptableObject`. Editor edits `ProjectSettings/TraceForgeSettings.asset`; injected via Preloaded Assets at build time | No forced Resources folder; reaches the Player; project-wide and VCS-friendly |
| D3 | Bootstrap timing | `[RuntimeInitializeOnLoadMethod(AfterAssembliesLoaded)]` (Runtime) + `[InitializeOnLoadMethod]` (Editor, for Edit Mode) | Runs before user `Awake`; right after the existing `Logger.Reset` (SubsystemRegistration) |
| D4 | Default Editor sink | Auto-register `RingBufferSink(1024)` in Editor and Development builds. Release builds follow settings (default: none) | Solves G1/G2. Avoids unwanted file creation in release builds |
| D5 | Unity log capture | Settings flag `CaptureUnityLog`, **on by default**. Routed to `Categories.Unity`. Exception → Fatal, Error/Assert → Error, Warning → Warning, **Log → Debug** | TF no longer writes to the Console, so no recursion risk. Getting third-party package logs into the file is the essence of the replacement. `Log → Debug` (not Info) keeps verbose third-party output out of files whose minimum is Info (user decision, 2026-09-22) |
| D6 | Context object | Add `int ContextInstanceId` to `LogEntry` (never hold the reference). Overloads only on `Log(category, verbosity, message, context)` and `Warning/Error/Fatal(…, context)` | Holding a reference in the struct creates thread-safety and lifetime problems. Overloading Trace/Debug/Info too would explode the API (guideline #14) |
| D7 | Stack trace | `StackTracePolicy { None, ErrorAndAbove, All }`. Default: `ErrorAndAbove` in Editor/Development, `None` in Release. `LogEntry.StackTrace` string (nullable) | Allocation on the error path is accepted, same precedent as `Exception.ToString()`. Hot path unaffected |
| D8 | Stripping mechanism | Keep `#if TRACEFORGE_STRIP_*`. Additionally: (a) `IsEnabled` reflects stripping symbols, (b) docs state "body removed, argument evaluation remains", (c) promote the `IsEnabled` guard to the recommended pattern | `[Conditional]` compiles only when the symbol is *present*, which inverts the default behavior. C# 10 interpolated string handlers are unavailable in Unity 6 (C# 9) |
| D9 | Sink ownership | Sinks created by the bootstrap are disposed by the bootstrap. Sinks added by the user via `AddSink` are the user's responsibility. `TF.Reset()` does not dispose (existing contract kept) | Consistent with the ownership principle in the existing design doc |
| D10 | `TF.Reset()` vs `Logger.Reset()` | `Logger.Reset` (domain reload) also cleans up bootstrap-owned sinks; `Logger` knows the bootstrap's registration list | Prevents FileSink handle leaks on repeated Play Mode entry |

## Target Structure

```text
                 ┌── TraceForgeSettings (SO, Preloaded Asset) ──┐
                 │   MinVerbosity, CategoryOverrides,             │
                 │   DefaultSinks, CaptureUnityLog, StackTrace    │
                 └───────────────┬────────────────────────────────┘
                                 │ read
Bootstrap ───────────────────────┤
 (RuntimeInitializeOnLoad /      │
  InitializeOnLoad)              ▼
   ├─ register RingBufferSink ──► SinkRegistry ◄── LogViewerWindow (Editor)
   ├─ register FileSink (if configured)
   ├─ start UnityLogCapture ──► Application.logMessageReceivedThreaded
   └─ Application.quitting ──► Flush + Dispose owned sinks

TF ─► Logger ─► ILogSink[] (unchanged)
```

## Phase 0: Branch Convergence (prerequisite for all phases)

**Goal:** Start work on top of the latest code *with* tests.

- [ ] Fast-forward `dev` to `feature/async-file-sink`: `git checkout dev && git merge --ff-only feature/async-file-sink`
- [ ] Cherry-pick the `main`-only content commits onto `dev`, oldest first: `8798c94` (Unity `.meta` files + `.gitattributes`), `902843f`, `ad5d5eb`, `4cee0ee`, `5ac6ef1`, `61e5420`. Preserve `dev`'s `Tests/`, `.claude/`, `CLAUDE.md`, and `.gitignore` on conflict
- [ ] On `dev`, delete orphaned `Runtime/Sinks/UnityConsoleSink.cs.meta` and add `Editor/AssemblyInfo.cs.meta` with a fresh GUID and the existing script-meta format. Commit `chore: fix orphan and missing script meta files`; do not change `main`
- [ ] Verify: `git diff --stat dev main -- . ':!Tests' ':!Tests.meta' ':!.claude' ':!CLAUDE.md' ':!CLAUDE.md.meta' ':!.agents' ':!.codex' ':!AGENTS.md' ':!AGENTS.md.meta' ':!.gitignore' ':!Documentation~/Plans/2026-09-22-debug-log-replacement'`. Expect only `Editor/AssemblyInfo.cs.meta` and `Runtime/Sinks/UnityConsoleSink.cs.meta`; stop and report any other divergence. Git may combine these as a rename; add `--no-renames` to display the two paths separately
- [ ] Track `.agents/`, `.codex/`, `AGENTS.md`, `AGENTS.md.meta`, `CLAUDE.md.meta`, and this plan directory on `dev`: `chore: track Codex tooling and native-logger plan on dev`. Dev-only root files that Unity imports (`CLAUDE.md`, `AGENTS.md`) carry metas with fresh GUIDs on dev; dot-prefixed folders (`.claude/`, `.agents/`, `.codex/`) do not need them
- [ ] Add `.agents/`, `.codex/`, `AGENTS.md`, `AGENTS.md.meta`, and `CLAUDE.md.meta` to the release-merge exclusions and the `CLAUDE.md` branching-strategy note: `chore: exclude Codex tooling from release merge`. Update `main`'s `.gitignore` only during the Session 7 release merge
- [ ] Remove the clean worktree with `git worktree remove .worktrees/async-file-sink` (no force), run `git worktree prune`, then `git branch -d feature/async-file-sink`. Keep `origin/feature/async-file-sink`; delete the empty `.worktrees/implement-v0.1.0` folder
- [ ] Create the working branch `feature/native-logger` from `dev` and check it out in the root working tree (no separate worktree — the test host references the repo root as the local package)
- [ ] Recreate the Unity test host using Task 1 Steps 1–2 of the prior plan and Unity `6000.3.8f1`, inside the repository at `C:/Projects/Other/trace-forge/.worktrees/native-logger-test-host`. Set manifest dependencies to `com.skrawberries.traceforge: file:C:/Projects/Other/trace-forge` and `com.unity.test-framework: 1.6.0`, with `com.skrawberries.traceforge` in `testables`. Add host-local `run-tests.ps1 -Platform (EditMode|PlayMode) -Out <name>` that prints XML `total/passed/failed`; subsequent sessions use this script
- [ ] Run the full existing suite in EditMode and PlayMode through `run-tests.ps1`; `PackageMetaValidationTests` must pass on the first run. Stop on any failure without changing tests. Record `(EditMode N, PlayMode M, YYYY-MM-DD)` here, check all Phase 0 steps, and commit `docs: record Phase 0 baseline` on `feature/native-logger` → this is the regression baseline

**Completion criteria:** `feature/native-logger` is checked out at the repository root with a clean working tree, `dev` is its ancestor, the comparison above has only the two known meta defects, and both test platforms pass with counts recorded. No Phase 1 work, test changes, or Runtime/Editor edits beyond the listed cherry-picks and two meta fixes. No commits to `main`; the known defects reach it only through the 0.3.0 release merge in Session 7.

## Risks and Mitigations (cross-phase)

| Risk | Mitigation |
|------|------------|
| Extra `LogEntry` fields grow the struct → more memory in RingBuffer / FileSink queue | Both fields are a reference and an int, roughly +16 B. ~64 KB at queue capacity 4096. Confirm via benchmark |
| Preloaded Assets manipulation pollutes the user's project settings | Post-build step must always restore. Leave untouched if already present. Guarantee with tests |
| `Application.logMessageReceivedThreaded` fires on arbitrary threads | `Logger.Write` is already thread-safe. The stack string is built by Unity, so no extra TF-side allocation (Unity's own allocation happens regardless of capture) |
| Changing `IsEnabled` semantics (0 sinks → false) breaks existing tests | List affected tests at the start of Phase 4 and fix in one batch |
| Category override semantics change is a behavior change | 0.x stage; mark as Breaking in CHANGELOG |
| Edit Mode bootstrap recreates sinks on every editor assembly reload | `InitializeOnLoad` checks the registry and creates only when absent |

## Resolved Questions (user decisions, 2026-09-22)

| # | Question | Resolution | Applied in |
|---|----------|------------|------------|
| 1 | Canonical repository URL | **`https://github.com/shinjh0380/trace-forge`**. `package.json` (`makeitliveforever`) is wrong and must be corrected | Phase 5 |
| 2 | Default sink in release builds | **D4 as recommended**: none. Release builds log only what the settings asset enables | Phase 2 |
| 3 | `LogType.Log` mapping in Unity log capture | **Lowered to `Debug`** (see D5) | Phase 3 |
| 4 | CI environment | **Feasible with a Unity Personal license.** GameCI `unity-test-runner@v4` activates from a `.ulf` stored as a GitHub secret and supports `packageMode` for UPM packages on Linux runners. Constraint: the package must not be the repository root, so the workflow checks out into a subdirectory. Details in Phase 5 | Phase 5 |
| 5 | "Native" backend | **Pure C#.** "Native logger" means TraceForge itself, written in C#, with no C/C++ plugin and no Unity `Debug.Log` dependency. No P/Invoke layer is planned. `LogEntry` still avoids holding `UnityEngine.Object` references (D6), which keeps a future unmanaged sink possible but is not a goal | All |

## Project Conventions to Respect

- All development on `dev`; merges via the `traceforge-release-merge` skill only.
- Naming: `Sink` (not Appender/Target), `Category` (not Tag/Channel), `Verbosity` (not Level/Severity). No `TraceForge` prefix on public type names.
- Run `traceforge-api-consistency` after any public API change, `traceforge-performance-review` after any core change.
- No external package dependencies in the Runtime asmdef.
