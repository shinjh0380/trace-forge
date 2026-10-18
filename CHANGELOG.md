# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-10-18

### Breaking
- Category verbosity overrides now replace the global minimum for that category in both directions.
- Unity context overloads can make a `null` second argument ambiguous; use a typed `Exception` or `UnityEngine.Object` cast.
- `TF.IsEnabled` now returns `false` when no sinks are registered or when the matching Trace/Debug level is stripped.
- `LogEntry.ContextInstanceId` and the extended constructor's context argument now use `ulong` instead of `int`. Context IDs are session-local and must not be persisted; the original five-argument constructor is unchanged.

### Added
- Editor and runtime bootstrap settings, automatic ring-buffer wiring, Unity log capture, context instance IDs, and policy-based stack traces.
- Migration guidance is available in [Documentation~/Migration.md](Documentation~/Migration.md).

### Changed
- The performance contract documents caller-side guards and body-level stripping without claiming general zero-allocation or throughput guarantees.

### Fixed
- Nested logging from a sink on the same thread is dropped, preventing recursive dispatch while preserving delivery of the original entry and independent threads.
- Log Viewer context ping uses the supported EntityId API on Unity 6000.3 and newer, retaining the instance-ID API for earlier supported editors.
- Unity 6000.6 context logging and Viewer lookup preserve 64-bit EntityIds through supported APIs; earlier Unity versions retain negative instance-ID round trips. Minimum Unity version remains 6000.0.
- Bootstrap and default stack-policy initialization use `Debug.isDebugBuild`, avoiding deprecated development-build directives while retaining Editor/Development defaults.

## [0.2.0] - 2026-03-07

### Added
- Asynchronous, bounded `FileSink` output with flush and dispose lifecycle behavior.
- `[HideInCallstack]` annotations for the internal logging output path.

### Removed
- `UnityConsoleSink`; file and ring-buffer sinks are the output paths.

## [0.1.0] - 2026-03-07

### Added
- Core logging architecture with `TF` static facade
- `Verbosity` enum: Trace, Debug, Info, Warning, Error, Fatal, Off
- `LogCategory` readonly struct with predefined categories
- `LogEntry` readonly struct for zero-allocation log data
- `ILogSink` interface for extensible log destinations
- `UnityConsoleSink`: Routes logs to Unity Console
- `RingBufferSink`: In-memory circular buffer for log replay
- `FileSink`: Persistent file-based logging
- Category-level verbosity overrides
- Compile-time stripping via `TRACEFORGE_STRIP_TRACE`, `TRACEFORGE_STRIP_DEBUG`, `TRACEFORGE_DISABLE`
- Editor Settings Provider (Project Settings > TraceForge)
- Log Viewer EditorWindow

[Unreleased]: https://github.com/shinjh0380/trace-forge/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/shinjh0380/trace-forge/compare/3baeb05071e6291d5abd57e8182b3429f53b8cd2...v0.3.0
