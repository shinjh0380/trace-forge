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

3. Restore the prior host fixture sources and their `.meta` files from `Assets.normalized/`, plus the saved `run-tests.ps1`. These fixtures include the Basic Usage, lifecycle and third-party-capture verification. A fresh clone without this local fixture archive has a different test count; do not claim the full host's 32/92 counts for it.
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
