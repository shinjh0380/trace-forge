# Repository Guidelines

## Project Structure & Module Organization

TraceForge is a Unity 6 UPM logging package targeting .NET Standard 2.1, with no third-party runtime dependencies.

- `Runtime/`: the public `TF` facade, internal `Logger`, filtering, and log value types. `Runtime/Sinks/` contains file and ring-buffer sinks.
- `Editor/`: settings, menus, and the log viewer; keep UnityEditor dependencies in this assembly.
- `Samples~/BasicUsage/`: an importable usage sample.
- `Documentation~/`: architecture, designs, plans, and benchmark records.
- `package.json`: UPM metadata, compatibility, and sample registration.

Preserve Unity `.meta` files and their GUIDs when moving assets. Keep generated Unity project files outside the package.

## Build, Test, and Development Commands

Use a separate Unity 6 host project with this checkout registered as a local package. Unity compiles the package when the host opens; this repository has no standalone build script or npm commands.

- `& $unityEditor -projectPath $hostProject`: open the host project; set both variables to absolute paths first.
- Import **Basic Usage** through Package Manager and enter Play Mode to exercise logging.
- `& $unityEditor -batchmode -projectPath $hostProject -runTests -testPlatform EditMode -testResults "$hostProject/test-results.xml" -logFile "$hostProject/test-run.log"`: run configured host tests. Use `PlayMode` for runtime checks.
- `git diff --check`: check patch whitespace before submission.

## Coding Style & Naming Conventions

Follow existing C#: four-space indentation, braces on separate lines, and block-scoped namespaces. Use `PascalCase` for types and methods, `camelCase` for parameters and locals, and `_camelCase` for private fields. Match filenames to principal types. Use `TraceForge` and `TraceForge.Editor` namespaces. Preserve LF endings as specified by `.gitattributes`; no repository formatter or linter is configured.

## Testing Guidelines

Use Unity Test Framework/NUnit. Development tests follow `Tests/Runtime/` and `Tests/Editor/`, but `Tests/` is intentionally ignored and absent from the distributed checkout; provision tests in the host before running commands above. Use `*Tests.cs` and descriptive names such as `Write_SingleEntry_RetrievableViaGetEntries`.

Reset shared state with `TF.Reset()` between tests. Cover filtering, sink lifecycle, concurrency, and allocation regressions when relevant. No numeric coverage threshold is configured. Remove owned file sinks before disposing them.

## Commit & Pull Request Guidelines

Follow existing prefixes: `feat:`, `fix:`, `test:`, `perf:`, `docs:`, and `chore:`. Keep commits focused. PRs should describe behavior changes, link relevant issues, and report the Unity version and actual validation results. Include screenshots for Editor UI changes and measurements for performance claims. Update documentation, samples, and `CHANGELOG.md` when behavior changes.
