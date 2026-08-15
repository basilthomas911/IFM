[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Cpp', 'Rust')]
    [string]$Implementation,

    [ValidateSet('Future', 'Options')]
    [string]$Scenario = 'Future',

    [ValidateRange(1, 1440)]
    [int]$DurationMinutes = 60,

    [datetime]$StartAt = [datetime]::Today.AddHours(15),

    [string]$ResultsRoot,

    [switch]$IncludeMbo,
    [switch]$CaptureCsv,
    [switch]$AllowDirtyWorkingTree,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests\TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj'
if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
    $ResultsRoot = Join-Path $repoRoot 'artifacts\DatabentoMarketCloseSoak'
}

if ([TimeZoneInfo]::Local.Id -ne 'Eastern Standard Time') {
    throw "The market-close launcher requires Windows Eastern Standard Time; current zone is $([TimeZoneInfo]::Local.Id)."
}

if ([string]::IsNullOrWhiteSpace($env:DATABENTO_API_KEY)) {
    throw 'DATABENTO_API_KEY must be set in this PowerShell session.'
}

$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to determine the repository commit.'
}
$gitStatus = (& git -C $repoRoot status --porcelain) -join [Environment]::NewLine
if (-not [string]::IsNullOrWhiteSpace($gitStatus) -and -not $AllowDirtyWorkingTree) {
    throw 'The working tree is dirty. Commit the comparison build or pass -AllowDirtyWorkingTree explicitly.'
}

$runId = '{0}-{1}-{2}' -f $StartAt.ToString('yyyyMMdd-HHmm'), $Implementation.ToLowerInvariant(), $Scenario.ToLowerInvariant()
$resultDirectory = Join-Path $ResultsRoot $runId
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$preflightLog = Join-Path $resultDirectory 'preflight.log'

$buildArguments = @(
    'build',
    $project,
    '-c', 'Release',
    '--no-restore',
    "-p:DatabentoNativeImplementation=$Implementation",
    '-p:DatabentoEnableLive=true'
)

Write-Host "Building the $Implementation live adapter and smoke-test host..."
& dotnet @buildArguments 2>&1 | Tee-Object -FilePath $preflightLog
if ($LASTEXITCODE -ne 0) {
    throw "The $Implementation preflight build failed. See $preflightLog."
}

$nativeRoot = if ($Implementation -eq 'Rust') {
    Join-Path $repoRoot 'native.rust\DatabentoFeed.Rust'
} else {
    Join-Path $repoRoot 'native\DatabentoFeed.Native'
}
$sourceNativeDirectory = Join-Path $nativeRoot 'out\live-build\Release'
$runtimeNativeDirectory = Join-Path $repoRoot 'TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests\bin\Release\net10.0\runtimes\win-x64\native'
$sourceDll = Join-Path $sourceNativeDirectory 'databento_feed_native.dll'
$runtimeDll = Join-Path $runtimeNativeDirectory 'databento_feed_native.dll'
if (-not (Test-Path -LiteralPath $sourceDll) -or -not (Test-Path -LiteralPath $runtimeDll)) {
    throw 'The selected native DLL was not produced and staged into the smoke-test runtime.'
}
$sourceHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash
$runtimeHash = (Get-FileHash -LiteralPath $runtimeDll -Algorithm SHA256).Hash
if ($sourceHash -ne $runtimeHash) {
    throw 'The staged native DLL does not match the selected implementation build.'
}

