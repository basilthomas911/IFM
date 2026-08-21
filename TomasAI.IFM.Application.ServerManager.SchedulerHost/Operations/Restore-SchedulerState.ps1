[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)] [string] $ConnectionString,
    [Parameter(Mandatory)] [string] $BackupFile,
    [string] $ServiceName = 'IFMSchedulerHost',
    [string] $PgRestore = 'pg_restore.exe'
)

$ErrorActionPreference = 'Stop'
$backup = [IO.Path]::GetFullPath($BackupFile)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw "Backup file not found: $backup" }
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') { throw "Stop '$ServiceName' before restoring scheduler state." }
if ($PSCmdlet.ShouldProcess($ConnectionString, "restore scheduler database from '$backup'")) {
    & $PgRestore --dbname=$ConnectionString --clean --if-exists --exit-on-error $backup
    if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed. Keep the service stopped and follow the recovery runbook.' }
    Write-Output 'Restore completed. Run acceptance checks before starting the service.'
}
