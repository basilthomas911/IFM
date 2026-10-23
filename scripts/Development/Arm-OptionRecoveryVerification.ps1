[CmdletBinding()]
param(
    [ValidateSet('Happy', 'Deadline')][string]$Mode = 'Happy',
    [Alias('HealthySeconds')][ValidateRange(300, 3600)][int]$DelaySeconds = 300,
    [switch]$RequireApiRestart
)
$ErrorActionPreference = 'Stop'
if ($RequireApiRestart) {
    if ($Mode -ne 'Deadline') { throw 'API restart acceptance requires Deadline mode.' }
    try { $capability = Invoke-RestMethod -Uri 'http://localhost:22543/health/recovery-ready' -TimeoutSec 10 }
    catch { throw 'The running API does not expose the restart-controller capability. Rebuild and restart with the updated API + Rust UI Development launcher first. No fault was armed.' }
    if (!$capability.RestartControllerEnabled) { throw 'The running API does not have the external restart controller enabled. Restart using the updated API + Rust UI Development launcher first. No fault was armed.' }
    Write-Host "API restart controller is enabled for API PID $($capability.ProcessId)."
}
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$directory = Join-Path $repositoryRoot '.artifacts/option-recovery-verification'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$requestPath = Join-Path $directory 'request.json'
if (Test-Path -LiteralPath $requestPath) { throw 'An unconsumed verification request already exists.' }
$operation = [guid]::NewGuid()
$request = @{
    OperationId = $operation.ToString('D')
    Mode = $Mode
    DelaySeconds = $DelaySeconds
    ExpiresAtUtc = [DateTimeOffset]::UtcNow.AddSeconds($DelaySeconds + 900).ToString('O')
}
$temporary = Join-Path $directory ($operation.ToString('D') + '.request.tmp')
$request | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
Move-Item -LiteralPath $temporary -Destination $requestPath
Write-Host "Armed $Mode verification: $operation"
Write-Host "Fault injection occurs after $DelaySeconds elapsed seconds, regardless of existing quote or plan health."
Write-Host "Result: $(Join-Path $directory ($operation.ToString('D') + '.json'))"
if ($RequireApiRestart) {
    $sessionFile = Join-Path $env:LOCALAPPDATA 'TomasAI/IFM/Development/server-manager-session.json'
    $managerSession = Get-Content -LiteralPath $sessionFile -Raw | ConvertFrom-Json
    $restartResult = Join-Path (Split-Path $sessionFile -Parent) "ApiRecovery/$($managerSession.SessionId)/result-$($operation.ToString('N')).json"
    Write-Host "External API restart result: $restartResult"
}
Write-Host 'Deadline mode permits the external Server Manager recovery controller to restart the API once for this value date, when deployed and enabled. This command does not submit orders.'
