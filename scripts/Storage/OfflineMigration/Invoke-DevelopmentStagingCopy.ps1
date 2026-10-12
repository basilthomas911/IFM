[CmdletBinding()]
param(
    [string]$Database = 'ifm_storage_migration_staging_20261012',
    [string]$PostgresContainer = 'ifm_db',
    [string]$ArtifactDirectory = '.artifacts/storage-cutover/staging-copy-20261012'
)
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^ifm_storage_migration_staging_[a-z0-9_]+$') { throw 'Only isolated migration staging databases are accepted.' }
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$artifactPath = [IO.Path]::GetFullPath((Join-Path $workspace $ArtifactDirectory))
if (-not $artifactPath.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifacts must remain inside the workspace.' }
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$dll = Join-Path $PSScriptRoot 'bin/Debug/net10.0/OfflineMigration.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Build OfflineMigration before running this offline driver.' }
$groups = [ordered]@{
    trade='trade_test_db'; trade_plan='trade_plan_test_db'; reference='reference_test_db'
    securities='securities_test_db'; option_pricer='option_pricer_test_db'; market_data='market_data_test_db'
}
$priorTarget = $env:IFM_OFFLINE_MIGRATION_TARGET
$results = [Collections.Generic.List[object]]::new()
try {
    $env:IFM_OFFLINE_MIGRATION_TARGET = 'Host=localhost;Database=' + $Database
    foreach ($group in $groups.GetEnumerator()) {
        $query = "SELECT table_name FROM information_schema.tables WHERE table_schema='$($group.Key)' AND table_type='BASE TABLE' ORDER BY table_name;"
        $tables = & docker exec $PostgresContainer psql -U postgres -d $Database -Atc $query
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory staging tables.' }
        foreach ($table in $tables) {
            if ($table -notmatch '^[a-z][a-z0-9_]*$') { throw 'Unexpected table identifier in staging metadata.' }
            $label = $group.Key + '.' + $table
            $timer = [Diagnostics.Stopwatch]::StartNew()
            $proofPath = Join-Path $artifactPath ($label + '.json')
            $errorPath = Join-Path $artifactPath ($label + '.errors.log')
            # Native stderr is retained without stopping the independent table inventory.
            $previousErrorPreference = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            & dotnet $dll --copy-table-writers-stopped $group.Value $group.Key $table > $proofPath 2> $errorPath
            $exitCode = $LASTEXITCODE
            $ErrorActionPreference = $previousErrorPreference
            $timer.Stop()
            $results.Add([pscustomobject]@{source=$group.Value+'.'+$table;target=$label;exitCode=$exitCode;seconds=$timer.Elapsed.TotalSeconds;proof=$proofPath;errors=$errorPath})
            $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactPath 'manifest.json') -Encoding UTF8
            Write-Output ($label + ' exit=' + $exitCode + ' elapsed=' + $timer.Elapsed.ToString())
        }
    }
} finally { $env:IFM_OFFLINE_MIGRATION_TARGET = $priorTarget }
if ($results.Where({$_.exitCode -ne 0}).Count -gt 0) { throw 'Some staged tables require explicit conversion; review the manifest and fix before cutover.' }
