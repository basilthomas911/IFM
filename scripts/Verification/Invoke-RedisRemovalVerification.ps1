[CmdletBinding()]
param(
    [ValidateSet('UnitBdd', 'Integration')][string] $Suite = 'UnitBdd',
    [string] $ResultsRoot = 'E:\IFM\RedisRemoval\20261108\tests',
    [string[]] $ExcludeProject = @(),
    [string[]] $OnlyProject = @()
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projects = if ($Suite -eq 'UnitBdd') {
    @('Framework.Caching.UnitTests', 'Application.MarketData.UnitTests', 'Domain.Application.UnitTests',
      'Domain.Reference.UnitTests', 'Domain.Reference.BDDTests', 'Domain.MarketData.Feed.UnitTests',
      'Domain.MarketData.Feed.BDDTests', 'Domain.MarketData.Analytics.UnitTests', 'Domain.MarketData.Analytics.BDDTests',
      'Domain.MarketData.UnitTests', 'Domain.MarketData.Securities.UnitTests', 'Domain.MarketData.Securities.BDDTests',
      'Domain.Trade.UnitTests', 'Domain.Trade.BDDTests', 'Domain.Portfolio.UnitTests', 'Domain.Portfolio.BDDTests',
      'Domain.BrokerAccount.UnitTests', 'Domain.BrokerAccount.BDDTests', 'Domain.SystemAdmin.UnitTests',
      'Domain.SystemAdmin.BDDTests', 'Application.MarketData.Rust.Host.Tests', 'Framework.Storage.DatabaseBackup.AwsCloud.UnitTests')
} else {
    @('Application.Storage.IntegrationTests', 'Domain.Reference.IntegrationTests', 'Domain.MarketData.Feed.IntegrationTests',
      'Domain.MarketData.Feed.Recovery.IntegrationTests', 'Domain.MarketData.Analytics.IntegrationTests',
      'Domain.MarketData.IntegrationTests', 'Domain.MarketData.Securities.IntegrationTests', 'Domain.Trade.IntegratedTests',
      'Domain.Portfolio.IntegrationTests', 'Domain.BrokerAccount.IntegrationTests', 'Domain.SystemAdmin.IntegrationTests',
      'LivePipeline.IntegrationTests')
}
New-Item -ItemType Directory -Force -Path $ResultsRoot | Out-Null
$results = @()
foreach ($name in $projects) {
    if ($name -in $ExcludeProject) { continue }
    if ($OnlyProject.Count -gt 0 -and $name -notin $OnlyProject) { continue }
    $projectName = "TomasAI.IFM.$name"
    $project = Join-Path $repositoryRoot "$projectName/$projectName.csproj"
    if (-not (Test-Path -LiteralPath $project)) {
        $candidates = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $projectName) -Filter '*.csproj' -File)
        if ($candidates.Count -ne 1) { throw "Cannot resolve the test project for $projectName" }
        $project = $candidates[0].FullName
    }
    $log = Join-Path $ResultsRoot "$name.log"
    Write-Output "Running $projectName"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $previousIsolatedNats = $env:IFM_STAGE3_ISOLATED_NATS
    if ($name -eq 'Domain.MarketData.Feed.IntegrationTests') { $env:IFM_STAGE3_ISOLATED_NATS = '1' }
    if ($name -eq 'Domain.MarketData.Feed.Recovery.IntegrationTests') {
        # This suite launches the native audit publisher and requires its own
        # PostgreSQL/NATS fixture; it must never inherit Development endpoints.
        & (Join-Path $repositoryRoot 'scripts/Test-MarketDataRecoveryAudit.ps1') `
            -ResultsDirectory (Join-Path $ResultsRoot 'recovery-audit') *> $log
    } elseif ($name -eq 'LivePipeline.IntegrationTests') {
        & (Join-Path $PSScriptRoot 'Test-RedisFreeLivePipeline.ps1') `
            -ResultsDirectory (Join-Path $ResultsRoot 'livepipeline') *> $log
    } else {
        & dotnet test $project -m:2 --nologo -v:q -p:NuGetAudit=false `
            --logger "trx;LogFileName=$name.trx" --results-directory $ResultsRoot `
            --blame-hang-timeout 3m --blame-hang-dump-type mini *> $log
    }
    $code = $LASTEXITCODE
    if ($name -eq 'Domain.MarketData.Feed.IntegrationTests') { $env:IFM_STAGE3_ISOLATED_NATS = $previousIsolatedNats }
    $results += [pscustomobject]@{ Project = $projectName; ExitCode = $code; Seconds = $timer.Elapsed.TotalSeconds; Log = $log }
    $results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $ResultsRoot "$Suite-summary.json")
    Write-Output "$projectName completed with exit code $code in $([math]::Round($timer.Elapsed.TotalSeconds, 1))s"
}
if (@($results | Where-Object ExitCode -ne 0).Count -gt 0) { exit 1 }
