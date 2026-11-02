param(
    [string]$InputFile = '.artifacts/iti-pilot.json',
    [string]$ArtifactDirectory = '.artifacts/iti-backtest',
    [string]$BuildArtifactDirectory = '.artifacts/iti-backtest-build',
    [switch]$NoBuild,
    [switch]$DailyComputeOnly
)
$ErrorActionPreference = 'Stop'
$inputPath = (Resolve-Path -LiteralPath $InputFile).Path
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
# The existing synthetic integration host requires this suffix. Historical
# prices are supplied directly to ITI; the synthetic feed is never started.
$database = "ifm_application_backtest_${runId}_synthetic_host"
$runDirectory = Join-Path $ArtifactDirectory $runId
New-Item -ItemType Directory -Force $runDirectory | Out-Null
$runInput = Join-Path $runDirectory 'input.json'
Copy-Item -LiteralPath $inputPath -Destination $runInput
$inputManifest = Get-Content -LiteralPath $inputPath -Raw | ConvertFrom-Json
foreach ($file in $inputManifest.files) {
    if ([IO.Path]::GetFileName($file.file) -ne $file.file) { throw 'Replay inputs must be adjacent CSV files.' }
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $inputPath) $file.file) -Destination (Join-Path $runDirectory $file.file)
}
$runInput = (Resolve-Path -LiteralPath $runInput).Path
$credential = [Environment]::GetEnvironmentVariable('POSTGRES_DEV_KEY')
if ([string]::IsNullOrWhiteSpace($credential)) {
    $credential = [Environment]::GetEnvironmentVariable('POSTGRES_DEV_KEY', 'User')
}
if ([string]::IsNullOrWhiteSpace($credential)) { throw 'POSTGRES_DEV_KEY is required to use the local backtest PostgreSQL server.' }
& "$PSScriptRoot/New-BacktestPostgres.ps1" -SourceDatabase ifm_application_backtest -TargetDatabase $database -ArtifactDirectory $runDirectory
$settings = @{
    'IFM_BACKTEST_POSTGRES_CONNECTION' = "Host=127.0.0.1;Port=5432;Database=$database;SSL Mode=Disable"
    'IFM_BACKTEST_INPUT' = $runInput
    # The fixture only needs an empty scratch keyspace; historical input comes
    # from the existing Scylla backtest keyspace. Cassandra avoids a second
    # Scylla process exhausting Docker's shared Linux AIO quota.
    'IFM_TEST_CQL_ENGINE' = 'Cassandra'
    'POSTGRES_DEV_KEY' = $credential
    'IFM_BACKTEST_DAILY_COMPUTE_ONLY' = $(if ($DailyComputeOnly) { '1' } else { '0' })
}
$previous = @{}
try {
    foreach ($name in $settings.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $settings[$name])
    }
    $testArguments = @('test', 'TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.csproj',
        '--artifacts-path', $BuildArtifactDirectory,
        '--filter', 'FullyQualifiedName~FuturesItiHistoricalBacktestTests', '--logger', 'trx', '--results-directory', (Join-Path $runDirectory 'tests'), '-v', 'minimal')
    if ($NoBuild) { $testArguments += '--no-build' }
    # Windows PowerShell treats native stderr as ErrorRecords. Retain the
    # complete xUnit diagnostics and inspect the exit code after it finishes.
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet @testArguments *> (Join-Path $runDirectory 'test.log')
        $testExit = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = 'Stop' }
    if ($testExit -eq 0) {
        $result = Get-Content -LiteralPath "$runInput.results.json" -Raw | ConvertFrom-Json
        $expectedHistory = ($result.generated_signals.PSObject.Properties.Value | Measure-Object -Sum).Sum
        $sql = "SELECT json_build_object('history_rows',(SELECT count(*) FROM market_data.futures_iti_signal),'timeframe_rows',(SELECT count(*) FROM market_data.futures_iti_timeframe_state),'command_rows',(SELECT count(*) FROM public.command_log),'event_rows',(SELECT count(*) FROM public.event_log),'order_rows',(SELECT count(*) FROM trade.trade_order))"
        $proof = & docker exec ifm_db psql -U postgres -d $database -Atc $sql
        if ($LASTEXITCODE -ne 0) { throw 'Could not verify persisted replay output.' }
        $proof | Set-Content (Join-Path $runDirectory 'persistence-proof.json')
        $counts = $proof | ConvertFrom-Json
        if ($counts.history_rows -ne $expectedHistory -or $counts.timeframe_rows -ne $result.expected_timeframe_rows -or $counts.order_rows -ne 0) {
            throw 'Persisted replay counts differ from the completed ITI signals.'
        }
    }
    @{
        run_id = $runId; postgres_database = $database; source_keyspace = 'trade_tick_quote_backtest_db'
        input = $runInput; results = "$runInput.results.json"; test_exit_code = $testExit
        completed_utc = [DateTime]::UtcNow.ToString('O')
    } | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'run.json')
    if ($testExit -ne 0) { throw "ITI replay failed. See $runDirectory/test.log. Database $database retained." }
    & "$PSScriptRoot/Get-ItiBacktestReport.ps1" -RunDirectory $runDirectory
    Write-Output "ITI replay passed. Results: $runInput.results.json; PostgreSQL: $database"
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
