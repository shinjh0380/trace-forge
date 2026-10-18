[CmdletBinding()]
param(
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe',
    [string]$HostProject = 'C:\Projects\Other\trace-forge\.worktrees\native-logger-test-host',
    [string]$PythonExe = 'python',
    [string]$RunName = '.readme-logging-comparison-20261018'
)

$ErrorActionPreference = 'Stop'
if ($RunName -notmatch '^\.readme-logging-comparison-[A-Za-z0-9_-]+$') { throw 'Use a new bounded run name.' }
$taskRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$taskHost = (Resolve-Path -LiteralPath $HostProject).Path
$taskEvidence = Join-Path $taskHost $RunName
if (Test-Path -LiteralPath $taskEvidence) { throw 'Run directory exists; do not overwrite earlier attempts.' }
if (-not (Test-Path -LiteralPath $UnityExe)) { throw 'Unity Editor is missing.' }
$taskHelper = Join-Path $taskHost 'Assets/Editor/ReadmeLoggingComparison.cs'
if ((Test-Path -LiteralPath $taskHelper) -or (Test-Path -LiteralPath ($taskHelper + '.meta'))) { throw 'Host helper already exists.' }
$taskActive = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.Replace('\','/') -match '/Unity/Hub/Editor/' })
if ($taskActive.Count -ne 0) { throw 'Close other Unity Editors before timing. Unity Hub can remain open.' }
New-Item -ItemType Directory -Path $taskEvidence | Out-Null

$taskPlanPath = Join-Path $PSScriptRoot 'plan.json'
$taskPlan = Get-Content -Raw -LiteralPath $taskPlanPath | ConvertFrom-Json
$taskPlan.frozenBeforeFirstTimingUtc = (Get-Date -AsUTC).ToString('o')
$taskPlan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskPlanPath -Encoding utf8
Copy-Item -LiteralPath $taskPlanPath -Destination (Join-Path $taskEvidence 'frozen-plan.json')

$taskBefore = @{}
foreach ($taskFolder in @('Assets','Packages','ProjectSettings')) {
    foreach ($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskHost $taskFolder) -File -Recurse) {
        $taskRelative = [IO.Path]::GetRelativePath($taskHost,$taskFile.FullName)
        $taskBefore[$taskRelative] = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
        $taskBackup = Join-Path $taskEvidence ('host-before/' + $taskRelative)
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskBackup))
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskBackup
    }
}
$taskBefore | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $taskEvidence 'host-before-hashes.json') -Encoding utf8
$taskSource = @{}
foreach ($taskFile in @(git -C $taskRepo ls-files Runtime Editor)) {
    $taskSource[$taskFile] = (Get-FileHash -LiteralPath (Join-Path $taskRepo $taskFile) -Algorithm SHA256).Hash
}
$taskSource | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskEvidence 'product-before-hashes.json') -Encoding utf8
[pscustomobject]@{
    PlanSha256 = (Get-FileHash -LiteralPath $taskPlanPath).Hash
    SourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ReadmeLoggingComparison.cs')).Hash
    RunnerSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash
    VerifierSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'verify_run.py')).Hash
    ProductCommit = git -C $taskRepo rev-parse HEAD
    CPU = @(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)
    OS = Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,TotalVisibleMemorySize
    PowerScheme = powercfg /getactivescheme
    EditorExe = $UnityExe
    StartedUtc = (Get-Date -AsUTC).ToString('o')
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskEvidence 'environment.json') -Encoding utf8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ReadmeLoggingComparison.cs') -Destination $taskHelper
@"
fileFormatVersion: 2
guid: $([guid]::NewGuid().ToString('N'))
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"@ | Set-Content -LiteralPath ($taskHelper + '.meta') -Encoding utf8

