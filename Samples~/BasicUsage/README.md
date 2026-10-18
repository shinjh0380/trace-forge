# Basic Usage Sample

Demonstrates core TraceForge features.

## Setup

1. Import the sample via Package Manager
2. Add `TraceForgeBasicUsage` component to any GameObject
3. Press Play

The package bootstrap creates the Editor ring buffer automatically. The sample adds one extra file sink at `traceforge-sample.log` and disposes the sink when the component is destroyed.

## What it demonstrates

- Bootstrap defaults plus an additional asynchronous File sink
- All verbosity levels (Trace, Debug, Info, Warning, Error)
- Category-based logging
- Custom categories
- `IsEnabled` guard for expensive formatting
- Per-category verbosity overrides
- Exception logging
- Proper FileSink disposal
