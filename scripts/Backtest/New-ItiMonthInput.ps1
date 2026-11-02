param(
    [string]$Container = 'ifm-scylladb',
    [string]$OutputDirectory = '.artifacts/iti-october-2026',
    [string]$EsManifest = '/tmp/ifm-es-backtest-october-2026/import-manifest.json'
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a new output directory; existing replay evidence is retained.' }
New-Item -ItemType Directory -Force $destination | Out-Null
$remote = '/tmp/ifm-iti-month-' + [Guid]::NewGuid().ToString('N')
& docker exec $Container mkdir $remote
if ($LASTEXITCODE -ne 0) { throw 'Could not create the export directory.' }
foreach ($file in @('prepare_iti_pilot.py', 'prepare_iti_month.py')) {
    & docker cp "$PSScriptRoot/$file" "${Container}:$remote/$file"
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage the read-only tick adapter.' }
}
& docker exec $Container python3 "$remote/prepare_iti_month.py" --output "$remote/output" --es-manifest $EsManifest
if ($LASTEXITCODE -ne 0) { throw 'Historical month export failed; partial evidence retained in the container.' }
& docker cp "${Container}:$remote/output/." $destination
if ($LASTEXITCODE -ne 0) { throw 'Could not retain the input snapshot.' }
Write-Output "Prepared monthly ITI input: $destination/input.json"
