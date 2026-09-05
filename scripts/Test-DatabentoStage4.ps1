[CmdletBinding()]
param([switch]$IncludePostgres)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot 'artifacts/Stage4Acceptance'

function Assert-NonemptyStage4Run([string]$fileName) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $resultsDirectory $fileName)
    if ([int]$result.TestRun.ResultSummary.Counters.executed -lt 1) {
        throw "No tests executed in $fileName; an empty run is not qualification."
    }
}

Push-Location $repositoryRoot
try {
    # Offline implemented subset only. This is not a live-enable or full Stage 4 acceptance command.
    dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj `
        --no-restore --filter 'FullyQualifiedName~Stage4&FullyQualifiedName!~Stage4SubscriptionBoundsTests' `
        --verbosity quiet -m:1 -nr:false --logger 'trx;LogFileName=stage4-contracts.trx' --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Stage 4 contract/coordinator/lifecycle qualification failed.' }
    Assert-NonemptyStage4Run 'stage4-contracts.trx'

    # Isolate process-allocation measurements from concurrent tests.
    dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj `
        --no-build --no-restore --filter FullyQualifiedName~Stage4SubscriptionBoundsTests `
        --logger 'console;verbosity=normal' --logger 'trx;LogFileName=stage4-bounds.trx' `
        --results-directory $resultsDirectory -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Stage 4 shared-chain allocation qualification failed.' }
    Assert-NonemptyStage4Run 'stage4-bounds.trx'

    if ($IncludePostgres) {
        # The fixture must independently verify the dedicated local test DB and isolate its rows.
        dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
            --no-restore --filter FullyQualifiedName~Stage4 `
            --verbosity quiet -m:1 -nr:false --logger 'trx;LogFileName=stage4-postgres-intent.trx' --results-directory $resultsDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Stage 4 isolated PostgreSQL qualification failed.' }
        Assert-NonemptyStage4Run 'stage4-postgres-intent.trx'
    }
    Write-Host 'Implemented offline Stage 4 subset passed. Worker option routing, pricing/composer integration and live acceptance remain separate gates.'
}
finally { Pop-Location }
