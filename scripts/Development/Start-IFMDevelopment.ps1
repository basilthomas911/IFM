[CmdletBinding()]
param(
    [ValidateSet("Debug")]
    [string] $Configuration = "Debug",
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$getProcessScript = Join-Path $PSScriptRoot "Get-IFMDevelopmentProcess.ps1"
$stopScript = Join-Path $PSScriptRoot "Stop-IFMDevelopment.ps1"

$existing = @(& $getProcessScript -RepositoryRoot $repositoryRoot)
if ($existing.Count -gt 0) {
    & $stopScript -VerifyStopped
}

if (-not $NoBuild) {
    $projects = @(
        "TomasAI.IFM.Application.Api.Server\TomasAI.IFM.Application.Api.Server.csproj",
        "TomasAI.IFM.UI.Net\TomasAI.IFM.UI.Net.csproj",
        "TomasAI.IFM.Application.ServerManager\TomasAI.IFM.Application.ServerManager.csproj"
    )
    foreach ($project in $projects) {
        & dotnet build (Join-Path $repositoryRoot $project) --configuration $Configuration -m:1 --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for '$project'."
        }
    }
}

if (-not $NoBuild) {
    & dotnet publish (Join-Path $repositoryRoot "TomasAI.IFM.Application.ServerManager.SchedulerHost/TomasAI.IFM.Application.ServerManager.SchedulerHost.csproj") -m:1 -c Debug --no-self-contained -o (Join-Path $repositoryRoot ".artifacts/scheduled-tasks/development/Host") --nologo
    if ($LASTEXITCODE -ne 0) { throw "Scheduler Host publish failed." }
}

$managerPath = Join-Path $repositoryRoot `
    "TomasAI.IFM.Application.ServerManager\bin\Debug\net10.0-windows7.0\IFMServerManager.exe"
if (-not (Test-Path -LiteralPath $managerPath)) {
    throw "Development Server Manager was not found at '$managerPath'. Build without -NoBuild first."
}

# Run the AWS cutover controller outside Server Manager's application job so it can
# stop/restart that job during an approved database recovery.
$cutoverScript = Join-Path $repositoryRoot "scripts/AwsBackup/Start-DevelopmentDatabaseCutoverWorker.ps1"
$cutoverBinary = Join-Path $repositoryRoot ".artifacts/database-recovery/windows-cutover-worker/TomasAI.IFM.Api.DatabaseBackup.Host.exe"
if ((Test-Path -LiteralPath $cutoverScript) -and (Test-Path -LiteralPath $cutoverBinary)) {
    try {
        $nativeBackupHost = docker inspect --format '{{.State.Running}}' ifm-database-backup-development-database-backup-host-1 2>$null
        if ($LASTEXITCODE -eq 0 -and $nativeBackupHost -eq 'true') {
            & powershell.exe -NoProfile -File $cutoverScript -NoBuild
            if ($LASTEXITCODE -ne 0) { Write-Warning 'AWS database cutover controller is unavailable; inspect its persistent logs before restoring.' }
        }
    } catch { Write-Warning 'AWS database cutover controller is unavailable; inspect its persistent logs before restoring.' }
}

$priorEnvironment = [Environment]::GetEnvironmentVariable("DOTNET_ENVIRONMENT")
$priorRepositoryRoot = [Environment]::GetEnvironmentVariable("IFM_REPOSITORY_ROOT")
try {
    [Environment]::SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development")
    [Environment]::SetEnvironmentVariable("IFM_REPOSITORY_ROOT", $repositoryRoot)
    & $managerPath
}
finally {
    [Environment]::SetEnvironmentVariable("DOTNET_ENVIRONMENT", $priorEnvironment)
    [Environment]::SetEnvironmentVariable("IFM_REPOSITORY_ROOT", $priorRepositoryRoot)
}
