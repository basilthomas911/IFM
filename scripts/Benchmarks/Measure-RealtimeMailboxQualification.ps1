param(
    [Parameter(Mandatory = $true)][ValidateSet('Core', 'SerialMemory', 'BatchMemory', 'BatchFile')][string]$Mode,
    [Parameter(Mandatory = $true)][int]$PayloadBytes,
    [Parameter(Mandatory = $true)][string]$BenchmarkDll,
    [Parameter(Mandatory = $true)][string]$TraceMetricsDll,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$DisposableNatsUrl = 'nats://127.0.0.1:45222'
)
$ErrorActionPreference = 'Stop'
if ($DisposableNatsUrl -ne 'nats://127.0.0.1:45222') {
    throw 'This qualification probe is restricted to the dedicated disposable broker on port 45222.'
}
if ($PayloadBytes -le 0 -or $PayloadBytes -gt 524288) { throw 'Invalid payload size.' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskStem = Join-Path $taskOutput "$Mode-$PayloadBytes"
$taskStart = "$taskStem.start"
if (Test-Path -LiteralPath $taskStart) { throw 'Use a new output directory; a prior start marker already exists.' }
$env:IFM_TEST_REALTIME_NATS = $DisposableNatsUrl
$taskProfile = $null
$taskTrace = $null
try {
    $taskBrokerBefore = & docker stats --no-stream --format '{{json .}}' ifm-realtime-mailbox-disposable-20261125
    if ($LASTEXITCODE -ne 0) { throw 'Could not sample the dedicated broker.' }
    $taskProfile = Start-Process dotnet -WindowStyle Hidden -PassThru `
        -ArgumentList @($BenchmarkDll, '--profile-durable-mailbox', $Mode, "$PayloadBytes", $taskStart, "$taskStem.metrics.json") `
        -RedirectStandardOutput "$taskStem.profile.log" -RedirectStandardError "$taskStem.profile.stderr.log"
    $taskReadyDeadline = [DateTime]::UtcNow.AddSeconds(90)
    do {
        if ($taskProfile.HasExited) { throw "Profile exited before readiness: $taskStem.profile.log" }
        $taskReady = Select-String -LiteralPath "$taskStem.profile.log" -Pattern 'PROFILE_READY pid=(\d+)' -ErrorAction SilentlyContinue
        if (-not $taskReady) { Start-Sleep -Milliseconds 200 }
        if ([DateTime]::UtcNow -gt $taskReadyDeadline) { throw 'Profile setup exceeded 90 seconds.' }
    } until ($taskReady)
    $taskProfileProcessId = [int]$taskReady.Matches[0].Groups[1].Value
    $taskTrace = Start-Process dotnet-trace -WindowStyle Hidden -PassThru `
        -ArgumentList @('collect', '--process-id', "$taskProfileProcessId", '--providers', 'Microsoft-Windows-DotNETRuntime:0x4001:5,IFM-Mailbox-Qualification:0xFFFFFFFFFFFFFFFF:5', '--output', "$taskStem.nettrace", '--duration', '00:02:00') `
        -RedirectStandardOutput "$taskStem.trace.log" -RedirectStandardError "$taskStem.trace.stderr.log"
    $taskAttachDeadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($taskTrace.HasExited) { throw "Trace exited before attachment: $taskStem.trace.log" }
        # Redirected dotnet-trace output omits the interactive progress line.
        # Its duration header is emitted after the EventPipe session is attached.
        $taskAttached = (Select-String -LiteralPath "$taskStem.trace.log" -Pattern 'Trace Duration' -ErrorAction SilentlyContinue) `
            -and (Test-Path -LiteralPath "$taskStem.nettrace")
        if (-not $taskAttached) { Start-Sleep -Milliseconds 200 }
        if ([DateTime]::UtcNow -gt $taskAttachDeadline) { throw 'Trace attachment exceeded 30 seconds.' }
    } until ($taskAttached)
    [IO.File]::WriteAllText($taskStart, 'start')
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(110)
    while (-not $taskProfile.WaitForExit(500)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw 'Marked workload exceeded 110 seconds.' }
    }
    if ($taskProfile.ExitCode -ne 0) { throw "Profile failed: $taskStem.profile.stderr.log" }
    if (-not $taskTrace.WaitForExit(15000)) { throw 'Trace did not finish after target exit.' }
    if ($taskTrace.ExitCode -ne 0) { throw "Trace failed: $taskStem.trace.log" }
    $taskBrokerAfter = & docker stats --no-stream --format '{{json .}}' ifm-realtime-mailbox-disposable-20261125
    if ($LASTEXITCODE -ne 0) { throw 'Could not sample the dedicated broker after the workload.' }
    @{ Before = ($taskBrokerBefore | ConvertFrom-Json); After = ($taskBrokerAfter | ConvertFrom-Json)
        Note = 'Container point samples before setup and after workload, not continuous peaks or isolated per-workload IO deltas.'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$taskStem.broker-samples.json" -Encoding utf8
    & dotnet $TraceMetricsDll "$taskStem.nettrace" "$taskStem.trace-metrics.json" "$taskProfileProcessId" $Mode
    if ($LASTEXITCODE -ne 0) { throw 'Trace metrics parser failed.' }
    $taskMetrics = Get-Content -Raw -LiteralPath "$taskStem.trace-metrics.json" | ConvertFrom-Json
    if (@($taskMetrics.Processes).Count -ne 1) { throw 'No unambiguous marked workload was found in the trace.' }
    $taskStarts = @($taskMetrics.WorkloadMarkers | Where-Object { $_.EventName -in @('WorkloadStart','Workload/Start') -and $_.Payload.name -eq $Mode })
    $taskStops = @($taskMetrics.WorkloadMarkers | Where-Object { $_.EventName -in @('WorkloadStop','Workload/Stop') -and $_.Payload.name -eq $Mode })
    if ($taskStarts.Count -ne 1 -or $taskStops.Count -ne 1) { throw 'Workload start/stop marker pair is incomplete.' }
    Get-Content -Raw -LiteralPath "$taskStem.metrics.json"
} finally {
    foreach ($taskChild in @($taskProfile, $taskTrace)) {
        if ($null -ne $taskChild -and -not $taskChild.HasExited) { $taskChild.Kill($true) }
    }
}
