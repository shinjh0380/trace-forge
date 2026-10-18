# Unity logging API versus TraceForge file logging

Measured 2026-10-18. The README chart uses the timing-only cohort below, not
direct `FileSink.Write` timings and not the allocation-instrumented diagnostic.
All 20 successful processes from both cohorts are retained. No product code,
existing test, performance threshold, version, or release tag was changed.

## Environment and paths

- Windows 11 Home 10.0.26200; i7-13700HX, 16 cores / 24 logical processors,
  about 32 GB RAM; Balanced power plan.
- Unity Editor 6000.6.6f1, batchmode with `-nographics`; Console window UI is
  not rendered. This measures the Editor's native logging backend, not GUI FPS.
- Product source commit: `1ba20e8e5ff5a21a08fe575b38c34e1099d4e4e8`. The README-only changes
  do not change that Runtime/Editor code. Source hashes and frozen protocol
  precede timing in each cohort's [environment](UnityLoggingTimingComparison/environment.json).
- Unity: actual `UnityEngine.Debug.Log` with the default `DebugLogHandler`,
  logging enabled, `filterLogType=Log`, output in each process's native Editor log.
- TraceForge: actual `TF.Info` → `Logger` → **one FileSink**, queue **4,096**,
  default backpressure, no ring buffer or additional counting sink, UTF-8 file.
- Both: 1,000 warm-up calls, 100,000 identical prebuilt ASCII messages with unique
  sequence IDs, main-thread caller, no interpolation in the measured loops,
  `StackTraceLogType.None` / `StackTracePolicy.None`, Unity-to-TF capture stopped.
  These are explicit no-stack configurations, not a comparison of untouched defaults.
- TF adds timestamp/severity/category metadata; Unity uses its native Editor log
  and console backend. Output formats, native bookkeeping, and paths differ.
  Neither is a physical-disk `fsync` benchmark.

## Protocol fixed before timing

The [timing-only plan](UnityLoggingTimingComparison/plan.json) fixes ten fresh
Unity processes: Unity/TF, TF/Unity, Unity/TF, TF/Unity, Unity/TF. No successful
observation is removed. A timeout, nonzero exit, compiler diagnostic, wrong
capture/filter/sink state, or missing/duplicate/out-of-order message stops the set.

`Profiler.enabled=false`, allocation call stacks disabled, and **no allocation
recorder** during the README timing cohort. Setup, message creation, reflection,
GC preparation, and persistence verification are outside the caller interval.
The Stopwatch interval contains the real public logging loop.

Caller time ends when all public API calls return. TF's separate completion
measurement uses the same start and ends after `FileSink.Flush`; it is not added
to caller time. Unity exposes no equivalent log-flush API here, so its matching
completion time is **unavailable**, not zero or inferred from the caller loop.

After clean process exit, every message ID is independently parsed from the
actual output file: 100,000 timed messages plus 1,000 warm-up messages, exactly
once and in order. stdout/stderr are also checked for duplicate timed output.
Raw native logs stay in the ignored host evidence directory because they include
Editor/licensing startup diagnostics; their SHA256, sizes, parsed counts, and
all numeric results are published in each run's JSON.

## Timing-only raw results — README data

| Run | API / route | Caller ms | Caller + Flush ms | Timed messages persisted |
| --- | --- | ---: | ---: | ---: |
| 1 | Unity Debug.Log / native Editor log | 5885.5019 | N/A | 100,000 |
| 2 | TF.Info / Logger / single FileSink | 719.6172 | 761.1580 | 100,000 |
| 3 | TF.Info / Logger / single FileSink | 777.7053 | 810.9009 | 100,000 |
| 4 | Unity Debug.Log / native Editor log | 5842.0061 | N/A | 100,000 |
| 5 | Unity Debug.Log / native Editor log | 5832.1643 | N/A | 100,000 |
| 6 | TF.Info / Logger / single FileSink | 689.2912 | 720.7035 | 100,000 |
| 7 | TF.Info / Logger / single FileSink | 797.2534 | 830.7661 | 100,000 |
| 8 | Unity Debug.Log / native Editor log | 5887.6515 | N/A | 100,000 |
| 9 | Unity Debug.Log / native Editor log | 5955.1554 | N/A | 100,000 |
| 10 | TF.Info / Logger / single FileSink | 694.2134 | 724.0838 | 100,000 |

[All source JSON](UnityLoggingTimingComparison/results/) ·
[CSV](UnityLoggingTimingComparison/measurements.csv) ·
[Recomputed summary](UnityLoggingTimingComparison/summary.json)

| Metric | Unity Debug.Log | TraceForge file logging |
| --- | ---: | ---: |
| Caller median / 100,000 messages | 5885.5019 ms | 719.6172 ms |
| Caller range | 5832.1643–5955.1554 ms | 689.2912–797.2534 ms |
| Caller + Flush median | N/A | 761.1580 ms |
| All timed messages persisted per process | 100,000 / 100,000 | 100,000 / 100,000 |

Caller median ratio `Unity / TF` = **8.178657**.
Caller time reduction `(Unity - TF) / Unity × 100` =
**87.773053%**.
These compare caller elapsed time in this configuration only, not a universal
logging-speed, completion-speed, frame-rate, or Player/mobile improvement.

