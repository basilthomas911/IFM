# Builds isolated outputs and enforces the project, compiled-reference and public-port gates.
[CmdletBinding()]
param([string]$ArtifactsPath = "$PSScriptRoot/../.artifacts/layering-verification")
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifacts = [System.IO.Path]::GetFullPath($ArtifactsPath)
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE." }
}
Push-Location $repo
try {
    Invoke-Checked python @('scripts/Architecture/audit_layer_dependencies.py', '--strict', '--output', "$artifacts/project-graph.json")
    Invoke-Checked dotnet @('build', 'TomasAI.IFM.sln', '--artifacts-path', $artifacts, '-m:4', '-v:quiet')
    # These repository projects are not members of the supported solution, but are still audited.
    foreach ($project in @('TomasAI.IFM.Framework.Statistics.Markov','TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers')) {
        Invoke-Checked dotnet @('build', "$project/$project.csproj", '--artifacts-path', $artifacts, '-m:4', '-v:quiet')
    }
    Invoke-Checked dotnet @('run', '--project', 'tools/LayerAssemblyAudit/LayerAssemblyAudit.csproj', '--artifacts-path', "$artifacts/audit-tool", '--', "$artifacts/bin", "$artifacts/compiled-references.json", $repo)
    Invoke-Checked dotnet @('run', '--project', 'tools/PersistedEventIdentityInventory/PersistedEventIdentityInventory.csproj', '--artifacts-path', "$artifacts/port-tool", '--', '--qualify-public-ports', "$artifacts/public-ports.json")
    Write-Output 'PASS: dependency direction, complete compiled assembly coverage and consumed public contract signatures.'
}
finally { Pop-Location }
