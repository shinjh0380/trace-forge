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

At that compatibility checkpoint, EditMode 32 passed but both standard PlayMode attempts were blocked by a missing controller script. The [compatibility record](Benchmarks/2026-10-18-unity66-context-compatibility.md) retains those failures. The later [path repair](Benchmarks/2026-10-18-playmode-host-repair-and-performance.md) restores full PlayMode 107/107 (15 additional cases after the original 92). No Framework code/asmdef workaround was adopted: unsuccessful embedding and reference-removal probes were restored.

### Creation procedure

For the nested 6000.6 host, apply the [package-path correction](#nested-host-package-path-correction-60006) below before running tests. The package checkout contains the host, so its local-package path overlaps the host's default Framework cache.

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

## Nested-host package-path correction (6000.6)

The 2026-10-18 [repair investigation](Benchmarks/2026-10-18-playmode-host-repair-and-performance.md) reproduced a path alias: adding the parent checkout as a local package changes the Framework controller's logical path from `Packages/com.unity.test-framework/...` to `Packages/com.skrawberries.traceforge/.worktrees/.../Library/PackageCache/...`. Its `MonoScript.GetClass()` becomes null; the runner scene stores an inline script reference that fails across domain reload. Removing only the parent dependency restores the correct mapping in the isolated project.

Keep the host inside `.worktrees/` and the TraceForge dependency pointing at this checkout. Resolve the same installed Test Framework from an **unmodified writable copy outside the checkout**, so its physical path cannot match the parent package. Do not edit Framework code/asmdefs or disable reload. Directly referencing Program Files restored binding but produced directory-watcher write failures; the final host uses `%LOCALAPPDATA%/TraceForge/UnityTestFramework/6000.6.6f1-1.8.0` instead.

Close the host, preserve manifest/lock, and apply this normalization using the runner's `$UnityExe` and absolute `$HostPath`:

```powershell
$editorDirectory = Split-Path $UnityExe -Parent
$editorVersion = Split-Path (Split-Path $editorDirectory -Parent) -Leaf
$frameworkSource = Join-Path $editorDirectory 'Data/Resources/PackageManager/BuiltInPackages/com.unity.test-framework'
$frameworkVersion = (Get-Content (Join-Path $frameworkSource 'package.json') -Raw | ConvertFrom-Json).version
$frameworkCopy = Join-Path $env:LOCALAPPDATA "TraceForge/UnityTestFramework/$editorVersion-$frameworkVersion"
if (-not (Test-Path -LiteralPath $frameworkCopy)) {
    New-Item -ItemType Directory -Path (Split-Path $frameworkCopy -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $frameworkSource -Destination $frameworkCopy -Recurse
}
if (@(Get-ChildItem -LiteralPath $frameworkCopy -File -Recurse).Count -ne
    @(Get-ChildItem -LiteralPath $frameworkSource -File -Recurse).Count) {
    throw 'Framework copy contains missing or extra files'
}
Get-ChildItem -LiteralPath $frameworkSource -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($frameworkSource, $_.FullName)
    if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $frameworkCopy $relative)).Hash) {
        throw "Framework copy differs: $relative"
    }
}
$manifestPath = Join-Path $HostPath 'Packages/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies.'com.unity.test-framework' = 'file:' + $frameworkCopy.Replace('\', '/')
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
```

All 1,176 original Framework files, 85 tracked package source/assets and 56 original host fixture files retained identical hashes during this repair. Only the host manifest/lock dependency source changes permanently. Default domain/scene reload and the standard runner remain unchanged. Record the resolved Framework version and reverify the copy when changing editors; this host-specific correction is not a new runtime package dependency.
