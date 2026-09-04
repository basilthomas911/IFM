[CmdletBinding()]
param(
    [switch]$IncludePostgres
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    & "$PSScriptRoot\Test-DatabentoLifecycleOwnership.ps1"
    if (-not $?) { throw "Lifecycle ownership gate failed." }

    dotnet test `
        TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj `
        --no-restore `
        --filter "DatasetWorkerProcessSupervisorTests|DatasetIncidentStateMachineTests|MarketDataOperationsHealthServiceTests|DatabentoResiliencyTests"
    if ($LASTEXITCODE -ne 0) { throw "Stage 3 managed qualification failed." }

    dotnet test `
        TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj `
        --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Databento native/managed unit qualification failed." }

    if ($IncludePostgres) {
        if (-not (Test-Path Env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION)) {
            throw "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION is required with -IncludePostgres."
        }
        dotnet test `
            TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
            --no-restore `
            --filter "FullyQualifiedName~MarketDataServicePostgresIntegrationTests"
        if ($LASTEXITCODE -ne 0) { throw "Stage 3 PostgreSQL qualification failed." }
    }

    Write-Host "Databento Stage 3 qualification passed."
}
finally {
    Pop-Location
}