The five paired TF/Unity ratios are
`0.122269, 0.133123, 0.118188, 0.135411, 0.116574`.
The descriptive paired log-ratio t95 interval is
**0.114671–0.135990**.
It uses `exp(mean(log ratios) ± 2.776445105 × sample SD / sqrt(5))`, assumes
approximately normal log ratios and independent pairs, and has only five pairs.
It is an uncertainty description, **not a new performance acceptance gate**.

## Earlier allocation-instrumented diagnostic — retained separately

The first complete ten-process protocol included a current-thread GC.Alloc
recorder and a positive control. Its reported Count delta was 200,000 for Unity
and zero for TF, with declared recorder capacity 1,024. Keep these raw counter
values and the capacity flag; do not equate them with allocated bytes, a complete
allocation trace, or all-thread allocation totals.

Unity documents [additional recording calculations](https://docs.unity.com/en-us/engine/6000.3/script-reference/unity/profiling/profilerrecordersample)
and [allocation-callstack overhead](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/profiling/profiler/enableallocationcallstacks).
This motivated a separately frozen timing-only cohort before drawing the README.
The diagnostic cohort did not record its global Profiler/callstack state, so do
not attribute the difference between cohorts to one specific profiler setting.
The two chronological cohorts are never pooled and their differences are not
an isolated causal measurement of instrumentation overhead.

| Run | API / route | Caller ms | Caller + Flush ms | Timed messages persisted |
| --- | --- | ---: | ---: | ---: |
| 1 | Unity Debug.Log / native Editor log | 5848.2033 | N/A | 100,000 |
| 2 | TF.Info / Logger / single FileSink | 443.2247 | 457.5417 | 100,000 |
| 3 | TF.Info / Logger / single FileSink | 424.5053 | 442.4796 | 100,000 |
| 4 | Unity Debug.Log / native Editor log | 5846.4848 | N/A | 100,000 |
| 5 | Unity Debug.Log / native Editor log | 5864.6246 | N/A | 100,000 |
| 6 | TF.Info / Logger / single FileSink | 455.0658 | 474.8105 | 100,000 |
| 7 | TF.Info / Logger / single FileSink | 453.8240 | 478.5164 | 100,000 |
| 8 | Unity Debug.Log / native Editor log | 5837.3933 | N/A | 100,000 |
| 9 | Unity Debug.Log / native Editor log | 5819.2585 | N/A | 100,000 |
| 10 | TF.Info / Logger / single FileSink | 545.6352 | 573.9393 | 100,000 |

Its caller medians are Unity **5846.4848 ms**
and TF **453.8240 ms**. These are **not**
the README headline values. All observations, including the slowest TF result,
remain in [diagnostic CSV](UnityLoggingComparison/measurements.csv) and
[raw JSON](UnityLoggingComparison/results/).

An unrelated Editor opened after that diagnostic set finished. The timing-only
preflight stopped before creating a measurement process; it did not discard any
timed observation. Measurements resumed only after the other Editor was closed.

## Scope, verification, and existing policy

- Timing-only: ten processes, all messages persisted in order, compiler
  diagnostics zero. No duplicate timed messages on stdout/stderr.
- Each cohort verifies original host files unchanged and moves its own helper
  out of Assets after completion. See [host verification](UnityLoggingTimingComparison/host-verification.json).
- No product/fixture source or user settings were edited to obtain numbers.
  Product hashes are checked after every process.
- No observed workload is excluded as an outlier. Thermal state, CPU clocks,
  and AC supply are not continuously instrumented; Balanced power alone is not
  a stability guarantee. One rig and five observations per route are limited evidence.
- Default Console UI, default stack-trace settings, release Player, mobile,
  WebGL, saturated multi-threaded producers, and production frame rates are not
  measured. TF default Editor ring-buffer plus FileSink is not this single-sink configuration.
- The [earlier direct FileSink implementation regression](2026-10-18-playmode-host-repair-and-performance.md)
  remains a separate record. Its 5% producer gate is unchanged; its direct-call
  values are not used for this public API chart.
- Historical ±10% reproducibility remains **FAIL** with only the previously
  approved v0.3.0 Windows rig exception. The accepted TF guard +5.71–8.07% remains
  specific to that safety change. No automatic baseline adoption or broader
  performance approval is introduced.

## Reproduce

Use the existing 6000.6.6f1 local-package host with other Unity Editors closed.
Copy the measured tools to a fresh ignored folder first: the runner updates its
local plan timestamp and the analyzer writes results beside its own source.
This keeps the published historical records immutable. Give every attempt a new
run name; the runner refuses to overwrite prior host evidence.

```powershell
$copy = './.worktrees/readme-reproduction/TimingComparison'
if (Test-Path -LiteralPath $copy) { throw 'Choose a fresh reproduction folder.' }
New-Item -ItemType Directory -Path $copy -Force | Out-Null
Copy-Item './Documentation~/Benchmarks/UnityLoggingTimingComparison/*' `
  -Destination $copy -Recurse
& "$copy/run.ps1" `
  -RunName '.readme-logging-comparison-timing-new'

python -X utf8 "$copy/analyze.py" `
  './.worktrees/native-logger-test-host/.readme-logging-comparison-timing-new'
```

The runner creates only a temporary host Editor script and meta, records the
actual product commit and protocol/source hashes, and removes its own helper
from Assets by moving it into the evidence directory. New runs update their
protocol timestamp. The published records here remain the original 2026-10-18
observations; do not silently replace them with a later run.
