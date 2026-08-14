[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $NoBuild,

    [switch] $ListOnly,

    [string] $Filter
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "TomasAI.IFM.sln"

$solutionProjects = & dotnet sln $solutionPath list
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate projects in $solutionPath."
}

$integrationProjects = $solutionProjects |
    Where-Object { $_ -match '\.csproj$' } |
    ForEach-Object { Join-Path $repositoryRoot $_.Trim() } |
    Where-Object {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($_)
        $projectName.EndsWith("IntegrationTests", [System.StringComparison]::Ordinal) -or
            $projectName.EndsWith("IntegratedTests", [System.StringComparison]::Ordinal)
    } |
    Sort-Object

if ($integrationProjects.Count -eq 0) {
    throw "No integration-test projects were found in $solutionPath."
}

if ($ListOnly) {
    $integrationProjects | ForEach-Object {
        [System.IO.Path]::GetFileNameWithoutExtension($_)
    }
    return
}

foreach ($projectPath in $integrationProjects) {
    Write-Host "Running $([System.IO.Path]::GetFileNameWithoutExtension($projectPath))"
    $arguments = @("test", $projectPath, "--configuration", $Configuration)
    if ($NoBuild) {
        $arguments += "--no-build"
    }
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @("--filter", $Filter)
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
