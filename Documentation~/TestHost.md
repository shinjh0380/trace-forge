# Unity Test Host

## Normalized configuration (2026-10-18)

The verification host is `C:/Projects/Other/trace-forge/.worktrees/native-logger-test-host`, validated with Unity **6000.3.8f1** and Test Framework **1.6.0**. Its PlayMode fixture assembly, `Assets/Phase1SampleVerification/TraceForge.HostSample.Tests.asmdef`, references:

```json
["TraceForge.Runtime", "TraceForge.Editor", "TraceForge.HostSample", "UnityEngine.TestRunner"]
```

The unused `UnityEditor.TestRunner` reference was removed as a permanent **host** correction. Package assemblies and test bodies, filters and assertions are unchanged. Keep the normalized host after verification; restoring the earlier asmdef reintroduces the observed missing-runner-script startup failure.

The original host files, asmdef hashes and normalized fixture copy are retained under `.approval-20261018/` inside the host: `Assets.before/`, `Assets.normalized/`, `normalization.json`, `host-before.csv` and `run-tests.original.ps1`. Host files remain ignored and are not shipped in the package. The dated guard validation report describes the earlier temporary workaround; it remains a historical record.

## Editor update attempted (2026-10-18)

After the user installed **6000.6.6f1**, the runner's `$UnityExe` was updated to that path. An isolated full compile failed before tests: nine CS0619 sites in `TF.cs` (`GetInstanceID`) and three UAC0009 sites (`DEVELOPMENT_BUILD`). The previous 6000.3.8f1 results do not establish compatibility with 6000.6.6f1. See the [failed revalidation record](Benchmarks/2026-10-18-unity-6000.6.6f1-revalidation.md); preserve its `.approval-20261018-unity600066/` backups. The host's old `ProjectVersion.txt` was retained after the failed upgrade. Do not treat the runner path or project-version metadata as proof of a successful suite.

## Recreate the host

### 6000.6 compatibility normalization

The compatibility follow-up keeps minimum package support at 6000.0. The 6000.6.6f1 host resolves **Test Framework 1.8.0 builtin** (Unity automatically updates the old 1.6.0 request). The new normalized fixture archive is `.unity66-fix-20261018/Assets.final-normalized/`: it preserves metas and adds only version-specific reflection/context lookup APIs. Keep the original `.approval-20261018/` archive for the prior 6000.3 host; it contains older context API calls.

The runner still uses the same `-Platform`/`-Out` interface and test arguments. It now waits for the Editor process with `WaitForExit()` rather than the entire child-process tree through `Start-Process -Wait`; compiler/import helpers can outlive a successful Editor exit. See the [PowerShell lifecycle distinction](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/start-process?view=powershell-7.5).

Full EditMode 32 passes with clean compilation. The upgraded host's expected complete PlayMode count is 107 (15 additional cases after the original 92); a missing controller script blocks both standard attempts, including a cold-cache run. The [compatibility record](Benchmarks/2026-10-18-unity66-context-compatibility.md) distinguishes standard suites, direct Editor smoke checks and incomplete benchmarks. No Framework code/asmdef workaround was adopted: unsuccessful embedding and reference-removal probes were restored.

### Creation procedure

1. Close Unity and preserve the existing host, especially fixture sources/metas, manifest, ProjectSettings and `run-tests.ps1`. Use a new empty project directory for recreation. Create it with the installed editor and a dedicated log:

   ```powershell
   & $UnityExe -batchmode -quit -createProject $HostPath -logFile $CreateLog
   ```

2. Replace `Packages/manifest.json` with:

   ```json
   {
     "dependencies": {
       "com.skrawberries.traceforge": "file:C:/Projects/Other/trace-forge",
       "com.unity.test-framework": "1.6.0"
     },
     "testables": ["com.skrawberries.traceforge"]
   }
   ```

3. For 6000.6, accept the resolved Test Framework 1.8.0 builtin and restore fixture sources/metas from `.unity66-fix-20261018/Assets.final-normalized/`, plus the current saved `run-tests.ps1`. For the older 6000.3 host, use its `Assets.normalized/` archive and 1.6.0. These fixtures include Basic Usage, lifecycle and third-party-capture verification. A fresh clone without this local fixture archive has a different test count; do not claim the full host's counts for it.
4. Check the host PlayMode asmdef above. If restoring an older fixture archive, back up that file and remove **only** `UnityEditor.TestRunner` from its `references` array. Retain all other references, assembly settings and tests. The normalized archive already contains this correction. Use normalized snapshots for subsequent benchmark restoration; the older `.guard-fix-20261014/restore-host.ps1` restores the pre-correction file.
5. Keep default domain and scene reload (`m_EnterPlayModeOptionsEnabled: 0`, `m_EnterPlayModeOptions: 0`) and empty Standalone scripting defines. Set `$UnityExe` at the top of the runner to the installed editor. Use `-logFile` on every Unity invocation.

## Standard verification

From the repository root, run both complete suites with unique output names:

```powershell
& ./.worktrees/native-logger-test-host/run-tests.ps1 -Platform EditMode -Out <unique-edit-name>
& ./.worktrees/native-logger-test-host/run-tests.ps1 -Platform PlayMode -Out <unique-play-name>
```

Require XML `result=Passed`, expected nonzero counts, `failed=0` and `skipped=0`. Preserve initial failures and retries under separate names. The normalized host's expected counts at candidate `d525209` are **EditMode 32 / PlayMode 92**; compare case names as well as totals. Existing FileSink and real Logger allocation tests include a positive control; GC sample proxy 0 is not a general zero-bytes claim.

For an actual full compiler check, preserve/move the host `Library`/`Temp` and set process-local `BEE_CACHE_DIRECTORY` to a new empty directory before the standard EditMode run. Confirm actual Csc execution for Runtime, Editor and both package test assemblies, no Csc CacheHit, and zero C# warnings/errors. Restore the environment variable afterward; preserving only a fresh local Library does not exclude the global Bee cache.

Restore manifest, defines, ProjectSettings and fixture contents to the **normalized** snapshot after temporary benchmark configuration. Retain raw XML/logs, files and snapshots separately from fixtures.
