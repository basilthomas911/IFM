[CmdletBinding()]
param(
    [int] $GracefulTimeoutSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$getProcessScript = Join-Path $workspace "scripts\Development\Get-IFMDevelopmentProcess.ps1"
$managedStopScript = Join-Path $workspace "scripts\Development\Stop-IFMDevelopment.ps1"

function Get-RepositoryDebugProcess {
    @(& $getProcessScript -RepositoryRoot $workspace) | Where-Object {
        $_.Role -in @("api", "ui") -and $_.LocksRepositoryOutput
    }
}

$allIfmProcesses = @(& $getProcessScript -RepositoryRoot $workspace)
$manager = @($allIfmProcesses | Where-Object Role -eq "manager")
if ($manager.Count -gt 0) {
    Write-Host "Stopping the active managed Development session before direct debugger startup."
    & $managedStopScript -VerifyStopped
}

$targets = @(Get-RepositoryDebugProcess)
foreach ($target in $targets | Sort-Object @{ Expression = { if ($_.Role -eq "ui") { 0 } else { 1 } } }) {
    $current = @(Get-RepositoryDebugProcess | Where-Object {
        $_.ProcessId -eq $target.ProcessId -and $_.StartedAtUtc -eq $target.StartedAtUtc
    })
    if ($current.Count -eq 0) {
        continue
    }

    $identityPath = if ($target.EntryAssemblyPath) {
        $target.EntryAssemblyPath
    }
    else {
        $target.HostPath
    }
    Write-Host "Stopping repository Debug $($target.Role) process $($target.ProcessId) from $identityPath"
    Stop-Process -Id $target.ProcessId -ErrorAction SilentlyContinue
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($GracefulTimeoutSeconds)
do {
    Start-Sleep -Milliseconds 200
    $remaining = @(Get-RepositoryDebugProcess)
} while ($remaining.Count -gt 0 -and [DateTimeOffset]::UtcNow -lt $deadline)

foreach ($target in $remaining) {
    $current = @(Get-RepositoryDebugProcess | Where-Object {
        $_.ProcessId -eq $target.ProcessId -and $_.StartedAtUtc -eq $target.StartedAtUtc
    })
    if ($current.Count -eq 0) {
        continue
    }

    Write-Warning "Repository Debug $($target.Role) PID $($target.ProcessId) exceeded the shutdown timeout; forcing that validated process to exit."
    Stop-Process -Id $target.ProcessId -Force -ErrorAction Stop
}

if ($remaining.Count -gt 0) {
    Start-Sleep -Milliseconds 500
}

$final = @(Get-RepositoryDebugProcess)
if ($final.Count -gt 0) {
    $details = $final | ForEach-Object { "{0} PID {1}" -f $_.Role, $_.ProcessId }
    throw "Repository Debug IFM processes remain after cleanup: $($details -join ', ')."
}

Write-Host "IFM repository Debug API/UI processes are stopped."
