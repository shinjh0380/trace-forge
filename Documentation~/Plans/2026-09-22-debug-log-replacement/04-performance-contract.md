# Phase 4: Performance Contract

- Parent: `00-orchestration.md`
- Closes: G9 (deferred issue 4)
- Depends on: Phase 3 (all structural changes to `LogEntry` and `Logger.Write` are complete)
- Decisions applied: D8

> **For agentic workers:** Read `00-orchestration.md` first. This phase is measurement-driven: do not change hot-path code without a before/after number from the test host. Reference baseline: `Documentation~/Benchmarks/2026-07-22-file-sink-performance.md`.

## Goal

Every performance claim in the README is backed by a test or a benchmark, and `IsEnabled()` never lies: if a call would be stripped or dropped, `IsEnabled()` returns `false`.

## What Is Currently Overstated

| Claim | Reality | Fix |
|-------|---------|-----|
| "Zero-allocation on disabled log paths" | True *inside* `TF.*`, but `TF.Trace($"...{x}")` interpolates before the call | Reword; promote the `IsEnabled` guard |
| "Compile-time stripping removes all Trace() calls" | `#if` empties the body; the call and its argument evaluation remain | Reword: "removes the method body; argument evaluation remains" |
| `IsEnabled(Verbosity.Trace)` under `TRACEFORGE_STRIP_TRACE` | Returns `true`, so guarded code builds a string that goes nowhere | Return `false` |
| `IsEnabled` with zero registered sinks | Returns `true`; `Logger.Write` then discards | Return `false` |

Why not `[Conditional]` (D8): it compiles the call only when the symbol is *defined*, which would make Trace/Debug silent by default unless every project adds a define. Unity 6 targets C# 9, so C# 10 interpolated string handlers (the mechanism that would truly defer interpolation) are unavailable. The honest contract is therefore "body-level stripping + caller-side guard".

## Files

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `Runtime/TF.cs` | `IsEnabled(Verbosity.Trace)` / `(Verbosity.Debug)` return `false` under the matching strip symbol; `TRACEFORGE_DISABLE` already returns `false` |
| Modify | `Runtime/Logger.cs` | `IsEnabled` returns `false` when `_sinks.Length == 0` (cheap length read on the already-published array) |
| Modify | `README.md` | Rewrite the Features bullets and the Compile-Time Stripping table with the precise guarantees; add the guard pattern as the recommended idiom for interpolated messages |
| Modify | `Documentation~/TraceForge.md` | Performance Notes section aligned with the README |
| Modify | `Tests/Runtime/VerbosityFilteringTests.cs` | `IsEnabled` under each strip symbol; `IsEnabled` with zero sinks |
| Modify | `Tests/Runtime/TraceForge.Tests.Runtime.asmdef` | Add `versionDefines` or a second test asmdef with `defineConstraints` so strip-symbol tests run in a compilation that actually has the symbol |
| Modify | `Tests/Runtime/FileSinkTests.cs` (allocation test) | Re-run the producer-path allocation check with `StackTracePolicy.None` and with `UnityLogCapture` active |
| Create | `Documentation~/Benchmarks/<date>-native-logger-overhead.md` | Post-Phase-3 measurement, same method as the 2026-07-22 document |
| Modify | `Documentation~/DeferredIssues.md` | Mark issue 4 resolved |

## Strip-Symbol Test Strategy

Tests that assert "`IsEnabled` is false under `TRACEFORGE_STRIP_TRACE`" need the symbol present at compile time. Two options; pick one and document it in the test asmdef:

1. **Separate test assembly** `TraceForge.Tests.Runtime.Stripped` with `defineConstraints: ["TRACEFORGE_STRIP_TRACE"]` and the symbol added to the test host's Player scripting defines in CI. Simple, but only one symbol combination per assembly.
2. **Runtime probe**: expose `internal static bool IsTraceStripped` constants from `TF` (set by the same `#if`) and assert `IsEnabled(Verbosity.Trace) == !IsTraceStripped`. Runs in any configuration and is self-consistent, but does not prove the symbol was applied.

Recommended: option 2 for the default test run plus one CI job with the symbols defined (Phase 5 sets that job up) so option 1's proof exists too.

## Benchmark Protocol (same as 2026-07-22)

- Test host: `.worktrees/native-logger-test-host`, Windows Editor PlayMode, batch mode, 5 independent Unity processes per configuration.
- 1,000-iteration warm-up, then 100,000 `Write(in entry)` calls with one pre-built `LogEntry`; queue capacity 101,024 so the producer path never saturates.
- Measure: producer time (to the last `Write` return), completion time (to `Flush()` return), producer-thread `GC.Alloc` count via `ProfilerRecorder` (`CollectOnlyOnCurrentThread`).
- Configurations:
  1. Phase 0 baseline commit (`f95dcc2`) — reproduce the 2026-07-22 numbers to validate the rig
  2. Post-Phase-3 `dev`, `StackTracePolicy.None`, capture off
  3. Post-Phase-3 `dev`, `StackTracePolicy.None`, capture on (measures subscription cost, expected ≈ 0)
  4. Post-Phase-3 `dev`, `StackTracePolicy.ErrorAndAbove`, Info-level entries (policy check cost only)
- Acceptance: configurations 2–4 median producer time within 5% of configuration 1; producer-thread allocations = 0 in all; file line count = 100,000.

If a regression exceeds 5%, profile `Logger.Write` first: the likely suspects are the policy read (make it a plain static int, like `_globalMinVerbosityInt`) and `LogEntry` copy cost (now larger; confirm sinks still take `in`).

## Steps

- [ ] Write the `IsEnabled` tests (strip symbols, zero sinks) — they fail first
- [ ] Implement the `IsEnabled` changes in `TF` and `Logger`
- [ ] Fix any tests broken by "zero sinks → false" (list them in the PR; expected in `VerbosityFilteringTests` and `CategoryFilteringTests` setups that never add a sink)
- [ ] Reproduce the 2026-07-22 baseline on the rig (configuration 1)
- [ ] Run configurations 2–4; write the benchmark document
- [ ] Rewrite README / TraceForge.md performance and stripping sections
- [ ] Run `traceforge-performance-review`

## Deferred (design separately after Phase 5)

Allocation-free formatting overloads such as `TF.Debug<T0>(string format, T0 arg0)` would remove the caller-side interpolation cost, but require `LogEntry` to carry unformatted arguments and sinks to format lazily — a structural change to every sink. Capture the idea in `DeferredIssues.md` as a new item with a link to this phase; do not implement here.

## Completion Criteria

- `IsEnabled` tests pass under both the default compilation and a compilation with strip symbols.
- Benchmark document committed; acceptance thresholds met or the regression is fixed and re-measured.
- README contains no claim that is not covered by a test or the benchmark document.
- `traceforge-performance-review` checklist passes.
- PR merged to `dev`.

## Handoff to Phase 5

Phase 5 wires the strip-symbol CI job and publishes the release. Leave the benchmark rig scripts (if any were written) under `.worktrees/` — they are not committed, per the existing convention.
