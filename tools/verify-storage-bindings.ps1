param(
    [string]$PostgresTestConnection = $env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION,
    [switch]$IncludeContextRegression
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($PostgresTestConnection)) {
    throw 'Supply -PostgresTestConnection for a dedicated PostgreSQL test database, without credentials.'
}
if ($PostgresTestConnection -match '(?i)(username|user id|userid|password|pwd)\s*=') {
    throw 'Use a credential-free connection string and POSTGRES_TEST_KEY for credentials.'
}
if ([string]::IsNullOrWhiteSpace($env:POSTGRES_TEST_KEY)) {
    throw 'Set POSTGRES_TEST_KEY to the integration-test database credential JSON before running verification.'
}

$repo = Split-Path -Parent $PSScriptRoot
$savedEnvironment = @{}
foreach ($name in @('DOTNET_ENVIRONMENT', 'ASPNETCORE_ENVIRONMENT', 'IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
Push-Location $repo
try {
    $env:DOTNET_ENVIRONMENT = 'Test'
    $env:ASPNETCORE_ENVIRONMENT = 'Test'
    $env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = $PostgresTestConnection
    $results = Join-Path $repo '.artifacts/provider-binding-tests'
    dotnet test TomasAI.IFM.Framework.Storage.UnitTests/TomasAI.IFM.Framework.Storage.UnitTests.csproj `
        -m:4 --verbosity quiet --logger 'trx;LogFileName=provider-binding-unit.trx' --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw 'Storage unit tests failed.' }

    $filter = 'FullyQualifiedName~FrameworkStorage'
    if ($IncludeContextRegression) {
        $classes = @('PostgresTradePlanTests', 'PostgresScheduledTaskTests', 'PostgresTradeFlowTests',
            'CommandAuditPersistenceTests', 'MarkerProjectorPipelineTests', 'Stage4DurableIntentPostgresTests',
            'Accepted_analytic_sources_and_rows_are_atomic_and_reconstruct_identically_without_calculators',
            'PostgresLookupTests', 'PostgresAcceptedPortfolioTests', 'PostgresParameterAtomicTests',
            'PostgresBackupAtomicTests', 'PostgresSecuritiesReferenceTests',
            'PostgresSequenceIdGeneratorTests', 'PostgresSequenceRecoveryTests')
        foreach ($class in $classes) { $filter += "|FullyQualifiedName~$class" }
    }
    dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj `
        -m:4 --filter $filter --verbosity quiet --logger 'trx;LogFileName=provider-binding-verification.trx' --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw 'Storage binding integration tests failed. See the TRX results.' }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    Pop-Location
}
