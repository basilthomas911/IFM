param(
    [Parameter(Mandatory=$true)][ValidateSet('ES','VX')][string]$Symbol,
    [Parameter(Mandatory=$true)][string]$Root,
    [ValidateRange(1,4)][int]$LoadWorkers = 1,
    [switch]$SkipStaging
)
$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$container = 'ifm-scylladb'
$remote = '/tmp/ifm-backtest-nov2025-sep2026/' + $Symbol
if (-not (Test-Path -LiteralPath (Join-Path $rootPath 'import-manifest.json'))) {
    throw 'Preparation must complete before loading.'
}
& docker exec $container mkdir -p $remote
if ($LASTEXITCODE) { throw 'Cannot create staging directory.' }
if (-not $SkipStaging) {
    foreach ($item in @('csv','import-manifest.json')) {
        & docker cp (Join-Path $rootPath $item) ($container + ':' + $remote + '/')
        if ($LASTEXITCODE) { throw "Cannot stage $item." }
    }
}
foreach ($phase in @('load','verify')) {
    Write-Output "$(Get-Date -Format o) $Symbol $phase started"
    $ErrorActionPreference = 'Continue'
    & docker exec $container /opt/scylladb/python3/bin/python3 /tmp/ifm-backtest-nov2025-sep2026/import_ticks.py $phase --root $remote --symbol $Symbol --load-workers $LoadWorkers 2>&1 | ForEach-Object { "$_" }
    $phaseExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($phaseExitCode) { throw "$Symbol $phase failed; inspect logs and retained staging files." }
    & docker cp ($container + ':' + $remote + '/load-checkpoint.json') (Join-Path $rootPath 'load-checkpoint.json')
    if ($LASTEXITCODE) { throw 'Cannot retain load checkpoint.' }
}
& docker cp ($container + ':' + $remote + '/verification.json') (Join-Path $rootPath 'verification.json')
if ($LASTEXITCODE) { throw 'Cannot retain verification proof.' }
$proof = Get-Content -LiteralPath (Join-Path $rootPath 'verification.json') -Raw | ConvertFrom-Json
if (-not $proof.verified) { throw 'Verification did not complete.' }
Write-Output "$(Get-Date -Format o) $Symbol complete: $($proof.new_rows_per_table) new rows per table; $($proof.expected_rows_per_table) including preserved October."
