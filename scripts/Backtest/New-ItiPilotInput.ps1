param(
    [string]$Container = 'ifm-scylladb',
    [string]$OutputFile = '.artifacts/iti-pilot.json',
    [ValidateRange(1, 10000)][int]$Limit = 5000
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputFile)
New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($destination)) | Out-Null
if (Test-Path -LiteralPath $destination) { throw 'Choose a new output file; existing replay evidence is retained.' }
$token = [Guid]::NewGuid().ToString('N')
$adapter = "/tmp/ifm-iti-pilot-$token.py"
$snapshot = "/tmp/ifm-iti-pilot-$token.json"
& docker cp "$PSScriptRoot/prepare_iti_pilot.py" "${Container}:$adapter"
if ($LASTEXITCODE -ne 0) { throw 'Could not stage the read-only tick adapter.' }
& docker exec $Container python3 $adapter --output $snapshot --limit $Limit
if ($LASTEXITCODE -ne 0) { throw 'Historical input preparation failed.' }
& docker cp "${Container}:$snapshot" $destination
if ($LASTEXITCODE -ne 0) { throw 'Could not retain the input snapshot.' }
Write-Output "Prepared historical ITI input: $destination"
