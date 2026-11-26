[CmdletBinding()]
param([string]$Job = (Join-Path $PSScriptRoot 'nov30-dec1.capture-job.json'), [switch]$Execute,
    [string]$OutputDirectory = 'E:\IFM\SessionQualification\nov30-dec1\witness')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tool = Join-Path $PSScriptRoot 'session_tool.py'
$preflight = "$OutputDirectory.preflight.json"
& python $tool preflight-job --job $Job --output $preflight
if ($LASTEXITCODE -ne 0) { throw "Prepared capture is blocked; no process/configuration changed. Read $preflight" }
if (-not $Execute) { Write-Output 'Preflight passed. No capture witness started; use -Execute only after boundary review.'; return }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new witness directory; existing evidence is never overwritten.' }
# The application source archive must already be enabled by a separately reviewed overlay/startup.
# This starts only a bounded read-only witness, never the development profile or a broker order.
# Windows Start-Process joins ArgumentList; explicitly quote each argument so paths with spaces stay one argument.
function ConvertTo-ProcessArgument([string]$Value) {
    if ($Value.Contains('"')) { throw 'A capture job path cannot contain a double quote.' }
    return '"' + $Value.TrimEnd([char]'\') + '"'
}
$arguments = @($tool,'watch-job','--job',$Job,'--output',$OutputDirectory) | ForEach-Object { ConvertTo-ProcessArgument $_ }
$process = Start-Process -FilePath (Get-Command python).Source -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Read-only witness PID $($process.Id) started. Source archival ownership is not session/financial qualification."
