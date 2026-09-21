# Phase 5: Migration Support and Release

- Parent: `00-orchestration.md`
- Closes: G10, deferred issue 6
- Depends on: Phase 4
- Resolved questions applied: #1 (canonical repository `shinjh0380/trace-forge`), #4 (CI with a Unity Personal license via GameCI)

> **For agentic workers:** Read `00-orchestration.md` first. The release steps at the end must use the project's own skills (`traceforge-release-check`, `traceforge-version-bump`, `traceforge-release-merge`); never merge to `main` by hand.

## Goal

A user can move a project from `Debug.Log` to TF with a written guide, every push to `dev` runs the test suite in CI, package metadata is consistent, and v0.3.0 is tagged from a clean `main`.

## Files

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Documentation~/Migration.md` | Debug.Log → TF mapping, category adoption, guard pattern, what capture handles automatically |
| Create (optional) | `Editor/TraceForgeMigrationScanner.cs` | `Window > TraceForge > Find Debug.Log Usages`: scans `.cs` files under `Assets/` and lists clickable results |
| Create (optional) | `Tests/Editor/MigrationScannerTests.cs` | Scanner finds seeded matches, ignores `Packages/` and comments |
| Create | `.github/workflows/test.yml` | GameCI test runner on `dev` pushes and PRs |
| Modify | `package.json` | `documentationUrl`, `changelogUrl`, `licensesUrl`, `repository.url` → `https://github.com/shinjh0380/trace-forge`; `author` reviewed; `unity` field and description agree on the minimum version; version `0.3.0` |
| Modify | `README.md` | Install URL confirmed as `shinjh0380`; Requirements section states minimum `6000.0` and tested `6000.3`; link to `Migration.md` |
| Modify | `CHANGELOG.md` | Add `0.2.0` (async FileSink, `UnityConsoleSink` removed, `[HideInCallstack]`) retroactively and `0.3.0` (this plan) with a **Breaking** entry for category override semantics |
| Modify | `Documentation~/DeferredIssues.md` | Mark issue 6 resolved; add the deferred lazy-formatting item from Phase 4 |
| Modify | `Tests/Editor/PackageMetaValidationTests.cs` | Assert the repository URL host/owner and version/changelog agreement |

## Migration Guide Content

### Mapping table

| Debug.Log call | TF equivalent | Notes |
|----------------|---------------|-------|
| `Debug.Log(msg)` | `TF.Info(msg)` | Choose a category where one applies: `TF.Info(Categories.Gameplay, msg)` |
| `Debug.Log(msg, ctx)` | `TF.Log(Categories.Default, Verbosity.Info, msg, ctx)` | Context overload exists on `Log` and on `Warning/Error/Fatal` |
| `Debug.LogWarning(msg)` | `TF.Warning(msg)` | |
| `Debug.LogWarning(msg, ctx)` | `TF.Warning(msg, ctx)` | |
| `Debug.LogError(msg)` | `TF.Error(msg)` | |
| `Debug.LogError(msg, ctx)` | `TF.Error(msg, ctx)` | |
| `Debug.LogException(ex)` | `TF.Error(ex.Message, ex)` | Exception is stored on the entry; `FileSink` writes `ex.ToString()` |
| `Debug.LogFormat(fmt, args)` | `if (TF.IsEnabled(Verbosity.Info)) TF.Info(string.Format(fmt, args))` | Guard avoids the format allocation when disabled |
| `Debug.Log($"...{x}")` | `if (TF.IsEnabled(Verbosity.Info)) TF.Info($"...{x}")` | Same reason; see Phase 4 contract |
| `Debug.Assert(cond)` | Leave as is, or `if (!cond) TF.Fatal(...)` | Assertions are out of scope; `Debug.Assert` output is captured via `LogType.Assert → Error` anyway |

### Sections

1. Why: what TF provides that the Console does not (file output, categories, verbosity, zero setup).
2. Install and verify: open the Log Viewer, press Play, confirm the sample entries.
3. Replace calls: the table above; recommended order — errors first, then warnings, then info.
4. Adopt categories: define a static `Categories`-style class per project; set per-category verbosity in Project Settings.
5. What you do not need to migrate: engine, package, and third-party `Debug.Log` output is captured automatically (`Categories.Unity`, `LogType.Log → Debug`).
6. Release builds: nothing is written unless enabled in the settings asset (D4); how to enable the file sink for QA builds.
7. Stripping: which symbols to set for release and what they do and do not remove.

## Migration Scanner (optional)

A Roslyn analyzer would be the proper tool but needs a separate DLL build pipeline and a `RoslynAnalyzer` asset label; out of scope. The editor scanner is a pragmatic substitute:

- `AssetDatabase.FindAssets("t:MonoScript")`, restrict to `Assets/`, read text, regex `\bDebug\.(Log|LogWarning|LogError|LogException|LogFormat|LogAssertion)\b`.
- Skip lines that begin with `//` and blocks within `/* */` (simple state machine; false negatives are acceptable).
- Results in a scrollable list; click → `AssetDatabase.OpenAsset(script, line)`.
- Ship only if it fits in the phase budget; the guide is the required deliverable.

