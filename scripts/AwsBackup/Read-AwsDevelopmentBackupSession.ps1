[CmdletBinding()]
param([string]$SessionFile)
if ([string]::IsNullOrWhiteSpace($SessionFile)) { $SessionFile = Join-Path $PSScriptRoot '../../.artifacts/aws-backup-session/session.json' }
$ErrorActionPreference = 'Stop'
# AWS SDK credential_process output is consumed only by its child process.
$document = $null
for ($attempt=0; $attempt -lt 10; $attempt++) {
    try { $document = [IO.File]::ReadAllText([IO.Path]::GetFullPath($SessionFile)); break }
    catch [IO.IOException] { if ($attempt -eq 9) { throw }; Start-Sleep -Milliseconds 100 }
}
$session = $document | ConvertFrom-Json
if ([DateTimeOffset]$session.Expiration -le [DateTimeOffset]::UtcNow.AddMinutes(1)) { throw 'Development backup session expired; run the authorized refresher.' }
[Console]::Out.Write($document)
