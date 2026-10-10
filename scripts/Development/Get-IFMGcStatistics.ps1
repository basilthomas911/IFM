[CmdletBinding()]
param([switch] $IncludeStopped)

$ErrorActionPreference = "Stop"
$taskRepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$taskHistoryRoot = Join-Path $taskRepositoryRoot ".artifacts/telemetry/gc"
if (-not (Test-Path -LiteralPath $taskHistoryRoot)) {
    Write-Output "No lifetime GC history yet. Start API/UI with the updated development build."
    return
}
$taskRunning = @(& (Join-Path $PSScriptRoot "Get-IFMDevelopmentProcess.ps1") | Where-Object Role -in @("api", "ui") | ForEach-Object ProcessId)
$taskObservations = foreach ($taskFile in Get-ChildItem -LiteralPath $taskHistoryRoot -Filter "process-gc-*.jsonl") {
    # A forced kill may leave the final line incomplete; use the latest complete observation.
    $taskLines = @(Get-Content -LiteralPath $taskFile.FullName -Tail 20)
    for ($taskIndex = $taskLines.Count - 1; $taskIndex -ge 0; $taskIndex--) {
        try { $taskSample = $taskLines[$taskIndex] | ConvertFrom-Json } catch { continue }
        if ($IncludeStopped -or $taskRunning -contains $taskSample.ProcessId) {
            $taskSample | Add-Member -NotePropertyName HistoryPath -NotePropertyValue $taskFile.FullName -PassThru
        }
        break
    }
}
$taskObservations | Group-Object Service | ForEach-Object {
    $taskSample = $_.Group | Sort-Object ProcessStartedUtc -Descending | Select-Object -First 1
    [PSCustomObject]@{
        Service = $taskSample.Service
        ProcessId = $taskSample.ProcessId
        ProcessRunId = $taskSample.ProcessRunId
        StartedUtc = $taskSample.ProcessStartedUtc
        LastSampleUtc = $taskSample.RecordedUtc
        Phase = $taskSample.Phase
        UptimeHours = [Math]::Round($taskSample.UptimeSeconds / 3600, 2)
        Gen0SinceStart = $taskSample.Gen0CollectionsSinceStart
        Gen1SinceStart = $taskSample.Gen1CollectionsSinceStart
        Gen2SinceStart = $taskSample.Gen2CollectionsSinceStart
        AllocatedMiBSinceStartApproximate = [Math]::Round($taskSample.AllocatedBytesSinceStartApproximate / 1MB, 2)
        GcPauseMillisecondsSinceStart = $taskSample.PauseMillisecondsSinceStart
        LastGcHeapMiB = [Math]::Round($taskSample.LastGcHeapBytes / 1MB, 2)
        WorkingSetMiB = [Math]::Round($taskSample.WorkingSetBytes / 1MB, 2)
        HistoryPath = $taskSample.HistoryPath
    }
}
