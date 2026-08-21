[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param([string] $ServiceName = 'IFMSchedulerHost')

$ErrorActionPreference = 'Stop'
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) { Write-Output "Service '$ServiceName' is not installed."; return }
if ($PSCmdlet.ShouldProcess($ServiceName, 'stop and remove Scheduler Host service registration')) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        (Get-Service -Name $ServiceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
    }
    & sc.exe delete $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unable to remove service '$ServiceName'." }
    Write-Output "Removed service registration. PostgreSQL state and task-run evidence were retained."
}
