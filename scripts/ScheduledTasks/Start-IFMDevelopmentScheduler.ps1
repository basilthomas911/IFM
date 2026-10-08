[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$deploymentRoot = Join-Path $repositoryRoot ".artifacts/scheduled-tasks/development"
$settings = Join-Path $deploymentRoot "scheduler.settings.json"
$executable = Join-Path $deploymentRoot "Host/TomasAI.IFM.Application.ServerManager.SchedulerHost.exe"
if (-not (Test-Path -LiteralPath $settings) -or -not (Test-Path -LiteralPath $executable)) { throw "Run scripts/ScheduledTasks/Install-IFMDevelopmentScheduler.ps1 first." }
$existing = @(Get-CimInstance Win32_Process -Filter "Name='TomasAI.IFM.Application.ServerManager.SchedulerHost.exe'" | Where-Object { $_.ExecutablePath -eq $executable })
if ($existing.Count -gt 0) { Write-Output "Development scheduler already running."; return }
$priorEnvironment = $env:DOTNET_ENVIRONMENT
$env:DOTNET_ENVIRONMENT = "Development"
try {
$process = Start-Process -FilePath $executable -ArgumentList @("--settings", ('"' + $settings + '"')) -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $deploymentRoot "host.stdout.log") -RedirectStandardError (Join-Path $deploymentRoot "host.stderr.log")
} finally { $env:DOTNET_ENVIRONMENT = $priorEnvironment }
Start-Sleep -Seconds 2
if ($process.HasExited) { throw "Scheduler host exited at startup; inspect $deploymentRoot/host.stderr.log." }
@{ ProcessId = $process.Id; StartedAtUtc = ([datetimeoffset]$process.StartTime.ToUniversalTime()).ToString("O"); ExecutablePath = $executable } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $deploymentRoot "scheduler-session.json") -Encoding UTF8
Write-Output "Started development scheduler PID $($process.Id); reconciliation and readiness are reported in System Admin."
