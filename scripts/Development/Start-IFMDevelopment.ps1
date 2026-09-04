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
        & dotnet build (Join-Path $repositoryRoot $project) --configuration $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for '$project'."
        }
    }
}

$managerPath = Join-Path $repositoryRoot `
    "TomasAI.IFM.Application.ServerManager\bin\Debug\net10.0-windows7.0\IFMServerManager.exe"
if (-not (Test-Path -LiteralPath $managerPath)) {
    throw "Development Server Manager was not found at '$managerPath'. Build without -NoBuild first."
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
