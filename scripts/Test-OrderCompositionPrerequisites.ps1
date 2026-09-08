[CmdletBinding()]
param(
    [switch]$Live,
    [switch]$SustainedLoad,
    [ValidatePattern('^[a-f0-9]{64}$')][string]$BundleId,
    [ValidateRange(30, 1800)][int]$SoakSeconds = 1800
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot ('.test-results/ocp/run-' + [guid]::NewGuid().ToString('N'))
$output = 'bin/ocp-verification/'
$savedEnvironment = @{}
$variables = @('IFM_OCP_LIVE', 'IFM_OCP_EVIDENCE_DIRECTORY', 'IFM_OCP_BUNDLE_ID',
    'IFM_OCP_WORKER_ASSEMBLY', 'IFM_OCP_SOAK_SECONDS', 'IFM_OCP_LOAD', 'IFM_OCP_LOAD_SECONDS', 'IFM_OCP_LOAD_EVIDENCE')
foreach ($name in $variables) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

function Test-Prerequisite([string]$project, [string]$filter, [string]$name) {
    dotnet test $project --no-restore "-p:BaseOutputPath=$output" -m:4 --verbosity quiet `
        --filter $filter --logger "trx;LogFileName=$name.trx" --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw "$name failed. Evidence: $resultsDirectory" }
    [xml]$result = Get-Content -LiteralPath (Join-Path $resultsDirectory "$name.trx")
    if ([int]$result.TestRun.ResultSummary.Counters.executed -lt 1) { throw "$name executed no tests." }
}

Push-Location $repositoryRoot
try {
    New-Item -ItemType Directory -Path $resultsDirectory | Out-Null
    $env:IFM_OCP_LIVE = '0'; $env:IFM_OCP_LOAD = '0'
    Test-Prerequisite 'TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj' `
        'FullyQualifiedName~OrderComposition|FullyQualifiedName~ReviewedEsOptionReference|FullyQualifiedName~OptionPremiumTick|FullyQualifiedName~UsTreasury|FullyQualifiedName~DatasetWorker|FullyQualifiedName~Stage4SubscriptionBounds' 'market'
    Test-Prerequisite 'TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj' `
        'FullyQualifiedName~OptionPricingConventionScyllaTests' 'reference-storage'
    Test-Prerequisite 'TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj' `
        'FullyQualifiedName~CompositionBusinessProjectionTests&FullyQualifiedName!~Published_reference_live_worker&FullyQualifiedName!~Inspect_live_reference' 'committed-ownership'
    if ($SustainedLoad) {
        $env:IFM_OCP_LOAD = '1'; $env:IFM_OCP_LOAD_SECONDS = "$SoakSeconds"
        $env:IFM_OCP_LOAD_EVIDENCE = Join-Path $resultsDirectory 'managed-load.json'
        Test-Prerequisite 'TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj' `
            'FullyQualifiedName~One_hundred_supervised_reset' 'supervised-recovery'
        Test-Prerequisite 'TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj' `
            'FullyQualifiedName~Frequent_health_observations' 'diagnostic-stress'
        # A separate test process keeps allocation measurements free from parallel recovery tests.
        Test-Prerequisite 'TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj' `
            'FullyQualifiedName~Maximum_scope_sustained' 'managed-load'
        $env:IFM_OCP_LOAD = '0'
    }
    if ($Live) {
        if (!$BundleId -or !$env:DATABENTO_API_KEY) { throw 'Live qualification requires an exact published BundleId and DATABENTO_API_KEY.' }
        $env:IFM_OCP_LIVE = '1'; $env:IFM_OCP_BUNDLE_ID = $BundleId
        $env:IFM_OCP_EVIDENCE_DIRECTORY = $resultsDirectory; $env:IFM_OCP_SOAK_SECONDS = "$SoakSeconds"
        $env:IFM_OCP_WORKER_ASSEMBLY = Join-Path $repositoryRoot "TomasAI.IFM.Application.MarketData.Worker/${output}Debug/net10.0/TomasAI.IFM.Application.MarketData.Worker.dll"
        Test-Prerequisite 'TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj' `
            'FullyQualifiedName~Published_reference_live_worker' 'live-pricing-recovery'
    }
    Write-Host "Requested prerequisite checks passed. Evidence: $resultsDirectory"
    Write-Host 'Bounded qualification does not certify a complete trading session, another platform, broker execution or the future composer.'
}
finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    Pop-Location
}
