[CmdletBinding()]
param(
    [switch]$IncludePostgres,
    [switch]$IncludeWindowsUi,
    [switch]$IncludeIsolatedNats
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$previousIsolatedNats = $env:IFM_STAGE3_ISOLATED_NATS

Push-Location $repositoryRoot
try {
    & "$PSScriptRoot\Test-DatabentoLifecycleOwnership.ps1"
    if (-not $?) { throw "Lifecycle ownership gate failed." }

    dotnet test `
        TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj `
        --no-restore `
        --filter "DatasetWorker|DatasetDesiredSubscription|DatasetPublicationGenerationFence|SupervisedHostPublisherLifecycle|DatasetIncidentStateMachineTests|MarketDataOperations|DatabentoResiliencyTests" `
        --verbosity quiet -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Stage 3 managed qualification failed." }

    dotnet test `
        TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj `
        --no-restore --verbosity quiet -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Databento native/managed unit qualification failed." }

    dotnet test `
        TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.csproj `
        --no-restore `
        --filter "FullyQualifiedName~TickAggregationEventPublisherRealtimeTests|FullyQualifiedName~BoundedTickAggregationPublisherTests" `
        --verbosity quiet -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Host realtime publisher qualification failed." }

    dotnet test `
        TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.csproj `
        --no-restore --filter "FullyQualifiedName~MarketOutlookUpdateProcessorTests" --verbosity quiet -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Market Outlook processing/instrumentation qualification failed." }

    dotnet test `
        TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj `
        --no-restore --filter "FullyQualifiedName~MarketDataOperationsHealthTests" --verbosity quiet -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "Operations-health query/presentation qualification failed." }

    if ($IncludeWindowsUi) {
        if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw "-IncludeWindowsUi requires Windows." }
        dotnet test `
            TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj `
            --no-restore --filter "FullyQualifiedName~MarketDataOperationsHealthRenderingTests" --verbosity quiet -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw "Operations-health WinForms rendering qualification failed." }
    }

    if ($IncludeIsolatedNats) {
        # Opt-in test owns a new loopback-only broker and removes only that exact container.
        # Requires Docker and the already-cached nats:2.12.0-alpine image; no image pull occurs.
        $env:IFM_STAGE3_ISOLATED_NATS = '1'
        dotnet test `
            TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.csproj `
            --no-restore --filter "FullyQualifiedName~Stage3NatsOutageTests" --verbosity quiet -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw "Isolated Core NATS outage/recovery qualification failed." }
    }

    if ($IncludePostgres) {
        if (-not (Test-Path Env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION)) {
            throw "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION is required with -IncludePostgres."
        }
        dotnet test `
            TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
            --no-restore `
            --filter "FullyQualifiedName~MarketDataServicePostgresIntegrationTests" --verbosity quiet -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw "Stage 3 PostgreSQL qualification failed." }
    }

    Write-Host "Databento Stage 3 synthetic qualification passed; live/platform acceptance is a separate gate."
}
finally {
    $env:IFM_STAGE3_ISOLATED_NATS = $previousIsolatedNats
    Pop-Location
}