## CI Workflow

Findings (2026-09-22): GameCI's `unity-test-runner@v4` supports UPM packages via `packageMode: true` on Linux runners, requires an explicit `unityVersion` (no `auto`), and **does not allow the package directory to be the repository root**. A Personal license works: activate once locally through Unity Hub, store the `.ulf` contents as `UNITY_LICENSE` plus `UNITY_EMAIL` / `UNITY_PASSWORD` secrets. Professional licenses use `UNITY_SERIAL` instead.

Since this repository's root *is* the package root, check out into a subdirectory:

```yaml
name: test
on:
  push: { branches: [dev] }
  pull_request: { branches: [dev] }
jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - unityVersion: 6000.0.<latest-LTS-patch>
            defines: ""
          - unityVersion: 6000.3.<patch-matching-local>
            defines: ""
          - unityVersion: 6000.3.<patch-matching-local>
            defines: "TRACEFORGE_STRIP_TRACE;TRACEFORGE_STRIP_DEBUG"   # Phase 4 strip-symbol job
    steps:
      - uses: actions/checkout@v4
        with: { path: package, lfs: true }
      - uses: actions/cache@v4
        with:
          path: package/Library
          key: Library-${{ matrix.unityVersion }}-${{ hashFiles('package/**/*.asmdef') }}
      - uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath: package
          packageMode: true
          unityVersion: ${{ matrix.unityVersion }}
          testMode: All
          githubToken: ${{ secrets.GITHUB_TOKEN }}
          customParameters: -scriptingDefines ${{ matrix.defines }}   # verify the exact flag against the action docs
      - uses: actions/upload-artifact@v4
        if: always()
        with: { name: test-results-${{ matrix.unityVersion }}, path: artifacts }
```

Checks before enabling:

- [ ] Confirm `unityci/editor` images exist for the chosen `6000.x` versions (GameCI publishes per patch; pick ones that exist).
- [ ] Confirm how `unity-test-runner@v4` passes scripting defines in package mode; if `customParameters` is not honored, fall back to a `csc.rsp` with `-define:` committed only in the strip-test asmdef folder, or a second test asmdef with `defineConstraints` (Phase 4 option 1).
- [ ] `main` is never built in CI — it is the distribution branch and has no tests. A separate lightweight job may validate `package.json` and `.meta` presence on `main` if desired.

## Metadata Cleanup (deferred issue 6)

| Item | Current | Target |
|------|---------|--------|
| `package.json` `repository.url` and doc URLs | `makeitliveforever/trace-forge` | `shinjh0380/trace-forge` |
| README install URL | `shinjh0380/trace-forge` | unchanged (already correct) |
| Unity version wording | "Unity 6.3 LTS" (description), "Unity 6.x", `6000.0` (`unity` field) | `unity: "6000.0"` as minimum; README says "minimum 6000.0, tested on 6000.3" |
| `package.json` version vs CHANGELOG | 0.2.0 vs latest entry 0.1.0 | 0.3.0 in both, with a 0.2.0 entry added |
| Git tag | none for 0.2.0 | tag `v0.3.0` on `main` after merge |

`PackageMetaValidationTests` gets assertions for each row so the drift cannot recur silently.

## Steps

- [ ] Write `Documentation~/Migration.md`
- [ ] (Optional) implement the migration scanner with tests
- [ ] Add the CI workflow; register the three secrets; run it on a `dev` PR until green on all matrix entries
- [ ] Metadata cleanup + `PackageMetaValidationTests` extension
- [ ] CHANGELOG 0.2.0 and 0.3.0 entries
- [ ] Update `DeferredIssues.md` (issue 6 resolved; add the lazy-formatting item)
- [ ] Run `traceforge-package-audit` and `traceforge-release-check`
- [ ] Run `traceforge-version-bump` to 0.3.0
- [ ] Run `traceforge-release-merge` (`dev → main`); tag `v0.3.0`
- [ ] Install `main` via Git URL into a fresh Unity 6000.3 project and repeat the Phase 2 completion check (zero-setup `TF.Info` → Viewer) as the final acceptance test

## Completion Criteria

- CI is green on `dev` for all matrix entries, including the strip-symbol job.
- `package.json`, README, CHANGELOG, and tag all say 0.3.0 and point at `shinjh0380/trace-forge`.
- `Migration.md` is linked from the README.
- `main` contains no `.claude/`, `CLAUDE.md`, or `Tests/`; a fresh Git-URL install passes the zero-setup check.
- `Documentation~/DeferredIssues.md` has no unresolved items from the original six.

## Sources

- GameCI activation: https://game.ci/docs/github/activation/
- GameCI test runner (package mode, inputs, secrets): https://game.ci/docs/github/test-runner/
- GameCI activate action: https://github.com/game-ci/unity-activate
