# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Breaking
- Category verbosity overrides now replace the global minimum for that category in both directions.
- Unity context overloads can make a `null` second argument ambiguous; use a typed `Exception` or `UnityEngine.Object` cast.
- `TF.IsEnabled` now returns `false` when no sinks are registered or when the matching Trace/Debug level is stripped.

### Added
- Editor and runtime bootstrap settings, automatic ring-buffer wiring, Unity log capture, context instance IDs, and policy-based stack traces.
- Migration guidance is available in [Documentation~/Migration.md](Documentation~/Migration.md).

### Changed
- The performance contract documents caller-side guards and body-level stripping without claiming general zero-allocation or throughput guarantees.

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