try {
    for ($taskIndex = 0; $taskIndex -lt $taskPlan.order.Count; $taskIndex++) {
        $taskMode = $taskPlan.order[$taskIndex]
        $taskName = 'run{0:D2}-{1}' -f ($taskIndex+1),$taskMode
        $taskDir = Join-Path $taskEvidence $taskName
        New-Item -ItemType Directory -Path $taskDir | Out-Null
        $taskLog = Join-Path $taskDir 'unity.log'
        $taskArgs = @('-batchmode','-nographics','-quit','-projectPath',('"{0}"' -f $taskHost),
            '-executeMethod','ReadmeLoggingComparison.Run','-logFile',('"{0}"' -f $taskLog),
            '-tfMode',$taskMode,'-tfOut',('"{0}"' -f $taskDir))
        [pscustomobject]@{ Arguments=$taskArgs; StartedUtc=(Get-Date -AsUTC).ToString('o'); CPU=@(Get-CimInstance Win32_Processor | Select-Object LoadPercentage) } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskDir 'invocation.json') -Encoding utf8
        $taskProcess = Start-Process -FilePath $UnityExe -ArgumentList $taskArgs -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $taskDir 'stdout.txt') -RedirectStandardError (Join-Path $taskDir 'stderr.txt')
        if (-not $taskProcess.WaitForExit(300000)) {
            Stop-Process -Id $taskProcess.Id -ErrorAction SilentlyContinue
            throw "Timeout in $taskName; preserve this attempt and stop."
        }
        $taskProcess.Refresh()
        [pscustomobject]@{ ExitCode=$taskProcess.ExitCode; EndedUtc=(Get-Date -AsUTC).ToString('o'); CPU=@(Get-CimInstance Win32_Processor | Select-Object LoadPercentage) } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskDir 'exit.json') -Encoding utf8
        if ($taskProcess.ExitCode -ne 0) {
            if (Test-Path -LiteralPath $taskLog) { Select-String -LiteralPath $taskLog -Pattern '\b(error|warning) (CS|UAC)\d+|Exception|license|activation' | Select-Object -Last 12 | ForEach-Object Line | Write-Output }
            throw "Unity exit $($taskProcess.ExitCode) in $taskName; no result is excluded or retried."
        }
        & $PythonExe -X utf8 (Join-Path $PSScriptRoot 'verify_run.py') $taskDir
        if ($LASTEXITCODE -ne 0) { throw "Output/state verification failed in $taskName; stop." }
        foreach ($taskFile in $taskSource.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $taskRepo $taskFile)).Hash -ne $taskSource[$taskFile]) { throw 'Product source changed during measurement.' }
        }
        Write-Output ("COMPLETED {0}/10 {1}" -f ($taskIndex+1),$taskName)
    }
}
finally {
    # Move only this run's two helper files out of Assets. No recursive removal.
    foreach ($taskPath in @($taskHelper,($taskHelper+'.meta'))) {
        if (Test-Path -LiteralPath $taskPath) {
            $taskDestination = Join-Path $taskEvidence ([IO.Path]::GetFileName($taskPath))
            Move-Item -LiteralPath $taskPath -Destination $taskDestination
        }
    }
    $taskChanges = @()
    foreach ($taskRelative in $taskBefore.Keys) {
        $taskPath = Join-Path $taskHost $taskRelative
        if (-not (Test-Path -LiteralPath $taskPath) -or (Get-FileHash -LiteralPath $taskPath).Hash -ne $taskBefore[$taskRelative]) { $taskChanges += $taskRelative }
    }
    [pscustomobject]@{ ExistingHostFiles=$taskBefore.Count; ChangedExistingFiles=$taskChanges; HelperRemovedFromAssets=(-not (Test-Path -LiteralPath $taskHelper)); FinishedUtc=(Get-Date -AsUTC).ToString('o') } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'host-after-verification.json') -Encoding utf8
    if ($taskChanges.Count -ne 0) { Write-Error ('Host files changed; inspect preserved before snapshots: ' + ($taskChanges -join ', ')) }
}
