param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$run = Get-Content -LiteralPath (Join-Path $RunDirectory 'run.json') -Raw | ConvertFrom-Json
if ($run.test_exit_code -ne 0 -or $run.postgres_database -notmatch '^ifm_application_backtest_[a-f0-9]{12}_synthetic_host$') {
    throw 'A completed, isolated backtest run is required.'
}
$database = $run.postgres_database
$sql = @"
SELECT json_build_object(
 'database',current_database(),
 'by_period_and_mode',(SELECT json_agg(row_to_json(s)) FROM (
   SELECT timeperiod,intrinsictimemode,count(*) AS events,min(bandlevel) AS min_band_level,max(bandlevel) AS max_band_level
   FROM market_data.futures_iti_signal GROUP BY timeperiod,intrinsictimemode ORDER BY timeperiod,intrinsictimemode) s),
 'overshoot_events',(SELECT count(*) FROM market_data.futures_iti_signal WHERE intrinsictimemode IN ('TrendOvershootExtended','TrendOvershootReversal')),
 'invalid_overshoot_levels',(SELECT count(*) FROM market_data.futures_iti_signal WHERE intrinsictimemode IN ('TrendOvershootExtended','TrendOvershootReversal') AND (bandlevel IS NULL OR NOT (bandlevel>1 AND bandlevel<'Infinity'::double precision))),
 'timeframe_states',(SELECT count(*) FROM market_data.futures_iti_timeframe_state),
 'first_signal',(SELECT min(intrinsictime) FROM market_data.futures_iti_signal),
 'last_signal',(SELECT max(intrinsictime) FROM market_data.futures_iti_signal))
"@
$report = & docker exec ifm_db psql -U postgres -d $database -Atc $sql
if ($LASTEXITCODE -ne 0) { throw 'Could not query the persisted signal report.' }
$parsed = $report | ConvertFrom-Json
if ($parsed.invalid_overshoot_levels -ne 0) { throw 'An overshoot event has an invalid BandLevel.' }
$report | Set-Content -Encoding UTF8 (Join-Path $RunDirectory 'overshoot-summary.json')
$csv = "COPY (SELECT sequenceid,valuedate,timeperiod,intrinsictime,intrinsictimetrend,intrinsictimemode,intrinsicprice,trendprice,trendextreme,trendreversal,threshold,bandlevel,reversallevel FROM market_data.futures_iti_signal WHERE intrinsictimemode IN ('TrendOvershootExtended','TrendOvershootReversal') ORDER BY intrinsictime,timeperiod,sequenceid) TO STDOUT WITH CSV HEADER"
& docker exec ifm_db psql -U postgres -d $database -c $csv | Set-Content -Encoding UTF8 (Join-Path $RunDirectory 'overshoot-events.csv')
if ($LASTEXITCODE -ne 0) { throw 'Could not export the overshoot events.' }
$parsed.by_period_and_mode | Format-Table -AutoSize
Write-Output "Verified $($parsed.overshoot_events) persisted overshoots; report: $RunDirectory/overshoot-summary.json"
