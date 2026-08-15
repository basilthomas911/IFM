[CmdletBinding()]
param(
    [string]$CppResultDirectory,
    [string]$RustResultDirectory,
    [string]$ResultsRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
    $ResultsRoot = Join-Path $repoRoot 'artifacts\DatabentoMarketCloseSoak'
}

function Find-LatestResultDirectory([string]$implementation) {
    $match = Get-ChildItem -LiteralPath $ResultsRoot -Directory |
        Where-Object { $_.Name -like "*-$implementation-future" } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'soak-result.json') } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $match) {
        throw "No completed $implementation future result was found below $ResultsRoot."
    }
    return $match.FullName
}

if ([string]::IsNullOrWhiteSpace($CppResultDirectory)) {
    $CppResultDirectory = Find-LatestResultDirectory 'cpp'
}
if ([string]::IsNullOrWhiteSpace($RustResultDirectory)) {
    $RustResultDirectory = Find-LatestResultDirectory 'rust'
}

function Read-Run([string]$directory, [string]$expectedImplementation) {
    $resolved = (Resolve-Path $directory).Path
    $manifest = Get-Content -LiteralPath (Join-Path $resolved 'run-manifest.json') -Raw |
        ConvertFrom-Json
    $completion = Get-Content -LiteralPath (Join-Path $resolved 'run-completion.json') -Raw |
        ConvertFrom-Json
    $result = Get-Content -LiteralPath (Join-Path $resolved 'soak-result.json') -Raw |
        ConvertFrom-Json
    if ($manifest.implementation -ne $expectedImplementation) {
        throw "$resolved contains $($manifest.implementation), expected $expectedImplementation."
    }
    if (-not $completion.testPassed) {
        throw "$expectedImplementation did not pass its soak test."
    }
    return [ordered]@{
        directory = $resolved
        manifest = $manifest
        completion = $completion
        result = $result
    }
}

$cpp = Read-Run $CppResultDirectory 'Cpp'
$rust = Read-Run $RustResultDirectory 'Rust'
foreach ($property in @('gitCommit', 'scenario', 'durationMinutes', 'includeMbo', 'captureCsv')) {
    if ($cpp.manifest.$property -ne $rust.manifest.$property) {
        throw "Comparison rejected: manifest property '$property' differs."
    }
}

function To-Summary([System.Collections.IDictionary]$run) {
    $result = $run.result
    $ticks = [double]$result.ticks
    return [ordered]@{
        implementation = $run.manifest.implementation
        ticks = [long]$result.ticks
        ticksPerSecond = [double]$result.ticksPerSecond
        cpuSeconds = [double]$result.process.cpuSeconds
        cpuSecondsPerMillionTicks = if ($ticks -eq 0) { 0 } else {
            [double]$result.process.cpuSeconds * 1000000 / $ticks
        }
        averageCpuCores = [double]$result.process.averageCpuCores
        managedAllocatedBytes = [long]$result.process.managedAllocatedBytes
        managedAllocatedBytesPerMillionTicks = if ($ticks -eq 0) { 0 } else {
            [double]$result.process.managedAllocatedBytes * 1000000 / $ticks
        }
        peakWorkingSetBytes = [long]$result.process.peakWorkingSetBytes
        privateMemoryBytes = [long]$result.process.privateMemoryBytes
        ringHighWaterRecords = [long]$result.native.ringHighWaterRecords
        ringHighWaterPercent = if ([double]$result.native.ringCapacityRecords -eq 0) { 0 } else {
            100 * [double]$result.native.ringHighWaterRecords /
                [double]$result.native.ringCapacityRecords
        }
        channelFullCount = [long]$result.native.channelFullCount
        poolMissCount = [long]$result.native.poolMissCount
        drainPassLimitHitCount = [long]$result.native.drainPassLimitHitCount
        exceptions = [long]$result.exceptions
        recordsReconciled = [long]$result.lifetimeTicks -eq [long]$result.native.recordsConsumed -and
            [long]$result.native.recordsProduced -eq [long]$result.native.recordsConsumed
    }
}

$cppSummary = To-Summary $cpp
$rustSummary = To-Summary $rust
$comparison = [ordered]@{
    schemaVersion = 1
    createdOnUtc = [DateTimeOffset]::UtcNow.ToString('o')
    gitCommit = $cpp.manifest.gitCommit
    scenario = $cpp.manifest.scenario
    durationMinutes = $cpp.manifest.durationMinutes
    cppDirectory = $cpp.directory
    rustDirectory = $rust.directory
    cpp = $cppSummary
    rust = $rustSummary
    notes = @(
        'Raw tick rate is market-volume dependent because the sessions occurred on different days.',
        'Prefer correctness and per-million-record CPU/allocation metrics for the implementation decision.'
    )
}
$outputPath = Join-Path $ResultsRoot 'cpp-rust-market-close-comparison.json'
$comparison | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8

@($cppSummary, $rustSummary) |
    ForEach-Object { [pscustomobject]$_ } |
    Format-Table implementation, ticks, ticksPerSecond, cpuSecondsPerMillionTicks,
        managedAllocatedBytesPerMillionTicks, peakWorkingSetBytes,
        ringHighWaterPercent, channelFullCount, poolMissCount, exceptions,
        recordsReconciled -AutoSize
Write-Host "Comparison JSON: $outputPath"
