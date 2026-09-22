# Phase 5: Migration Support and Release

- Parent: `00-orchestration.md`
- Closes: G10, deferred issue 6
- Depends on: Phase 4
- Resolved questions applied: #1 (canonical repository `shinjh0380/trace-forge`), #4 (CI with a Unity Personal license via GameCI)

Execution scope (2026-09-22): Session 6 implements pre-release work on `feature/native-logger`. Keep package version `0.2.0`, backfill its changelog, and retain new changes under `Unreleased`. Session 7 owns release-check execution, version-bump, release-merge/tag, and fresh installation; do not perform those operations here. Validate CI by pushing the feature branch, with ongoing `dev` push/PR triggers and no `main` trigger.

Confirmed corrections: the mapping table below has **10 rows**. Preserve author name/email as identity, and point every package URL at the canonical `shinjh0380` repository. The old-owner check is `rg -n "github\.com/makeitliveforever" . --glob '!.git/**'` (zero hits); it does not inspect the email domain. The strip job generates ignored `csc.rsp` files in `Runtime/` and `Tests/Runtime/`, defines both strip symbols, and requires the Runtime probes to be true when `TRACEFORGE_CI_EXPECT_STRIPPED=1`. Do not use `defineConstraints` for this proof; see Phase 4's updated strategy.

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

Since the package root is the repository root, `.github/workflows/test.yml` checks out into `package/` and uses `packageMode: true`. The three jobs use Unity `6000.0.84f1` (default), `6000.3.8f1` (default), and `6000.3.8f1` (both strip symbols). Each runs EditMode and PlayMode without coverage instrumentation, with distinct checks and XML artifacts. Package-mode Library caching is not supported by GameCI and is omitted.

The strip job writes ignored response files and their metas in `Runtime/` and `Tests/Runtime/`, with `-define:TRACEFORGE_STRIP_TRACE` and `-define:TRACEFORGE_STRIP_DEBUG` on separate lines. No response file is committed. The action pins GameCI CLI `v0.1.69` and uses its documented `GAME_CI_DOCKER_ENV` option to forward `TRACEFORGE_CI_EXPECT_STRIPPED=1` into the strip container. Arbitrary action environment variables alone are not forwarded. `GAME_CI_COVERAGE_ENABLED=false` disables instrumentation without the action's incompatible `--no-coverageEnabled` argument. Credentials remain action environment secrets; no custom image or credential build arguments are needed. A post-run XML check requires both platforms to pass and the stripped probe marker to be present.

Checks before enabling:

- [x] Confirmed Docker Hub tags `ubuntu-6000.0.84f1-linux-il2cpp-3` and `ubuntu-6000.3.8f1-linux-il2cpp-3` exist (2026-09-22).
- [x] Inspected the v4 README, action inputs and current GameCI CLI: custom parameters are forwarded to Unity but no supported scripting-defines CLI flag is documented. Use the approved CI-generated response files and Runtime probe proof; do not use `defineConstraints`.
- [x] `main` is excluded. Triggers are `dev` pushes/PRs and `feature/native-logger` pushes for this session's verification.

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

- [x] Write `Documentation~/Migration.md`
- [x] Optional scanner assessed and omitted; the required migration guide is the deliverable for this session
- [ ] Add the CI workflow using the three user-registered secrets; push `feature/native-logger` and verify all three matrix jobs
- [x] Metadata cleanup + `PackageMetaValidationTests` extension
- [x] Backfill CHANGELOG 0.2.0 and retain Phase 1–4 changes under Unreleased (version-bump deferred)
- [x] Update `DeferredIssues.md` (issue 6 resolved; add the lazy-formatting item)
- [x] Compare both audit/release checklists item by item and record results in the commit body
- [ ] Run `traceforge-release-check` for the release (Session 7)
- [ ] Run `traceforge-version-bump` to 0.3.0
- [ ] Run `traceforge-release-merge` (`dev → main`); tag `v0.3.0`
- [ ] Install `main` via Git URL into a fresh Unity 6000.3 project and repeat the Phase 2 completion check (zero-setup `TF.Info` → Viewer) as the final acceptance test

## Session 6 validation

- Local Unity 6000.3.8f1: default EditMode 31/31 and PlayMode 83/83; CI-style csc.rsp EditMode 30/30 and PlayMode 83/83. The stripped EditMode run temporarily excludes the host-only Phase 4 test that checks global Player defines, which are intentionally not used by the CI response-file mechanism. Package tests are all included.
- Negative control: with expectation environment variable `1` and no response files, the probe test fails; with response files, both Runtime probes are true and the test passes. The workflow's XML verification code passes against both real result sets.
- Local PlayMode uses the previously established domain-reload workaround; original host settings, the host-only test, and generated response files/metas are restored after verification.
- Package URLs are canonical; author name/email and version 0.2.0 are unchanged. Historical performance rig reproducibility remains a documented Phase 4 limitation; this session makes no new timing claim.
- First CI run `35688902916` failed all three jobs before Unity started: `Unknown argument: noCoverageEnabled`. The action/CLI argument mismatch is repaired using the pinned CLI environment options above; the next remote run is pending. Release operations below remain for Session 7.

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
