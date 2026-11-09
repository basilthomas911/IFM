[CmdletBinding()]
param([switch] $SourceOnly)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repositoryRoot
try {
    $violations = @()
    $files = @(& rg --files -g '*.cs' -g '*.csproj' -g 'appsettings*.json' -g '!TWS API/**' |
        Where-Object { $_ -match '^TomasAI.IFM\.' -and $_ -notmatch '(?i)(Tests|Benchmarks)[\\/]' })
    foreach ($file in $files) {
        $source = [IO.File]::ReadAllText((Join-Path $repositoryRoot $file))
        if ($source -match '\b(IBlackboardService|BlackboardService|IRedisCache)\b|StackExchange\.Redis|Framework\.Caching\.Redis|"RedisUri"|"RedisConnection"') {
            $violations += $file
        }
    }
    foreach ($retired in @('TomasAI.IFM.Application.BlackBoard/TomasAI.IFM.Application.Blackboard.csproj',
            'TomasAI.IFM.Framework.Caching.Redis/TomasAI.IFM.Framework.Caching.Redis.csproj', 'Docker/Redis/docker-compose.yml')) {
        if (Test-Path -LiteralPath $retired) { $violations += $retired }
    }
    $dependencies = @()
    if (-not $SourceOnly) {
        foreach ($path in @('TomasAI.IFM.Application.Api.Server/bin/Debug/net10.0/TomasAI.IFM.Application.Api.Server.deps.json',
                '.artifacts/rust-marketdata-package/Debug/TomasAI.IFM.Application.MarketData.Host.deps.json',
                '.artifacts/database-recovery/windows-cutover-worker/TomasAI.IFM.Api.DatabaseBackup.Host.deps.json')) {
            if (-not (Test-Path -LiteralPath $path)) { throw "Required deployment dependencies are missing: $path" }
            $dependency = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $names = @($dependency.libraries.PSObject.Properties.Name)
            if ($names -match 'Blackboard|Caching\.Redis|StackExchange\.Redis') { $violations += $path }
            $dependencies += $path
        }
    }
    [pscustomobject]@{ ProductionFiles = $files.Count; DeploymentManifests = $dependencies; Violations = $violations } | ConvertTo-Json -Depth 5
    if ($violations.Count -gt 0) { throw 'Redis/Blackboard production dependency audit failed.' }
} finally { Pop-Location }
