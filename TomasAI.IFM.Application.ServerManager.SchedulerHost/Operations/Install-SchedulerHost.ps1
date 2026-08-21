[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [string] $ServiceName = 'IFMSchedulerHost',
    [string] $ServiceAccount = 'NT SERVICE\IFMSchedulerHost',
    [string] $OperatorGroup = 'IFM Scheduler Operators',
    [string] $TaskRunRoot = "$env:ProgramData\TomasAI\IFM\ServerManager\TaskRuns",
    [switch] $StartService
)

$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Scheduler Host installation requires an elevated PowerShell session.'
}

$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
$executable = Join-Path $publishRoot 'TomasAI.IFM.Application.ServerManager.SchedulerHost.exe'
$configuration = Join-Path $publishRoot 'appsettings.json'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Scheduler Host executable not found: $executable" }
if (-not (Test-Path -LiteralPath $configuration -PathType Leaf)) { throw "Scheduler Host configuration not found: $configuration" }
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { throw "Service '$ServiceName' already exists. Use the documented upgrade procedure." }

if (-not (Get-LocalGroup -Name $OperatorGroup -ErrorAction SilentlyContinue)) {
    New-LocalGroup -Name $OperatorGroup -Description 'Local operators approved to use IFM Scheduler Host controls.' | Out-Null
}

New-Item -ItemType Directory -Path $TaskRunRoot -Force | Out-Null
& icacls.exe $publishRoot /inheritance:r /grant:r "${ServiceAccount}:(OI)(CI)RX" "BUILTIN\Administrators:(OI)(CI)F" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to secure the Scheduler Host publish directory.' }
& icacls.exe $TaskRunRoot /inheritance:r /grant:r "${ServiceAccount}:(OI)(CI)M" "${OperatorGroup}:(OI)(CI)R" "BUILTIN\Administrators:(OI)(CI)F" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to secure the Scheduler Host task-run directory.' }

$binaryPath = '"' + $executable + '" --environment Production'
& sc.exe create $ServiceName binPath= $binaryPath start= delayed-auto obj= $ServiceAccount DisplayName= 'IFM Scheduler Host'
if ($LASTEXITCODE -ne 0) { throw 'Windows service creation failed.' }
& sc.exe description $ServiceName 'Owns durable IFM Quartz schedules and supervised scheduled-task processes.' | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null
& sc.exe failureflag $ServiceName 1 | Out-Null
& sc.exe sidtype $ServiceName restricted | Out-Null

if ($StartService) {
    Start-Service -Name $ServiceName
}

Write-Output "Installed '$ServiceName'. Validate PostgreSQL grants and run Test-SchedulerHostAcceptance.ps1 before enabling any schedule."
