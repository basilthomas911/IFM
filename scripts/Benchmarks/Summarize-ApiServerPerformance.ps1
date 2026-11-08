[CmdletBinding()]
param(
    [string]$EvidenceRoot = 'E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107',
    [string]$OutputDirectory = 'E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\summary',
    [string]$FinalRunRoot
)
$ErrorActionPreference = 'Stop'
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ([IO.Path]::GetPathRoot($OutputDirectory) -ine 'E:\') { throw 'Large evidence reports must stay on E:.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$reports = @()
if ($FinalRunRoot) {
    $finalResults = Join-Path ([IO.Path]::GetFullPath($FinalRunRoot)) 'paired\results'
    if (-not (Test-Path -LiteralPath $finalResults)) { throw "Final measurements are missing: $finalResults" }
    $reports += Get-ChildItem -LiteralPath $finalResults -Filter '*-report-full*.json'
    $receive = Join-Path $EvidenceRoot 'acceptance-nats-final\paired\results'
    if (-not (Test-Path -LiteralPath $receive)) { throw 'Final NATS qualification measurements are missing.' }
    $reports += Get-ChildItem -LiteralPath $receive -Filter '*-report-full*.json'
}
else {
    # Initial expiry and seed candidates were rejected; the initial health case failed.
    # Only unchanged successful paths from that run are eligible for this summary.
    $reports += Get-ChildItem -LiteralPath (Join-Path $EvidenceRoot 'paired-bounded\results') -Filter '*-report-full*.json' |
        Where-Object Name -Match '(RpcBenchmarks|RpcDescriptorBenchmarks|HistoryBenchmarks|LoggingBenchmarks)-report'
    $acceptance = Join-Path $EvidenceRoot 'acceptance-corrected\paired\results'
    if (-not (Test-Path -LiteralPath $acceptance)) { throw 'Corrected acceptance measurements are still pending.' }
    $reports += Get-ChildItem -LiteralPath $acceptance -Filter '*-report-full*.json' |
        Where-Object Name -NotMatch '(ExpiryBenchmarks|ExpiryBurstBenchmarks|TradeContentionBenchmarks|DiagnosticBenchmarks)-report'
    $final = Join-Path $EvidenceRoot 'acceptance-final\paired\results'
    if (-not (Test-Path -LiteralPath $final)) { throw 'Final expiry and production-protocol measurements are still pending.' }
    $reports += Get-ChildItem -LiteralPath $final -Filter '*-report-full*.json' |
        Where-Object Name -NotMatch '(ExpiryBenchmarks|ExpiryBurstBenchmarks)-report'
    $expiry = Join-Path $EvidenceRoot 'acceptance-expiry-final\paired\results'
    if (-not (Test-Path -LiteralPath $expiry)) { throw 'Dense/sparse expiry qualification is still pending.' }
    $reports += Get-ChildItem -LiteralPath $expiry -Filter '*-report-full*.json'
    $receive = Join-Path $EvidenceRoot 'acceptance-nats\paired\results'
    if (Test-Path -LiteralPath $receive) { $reports += Get-ChildItem -LiteralPath $receive -Filter '*-report-full*.json' }
    $warmup = Join-Path $EvidenceRoot 'acceptance-warmup\paired\results'
    if (Test-Path -LiteralPath $warmup) { $reports += Get-ChildItem -LiteralPath $warmup -Filter '*-report-full*.json' }
}
$pairs = @{
    DescriptorBefore='DescriptorAfter'; PayloadBefore='PayloadAfter'; ReadBefore='ReadAfter'
    EverySuccessBefore='SampledSuccessAfter'; ExpireBefore='ExpireAfter'; AggregateBefore='AggregateAfter'
    FullResponseBefore='SummaryResponseAfter'; BatchBefore='BatchAfter'; DirectBefore='BufferedAfter'
    PerBarBefore='AtomicBatchAfter'; SynchronousBefore='AsynchronousAfter'; IndependentBefore='CoalescedAfter'
    SerialBefore='BoundedAfter'; BurstBefore='BurstAfter'; LegacyCompatibilityArray='ExistingPooledDefault'
    OriginalBefore='CurrentAfter'
}
function Get-Metric($Case, [string]$Name) {
    $metric = @($Case.Metrics | Where-Object {$_.Descriptor.Id -eq $Name})
    if ($metric.Count -ne 1) { throw "Missing or ambiguous metric $Name for $($Case.FullName)" }
    return [double]$metric[0].Value
}
$measurements = @()
$provenance = @()
foreach ($file in $reports) {
    $report = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    foreach ($case in $report.Benchmarks) {
        if ($null -eq $case.Statistics) { throw "Unexecuted case: $($case.FullName)" }
        $measurements += $case
    }
    $provenance += [ordered]@{ File=$file.FullName; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}
$comparison = @($measurements | Group-Object Type,Parameters | ForEach-Object {
    $group = $_.Group
    foreach ($before in $group | Where-Object {$pairs.ContainsKey($_.Method)}) {
        $after = @($group | Where-Object Method -EQ $pairs[$before.Method])
        if ($after.Count -ne 1) { throw "Missing or ambiguous paired candidate: $($before.FullName)" }
        $after = $after[0]
        [ordered]@{
            Workload=$before.Type; Parameters=$before.Parameters; Before=$before.Method; After=$after.Method
            BeforeNanoseconds=$before.Statistics.Mean; AfterNanoseconds=$after.Statistics.Mean
            TimeReductionPercent=100*(1-$after.Statistics.Mean/$before.Statistics.Mean)
            BeforeConfidenceMarginNanoseconds=$before.Statistics.ConfidenceInterval.Margin
            AfterConfidenceMarginNanoseconds=$after.Statistics.ConfidenceInterval.Margin
            BeforeBytes=$before.Memory.BytesAllocatedPerOperation; AfterBytes=$after.Memory.BytesAllocatedPerOperation
            BeforeGen0Per1000=(Get-Metric $before 'Gen0Collects'); AfterGen0Per1000=(Get-Metric $after 'Gen0Collects')
            BeforeGen1Per1000=(Get-Metric $before 'Gen1Collects'); AfterGen1Per1000=(Get-Metric $after 'Gen1Collects')
            BeforeGen2Per1000=(Get-Metric $before 'Gen2Collects'); AfterGen2Per1000=(Get-Metric $after 'Gen2Collects')
            BeforeMonitorContentionsPerOperation=(Get-Metric $before 'LockContentionCount')
            AfterMonitorContentionsPerOperation=(Get-Metric $after 'LockContentionCount')
            InvestigateLatencyRegression=($after.Statistics.Mean -gt 1.05*$before.Statistics.Mean)
        } | ForEach-Object {[pscustomobject]$_}
    }
})
if (-not $comparison.Count) { throw 'No measured pairs were found.' }
$comparison | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'comparison.csv') -NoTypeInformation
$provenance | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'measurement-files.json')
Write-Host "Summarized $($comparison.Count) measured pairs. Async wait durations, per-request latency, GC pauses and process memory remain separate probe/trace evidence."
$comparison | Select-Object Workload,Parameters,TimeReductionPercent,BeforeBytes,AfterBytes,InvestigateLatencyRegression | Format-Table -AutoSize
