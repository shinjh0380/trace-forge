# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
