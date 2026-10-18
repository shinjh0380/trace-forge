# README visual sources

English and Korean panels use identical data. These are original diagrams and
Matplotlib charts, not product screenshots. Each localized panel has a mobile
layout; the `picture` media query is supported by GitHub's Markdown renderer.
There are no external fonts, scripts, image services, or invented screenshots.

## Sources

| Image | Source |
| --- | --- |
| `hero.svg` | Brand illustration; version from [package.json](../../package.json). |
| `facts.*.svg` | 0 external runtime packages: [package metadata](../../package.json) and [Runtime asmdef](../../Runtime/TraceForge.Runtime.asmdef). Six active levels excluding Off: [Verbosity](../../Runtime/Verbosity.cs). Nine predefined categories: [Categories](../../Runtime/Categories.cs). Default queue 4,096: [settings](../../Runtime/TraceForgeSettings.cs) and [FileSink](../../Runtime/Sinks/FileSink.cs). |
| `benchmark.*.svg` | Actual Unity Debug.Log versus TF.Info → Logger → single FileSink. [Method and both complete cohorts](../Benchmarks/2026-10-18-unity-logging-comparison.md), [timing-only raw CSV](../Benchmarks/UnityLoggingTimingComparison/measurements.csv), [summary](../Benchmarks/UnityLoggingTimingComparison/summary.json). Bars are medians; dots include every one of five observations per route. |
| `pipeline.*.svg` | [TF](../../Runtime/TF.cs), [Logger](../../Runtime/Logger.cs), [Unity capture](../../Runtime/UnityLogCapture.cs), [FileSink](../../Runtime/Sinks/FileSink.cs), and [Viewer](../../Editor/TraceForgeLogViewerWindow.cs). File output is opt-in and a full queue blocks the caller. |
| `validation.*.svg` | [Release-code CI](https://github.com/shinjh0380/trace-forge/actions/runs/39112593023), [completion CI](https://github.com/shinjh0380/trace-forge/actions/runs/39113329138), [immutable release evidence](https://github.com/shinjh0380/trace-forge/blob/800fe50095c74448e12ed097842659b4324359e6/Documentation~/Releases/0.3.0.md). These dated v0.3.0 results are not live badges. |

## Public API comparison values

Windows Editor 6000.6.6f1, i7-13700HX, headless batchmode, 2026-10-18.
Five independent processes per route, 1,000 warm-up calls and 100,000 measured
prebuilt messages each. Profiler, allocation recorder, stacks, and Unity capture
are off. TF has one manually registered FileSink, queue 4,096, no ring buffer.

| Route | Caller median (ms) | Caller + Flush median (ms) |
| --- | ---: | ---: |
| Unity Debug.Log → native Editor log | 5885.5019 | N/A |
| TF.Info → Logger → FileSink | 719.6172 | 761.1580 |

Median caller time reduction is 87.773053%, rounded to **87.8%** in both charts.
The ratio of caller medians is 8.178657. This does not compare completion times:
Unity exposes no equivalent flush operation in this benchmark. Every measured
message was verified in its actual file after process exit. File formats and
native bookkeeping differ. Console UI, Player/mobile performance, default
stack policies, and the default Editor ring-buffer-plus-file configuration
are not measured.

The earlier allocation-instrumented cohort remains in the detailed report and
raw JSON; it is not pooled into the README chart. No GC observation is converted
into a byte-allocation guarantee.

## Internal regression evidence

The [previous direct FileSink implementation comparison](../Benchmarks/2026-10-18-playmode-host-repair-and-performance.md)
is retained separately, including all its raw rows and queue configuration.
Its direct-call values cannot stand in for TF/Logger API timing. The existing
producer 5% gate, historical ±10% reproduction **FAIL**, approved v0.3.0 rig
exception, and scoped guard-cost acceptance remain unchanged. No new baseline
or general performance approval is introduced by the README comparison.

## Release validation counts

The dated CI package suites contain 28 EditMode and 107 PlayMode cases per
configuration. The Windows 6000.6.6f1 host adds its own fixtures: 32 EditMode and
109 PlayMode. Fresh Git installation is a separate one-case acceptance check.
Do not combine these into a unique-test total or an identical cross-version
fixture claim. All listed release suites have zero failures and skips.
