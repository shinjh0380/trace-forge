# README visual sources

The English and Korean README images use the same data. They are original SVG
illustrations and charts, not Editor screenshots. No external fonts, scripts,
image services, or generated product screenshots are used.

Each localized panel has a narrow-screen layout selected with a GitHub-supported
`picture`/`source` media query. Both layouts use identical values and scope.

## Sources

| Image | Source |
| --- | --- |
| `hero.svg` | Brand illustration; version from [package.json](../../package.json). |
| `facts.*.svg` | 0 external runtime packages: `dependencies` in [package.json](../../package.json) and `references` in [Runtime asmdef](../../Runtime/TraceForge.Runtime.asmdef). Six active levels (excluding `Off`): [Verbosity.cs](../../Runtime/Verbosity.cs). Nine predefined categories: [Categories.cs](../../Runtime/Categories.cs). Queue capacity 4,096: [TraceForgeSettings.cs](../../Runtime/TraceForgeSettings.cs) and [FileSink.cs](../../Runtime/Sinks/FileSink.cs). |
| `pipeline.*.svg` | [TF](../../Runtime/TF.cs), [Logger](../../Runtime/Logger.cs), [UnityLogCapture](../../Runtime/UnityLogCapture.cs), [FileSink](../../Runtime/Sinks/FileSink.cs), and [Viewer](../../Editor/TraceForgeLogViewerWindow.cs). File output is opt-in; a full queue blocks the producer. |
| `benchmark.*.svg` | All 20 raw runs in the [2026-10-18 same-rig report](../Benchmarks/2026-10-18-playmode-host-repair-and-performance.md). Matplotlib chart; bars are medians and dots are all five independent producer measurements. |
| `validation.*.svg` | [Release-code CI](https://github.com/shinjh0380/trace-forge/actions/runs/39112593023), [completion CI](https://github.com/shinjh0380/trace-forge/actions/runs/39113329138), and [immutable release evidence](https://github.com/shinjh0380/trace-forge/blob/800fe50095c74448e12ed097842659b4324359e6/Documentation~/Releases/0.3.0.md). Counts describe v0.3.0 validation, not a live badge. |

## Benchmark values

Time is milliseconds, converted from Stopwatch ticks at 10,000,000 ticks/second.
Each process writes 100,000 prebuilt entries directly to `FileSink.Write(in entry)`;
the benchmark queue is 101,024, not the shipping default 4,096. There is a
separate 1,000-entry warm-up. All five measurements per configuration are included.

| Configuration | Producer median (ms) | Flush completion median (ms) |
| --- | ---: | ---: |
| C1: `f95dcc2` reference | 22.7981 | 786.0451 |
| C2: candidate, None / capture off | 17.0726 | 639.9173 |
| C3: candidate, None / capture subscribed, no callbacks | 20.1773 | 739.1608 |
| C4: candidate, ErrorAndAbove / Info / capture off | 16.8669 | 667.1979 |

The right-hand completion values include producer time and `Flush`, measured
from the same start. They are not added to producer time. C2–C4 settings describe
process configuration; dispatch, policy reads, stack capture, and Unity callback
execution are outside this direct FileSink measurement. This is not a `Debug.Log`
comparison, an end-to-end TF benchmark, or a Player/mobile speed claim.

Each of 20/20 processes recorded accepted/written/persisted counts of 100,000,
with zero observed current-thread GC.Alloc samples. This sample/event proxy does
not establish zero allocated bytes. Median producer ratios and their paired
t95% upper bounds passed the existing 5% same-rig regression gate.

Historical ±10% reproducibility remains **FAIL**, with an explicitly approved
exception only for the v0.3.0 designated Windows Editor rig. The accepted earlier
TF guard overhead (+5.71–8.07%) remains specific to that safety change. These
images do not adopt a new baseline or broaden performance approval.

## Validation counts

CI package suites contain 28 EditMode and 107 PlayMode cases per configuration.
The 6000.6.6f1 Windows host contains additional fixtures: 32 EditMode and 109
PlayMode cases. Do not combine them into a unique-test total or infer that all
versions use the same fixture set. Fresh install is a separate one-case acceptance
test, not an additional full-suite run. All listed results have zero failures
and skips.
