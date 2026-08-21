[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ConnectionString,
    [Parameter(Mandatory)] [string] $DestinationDirectory,
    [string] $PgDump = 'pg_dump.exe'
)

$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$backup = Join-Path $destination "ifm_scheduler_$stamp.dump"
& $PgDump --dbname=$ConnectionString --format=custom --compress=9 --file=$backup
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; no valid scheduler backup was produced.' }
$hash = Get-FileHash -LiteralPath $backup -Algorithm SHA256
$hash | ConvertTo-Json | Set-Content -LiteralPath "$backup.sha256.json" -Encoding utf8
Write-Output $backup
