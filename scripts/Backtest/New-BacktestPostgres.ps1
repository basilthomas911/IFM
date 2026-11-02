param(
    [string]$Container = 'ifm_db',
    [string]$SourceDatabase = 'ifm_application_development',
    [string]$TargetDatabase = 'ifm_application_backtest',
    [string]$ArtifactDirectory = '.artifacts/backtest-postgres'
)
$ErrorActionPreference = 'Stop'
if ($TargetDatabase -notmatch '^ifm_application_backtest(?:_[a-f0-9]{12}_synthetic_host)?$' -or
    $SourceDatabase -notmatch '^[a-z0-9_]+$') {
    throw 'Only the isolated backtest database namespace is permitted.'
}
New-Item -ItemType Directory -Force $ArtifactDirectory | Out-Null
$exists = & docker exec $Container psql -U postgres -Atc "SELECT 1 FROM pg_database WHERE datname='$TargetDatabase'"
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect PostgreSQL.' }
if ($exists -eq '1') { throw "Target $TargetDatabase already exists; it will not be replaced." }
$dump = "/tmp/$TargetDatabase-schema.dump"
& docker exec $Container pg_dump -U postgres -d $SourceDatabase --schema-only --no-owner --no-privileges -Fc -f $dump
if ($LASTEXITCODE -ne 0) { throw 'Schema export failed.' }
& docker exec $Container createdb -U postgres -T template0 $TargetDatabase
if ($LASTEXITCODE -ne 0) { throw 'Database creation failed.' }
& docker exec $Container pg_restore -U postgres -d $TargetDatabase --no-owner --no-privileges --exit-on-error $dump
if ($LASTEXITCODE -ne 0) { throw 'Schema restoration failed; the target is retained for inspection.' }
$query = "SELECT schemaname||'.'||tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY 1"
$sourceTables = @(& docker exec $Container psql -U postgres -d $SourceDatabase -Atc $query)
if ($LASTEXITCODE -ne 0) { throw 'Source inventory failed.' }
$targetTables = @(& docker exec $Container psql -U postgres -d $TargetDatabase -Atc $query)
if ($LASTEXITCODE -ne 0 -or (Compare-Object $sourceTables $targetTables)) { throw 'Table inventory mismatch.' }
& docker cp "${Container}:$dump" (Join-Path $ArtifactDirectory "$TargetDatabase-schema.dump") | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not retain schema export.' }
@{
    source = $SourceDatabase; target = $TargetDatabase; schema_only = $true
    table_count = $targetTables.Count; tables = $targetTables
    created_utc = [DateTime]::UtcNow.ToString('O')
} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $ArtifactDirectory "$TargetDatabase-proof.json")
Write-Output "Created $TargetDatabase with $($targetTables.Count) tables; no source rows copied."