$nativeFiles = Get-ChildItem -LiteralPath $runtimeNativeDirectory -File | ForEach-Object {
    [ordered]@{
        name = $_.Name
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}
$manifest = [ordered]@{
    schemaVersion = 1
    runId = $runId
    implementation = $Implementation
    scenario = $Scenario
    durationMinutes = $DurationMinutes
    scheduledStartLocal = $StartAt.ToString('o')
    timeZone = [TimeZoneInfo]::Local.Id
    createdOnUtc = [DateTimeOffset]::UtcNow.ToString('o')
    machineName = [Environment]::MachineName
    osVersion = [Environment]::OSVersion.VersionString
    processorCount = [Environment]::ProcessorCount
    dotnetVersion = (& dotnet --version).Trim()
    gitCommit = $gitCommit
    workingTreeDirty = -not [string]::IsNullOrWhiteSpace($gitStatus)
    includeMbo = [bool]$IncludeMbo
    captureCsv = [bool]$CaptureCsv
    nativeDllSha256 = $runtimeHash
    nativeFiles = @($nativeFiles)
}
$manifestPath = Join-Path $resultDirectory 'run-manifest.json'
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Host "Preflight passed for $Implementation."
Write-Host "Native DLL SHA-256: $runtimeHash"
Write-Host "Result directory: $resultDirectory"
if ($PreflightOnly) {
    Write-Host 'Preflight-only mode completed; the live soak was not started.'
    return
}

$now = Get-Date
if ($StartAt -lt $now.AddMinutes(-1)) {
    throw "The requested start time $StartAt has passed. Supply a future -StartAt value."
}
Write-Host "Starting the test host; the feed will warm up until $StartAt."

$filter = if ($Scenario -eq 'Options') {
    'FullyQualifiedName~DatabentoOneHourLiveSmokeTests.CurrentEsFutureOptionsReceiveEveryTickForConfiguredDuration'
} else {
    'FullyQualifiedName~DatabentoOneHourLiveSmokeTests.CurrentEsFutureReceivesEveryTickForConfiguredDuration'
}
$machineResultPath = Join-Path $resultDirectory 'soak-result.json'
$consoleLog = Join-Path $resultDirectory 'console.log'
$csvDirectory = Join-Path $resultDirectory 'ticks'
$previousEnvironment = @{
    IFM_RUN_DATABENTO_ONE_HOUR_TESTS = $env:IFM_RUN_DATABENTO_ONE_HOUR_TESTS
    IFM_DATABENTO_SOAK_MINUTES = $env:IFM_DATABENTO_SOAK_MINUTES
    IFM_DATABENTO_SOAK_START_LOCAL = $env:IFM_DATABENTO_SOAK_START_LOCAL
    IFM_DATABENTO_INCLUDE_MBO = $env:IFM_DATABENTO_INCLUDE_MBO
    IFM_DATABENTO_TICK_CSV_DIRECTORY = $env:IFM_DATABENTO_TICK_CSV_DIRECTORY
    IFM_DATABENTO_SOAK_RESULT_PATH = $env:IFM_DATABENTO_SOAK_RESULT_PATH
    IFM_DATABENTO_NATIVE_IMPLEMENTATION = $env:IFM_DATABENTO_NATIVE_IMPLEMENTATION
}

$startedOn = [DateTimeOffset]::Now
$exitCode = -1
try {
    $env:IFM_RUN_DATABENTO_ONE_HOUR_TESTS = '1'
    $env:IFM_DATABENTO_SOAK_MINUTES = $DurationMinutes.ToString([Globalization.CultureInfo]::InvariantCulture)
    $env:IFM_DATABENTO_SOAK_START_LOCAL = ([DateTimeOffset]$StartAt).ToString('o')
    $env:IFM_DATABENTO_NATIVE_IMPLEMENTATION = $Implementation
    $env:IFM_DATABENTO_SOAK_RESULT_PATH = $machineResultPath
    $env:IFM_DATABENTO_INCLUDE_MBO = if ($IncludeMbo) { '1' } else { $null }
    $env:IFM_DATABENTO_TICK_CSV_DIRECTORY = if ($CaptureCsv) { $csvDirectory } else { $null }

    $testArguments = @(
        'test',
        $project,
        '-c', 'Release',
        '--no-build',
        '--no-restore',
        "-p:DatabentoNativeImplementation=$Implementation",
        '-p:DatabentoEnableLive=true',
        '--filter', $filter,
        '--results-directory', $resultDirectory,
        '--logger', 'console;verbosity=detailed',
        '--logger', 'trx;LogFileName=soak.trx'
    )
    & dotnet @testArguments 2>&1 | Tee-Object -FilePath $consoleLog
    $exitCode = $LASTEXITCODE
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        Set-Item -Path "Env:$name" -Value $previousEnvironment[$name]
    }
}

$completion = [ordered]@{
    schemaVersion = 1
    runId = $runId
    implementation = $Implementation
    startedOnLocal = $startedOn.ToString('o')
    completedOnLocal = [DateTimeOffset]::Now.ToString('o')
    exitCode = $exitCode
    testPassed = $exitCode -eq 0
    machineResultCreated = Test-Path -LiteralPath $machineResultPath
    consoleLog = $consoleLog
    trx = Join-Path $resultDirectory 'soak.trx'
    machineResult = $machineResultPath
}
$completion | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resultDirectory 'run-completion.json') -Encoding utf8

if ($exitCode -ne 0) {
    throw "The $Implementation market-close soak failed with exit code $exitCode. See $resultDirectory."
}
if (-not (Test-Path -LiteralPath $machineResultPath)) {
    throw "The test passed but did not create $machineResultPath."
}
Write-Host "The $Implementation market-close soak passed. Results: $resultDirectory"
