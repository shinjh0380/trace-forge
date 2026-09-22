# Phase 4: Performance Contract

- Parent: `00-orchestration.md`
- Closes: G9 (deferred issue 4)
- Depends on: Phase 3 (all structural changes to `LogEntry` and `Logger.Write` are complete)
- Decisions applied: D8

Execution request (2026-09-22): work on `feature/native-logger`; do not merge to `dev` or start Phase 5. Use option 2's internal readonly probes without changing the test asmdef, and verify the actual Player strip defines locally; the CI job is deferred to Session 6. The primary runs all benchmarks, and one Luna worker handles the bounded IsEnabled/tests/documentation change, followed by independent Astra review.

Measurement boundary: this protocol calls `FileSink.Write(in entry)` directly with a prebuilt entry, so it measures sink enqueue/copy costs, not `Logger.Write` or its stack-policy read. Configurations 2–4 set the requested policy/capture state, but their direct-call results cannot establish the cost of the Logger policy branch. Record this limitation rather than attribute timing differences to code outside the measured path.

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
| Unchanged | `Tests/Runtime/TraceForge.Tests.Runtime.asmdef` | Execution selects option 2; an ignored host Editor probe verifies the actual Player defines locally |
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
  2. Post-Phase-3 `feature/native-logger` with the Phase 4 IsEnabled changes, `StackTracePolicy.None`, capture off
  3. Same checkout, `StackTracePolicy.None`, capture on (subscription active; no Unity message emitted in the timed region)
  4. Same checkout, `StackTracePolicy.ErrorAndAbove`, Info-level prebuilt entries (the direct sink call bypasses the policy check)
- Acceptance: configurations 2–4 median producer time within 5% of configuration 1; producer-thread allocations = 0 in all; file line count = 100,000.

If a regression exceeds 5%, profile `Logger.Write` first: the likely suspects are the policy read (make it a plain static int, like `_globalMinVerbosityInt`) and `LogEntry` copy cost (now larger; confirm sinks still take `in`).

## Steps

- [x] Write the `IsEnabled` tests (strip symbols, zero sinks) — the two zero-sink tests failed before implementation
- [x] Implement the `IsEnabled` changes in `TF` and `Logger`
- [x] Fix tests affected by "zero sinks → false" and record their names below
- [x] Run the 2026-07-22 baseline reproduction (configuration 1); the ±10% historical reproduction criterion failed, as recorded below
- [x] Run configurations 2–4; write the benchmark document
- [x] Rewrite README / TraceForge.md performance and stripping sections
- [x] Run `traceforge-performance-review` — code/docs accepted with the limits recorded below; historical rig validity failed

## Deferred (design separately after Phase 5)

Allocation-free formatting overloads such as `TF.Debug<T0>(string format, T0 arg0)` would remove the caller-side interpolation cost, but require `LogEntry` to carry unformatted arguments and sinks to format lazily — a structural change to every sink. Capture the idea in `DeferredIssues.md` as a new item with a link to this phase; do not implement here.

## Completion Criteria

- `IsEnabled` tests pass under both the default compilation and a compilation with strip symbols.
- Benchmark document committed; acceptance thresholds met or the regression is fixed and re-measured.
- README contains no claim that is not covered by a test or the benchmark document.
- `traceforge-performance-review` checklist passes.
- Both requested Phase 4 commits recorded on `feature/native-logger`; merge to `dev` remains deferred to Session 7.

## Handoff to Phase 5

Phase 5 wires the strip-symbol CI job and publishes the release. Leave the benchmark rig scripts (if any were written) under `.worktrees/` — they are not committed, per the existing convention.

## Execution and validation (2026-09-22)

- One native `astra_luna_worker` (`/root/phase4_luna`, requested GPT-5.6 Luna/medium) implemented the bounded IsEnabled/tests/docs change. The primary handled host verification, benchmarks, report, and the minimal out-of-worker-scope Editor test setup correction. Token usage: unavailable.
- Real red run: `phase4-red-no-sinks.xml`, **0 passed / 2 failed**, both expected false but observed true. Final normal runs after restoring the host from benchmarking: `phase4-postbench-editmode` **24/24**, `phase4-postbench-playmode` **83/83**, no skipped tests.
- Existing zero-sink failures identified before implementation: `SettingsProviderTests.ApplySavedSettings_AppliesDefaultVerbosityWhenNoSettingsAssetExists` and `ApplySavedSettings_AppliesAssetVerbosity`. Their setup now registers a ring buffer. Both Runtime fixtures already registered a test sink; no missing-sink setup was invented there.
- Existing strip-sensitive expectations updated: `VerbosityFilteringTests.DefaultMinVerbosity_IsDebug`, `Reset_RestoresDefaultMinVerbosity`, `CategoryFilteringTests.CategoryVerbosity_CanBeSetLowerThanGlobal`, and `ClearCategoryVerbosity_RestoresGlobalFilter`; the Editor default-verbosity test also respects the runtime probe.
- Actual Player defines `TRACEFORGE_STRIP_TRACE;TRACEFORGE_STRIP_DEBUG`: `phase4-stripped-editmode` **24/24**, host output **trace=True debug=True path=stripped**; filtered Runtime fixtures **19/19**. The host Editor test checks the compiled runtime probes and both IsEnabled overloads, not just the configured define string. Defines were restored; no CI workflow was created.
- [Benchmark report](../../Benchmarks/2026-09-22-native-logger-overhead.md): four configurations × five independent Unity processes, all **allocationSamples=0** and **persisted=100000**. Current producer deltas for 2/3/4 versus 1 are **-1.7682% / -16.7583% / -19.3457%**, so the conditional >5% regression profiling/repair step was not triggered. No Write/policy/copy optimization was made.
- **Performance approval remains unestablished:** the historical producer reproduction differs by **-38.6777%** and completion by **-46.8544%**, outside ±10%. Checking the execution steps above records the completed measurements, not a passing rig-validity verdict. The direct FileSink protocol cannot measure Logger policy overhead; completion medians also increased versus the current baseline.
- Original host assets, manifest, package lock, and defines were restored; the baseline worktree was removed. Successful suites/benchmarks used domain reload disabled and scene reload enabled; the original default host setting is restored at the end. Phase 3's default-domain-reload Test Framework limitation remains.
- Independent review by `/root/phase4_astra_review` (`astra_review`, requested GPT-6 Astra/low): **code/docs accept; overall performance gate not accepted**. The reviewer read all 20 benchmark XML files, CSV, test XML, source and scripts, and matched README claims sentence by sentence. No repair round was required. Runtime/tests were then committed as `61ffa6e`; only result/commit metadata was finalized in documentation afterward.
- All 27 performance checklist items were considered, not reported as an unconditional 27/27 pass: 1–2, 6–7, 10–11, 13–21 and 23–26 satisfy the checked source/test contracts; 4–5 and 9 are not applicable; 3 follows D8's caller guard; 8 permits the existing sink-failure diagnostic exception path; 12 follows D7's Editor/Development default; 22 retains the existing limitation that a custom sink calling TF recursively has no general recursion guard (Unity capture has its own guard); 27 excludes default-domain-reload runner validation.
- README audit links asynchronous formatting/I/O and queue/Flush/Dispose behavior to FileSink tests and source, filtering/guards to Runtime tests, stripping to real define compilation and counter tests, registration threading to writer-lock/COW contracts, and measured allocation/timing to the qualified benchmark report. No general speed or allocation guarantee is claimed.
