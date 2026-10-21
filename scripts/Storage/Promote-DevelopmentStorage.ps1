[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$proofRoot = Join-Path $root '.artifacts/storage-cutover'
$rows = Get-Content (Join-Path $proofRoot 'final-development-restore-row-proof-20261012.json') -Raw | ConvertFrom-Json
$metadata = Get-Content (Join-Path $proofRoot 'final-development-restore-metadata-proof-20261012.json') -Raw | ConvertFrom-Json
$archive = Get-Content (Join-Path $proofRoot 'final-development-archive-20261012.json') -Raw | ConvertFrom-Json
if ($rows.sourceDatabase -ne 'ifm_application_development' -or $rows.restoredDatabase -ne 'ifm_storage_final_restore_20261012' -or $rows.tablesVerified -ne 287 -or -not $rows.sourceUnchanged) { throw 'Full final Development restore row proof is required.' }
if ($metadata.sourceDatabase -ne $rows.sourceDatabase -or $metadata.sequenceStatesVerified -ne 31 -or -not $metadata.sourceUnchanged) { throw 'Final schema and sequence proof is required.' }
$archivePath = Join-Path $root $archive.archive
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $archive.sha256) { throw 'Final archive checksum differs.' }
$running = @(& (Join-Path $root 'scripts/Development/Get-IFMDevelopmentProcess.ps1'))
if ($running.Count -gt 0) { throw 'Stop IFM Development processes before changing storage connections.' }
$path = Join-Path $root 'TomasAI.IFM.Application.Api.Server/appsettings.Development.json'
$rollback = Join-Path $proofRoot 'appsettings.Development.prepromotion-20261012.json'
if (Test-Path -LiteralPath $rollback) { throw 'Preserve the existing rollback settings; promotion must not overwrite them.' }
$settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$connection = $settings.ConnectionStrings.EventSourceActorDbConnection
if ($connection -notmatch '(?i)Database\s*=\s*event-source-dev-db(?:;|$)') { throw 'Unexpected Development source database.' }
$connection = $connection -replace '(?i)Database\s*=\s*event-source-dev-db', 'Database=ifm_application_development'
Copy-Item -LiteralPath $path -Destination $rollback
$settings | Add-Member -Force NoteProperty Storage (@{ PostgresConnection = $connection })
foreach ($key in @('EventSourceActorDbConnection','ConfigurationDbConnection','MarketDataServiceDbConnection','SystemAdminDbConnection','SequenceIdDbConnection','PortfolioDbConnection','MarketDataDbConnection','ReferenceDbConnection','SecuritiesDbConnection','TradeDbConnection','TradePlanDbConnection','ScheduledTaskDbConnection')) {
    $settings.ConnectionStrings | Add-Member -Force NoteProperty $key $connection
}
$settings.ConnectionStrings | Add-Member -Force NoteProperty TradeTickQuoteDbConnection 'Contact Points=127.0.0.1;Default Keyspace=trade_tick_quote_test_db'
$settings | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
Write-Output 'Development storage promoted: one PostgreSQL database and one raw ScyllaDB keyspace. Original databases and rollback settings are preserved.'
