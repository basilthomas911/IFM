[CmdletBinding()]
param([string]$BackupRoot = 'E:\IFM\DatabaseBackup')
$ErrorActionPreference='Stop'
$path=Join-Path $BackupRoot 'cutover-controller/session.json'
if (-not (Test-Path -LiteralPath $path)) { return }
$receipt=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
$process=Get-Process -Id $receipt.ProcessId -ErrorAction SilentlyContinue
if ($null -eq $process) { return }
if ($process.Path -ne $receipt.ExecutablePath -or [Math]::Abs((([DateTimeOffset]$process.StartTime.ToUniversalTime())-([DateTimeOffset]$receipt.StartedAtUtc)).TotalSeconds) -gt 1) { throw 'Cutover worker ownership changed; refusing to stop another process.' }
# Refuse normal stop while database replacement requires controller reconciliation.
$journal=Join-Path $BackupRoot 'cutover-controller/journal'
if (Test-Path -LiteralPath $journal) {
    foreach ($file in Get-ChildItem -LiteralPath $journal -Filter '*.json') {
        $record=Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
        if ($record.Stage -notin 8,10,'Completed','RolledBack') { throw 'A replacement is active or requires reconciliation; leave the cutover worker running.' }
    }
}
Stop-Process -Id $process.Id -Force
$process.WaitForExit()
Write-Output 'Development cutover worker stopped.'

