[CmdletBinding()]
param([string]$BackupRoot = 'E:\IFM\DatabaseBackup', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $repository '.artifacts/database-recovery/windows-cutover-worker'
$state = Join-Path $BackupRoot 'cutover-controller'
[IO.Directory]::CreateDirectory($state) | Out-Null
if (-not $NoBuild) {
    dotnet publish (Join-Path $repository 'TomasAI.IFM.Api.DatabaseBackup.Host/TomasAI.IFM.Api.DatabaseBackup.Host.csproj') -o $output --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Cutover worker publish failed.' }
}
$exe = Join-Path $output 'TomasAI.IFM.Api.DatabaseBackup.Host.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Cutover worker executable is missing.' }
$existing = Get-Process -Name TomasAI.IFM.Api.DatabaseBackup.Host -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($existing) { Write-Output 'Development cutover worker is already running.'; return }
$native = docker inspect ifm-database-backup-development-database-backup-host-1 | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Native AWS backup host must be running first.' }
# Copy public AWS configuration only; never copy native database or AWS credentials.
foreach ($entry in $native.Config.Env) {
    $pair = $entry.Split('=',2)
    if ($pair[0].StartsWith('DatabaseBackup__Sources__AwsCloud__')) {
        [Environment]::SetEnvironmentVariable($pair[0],$pair[1],'Process')
    }
    if ($pair[0] -eq 'DatabaseBackup__Host__HostId') {
        [Environment]::SetEnvironmentVariable($pair[0],$pair[1],'Process')
    }
}
$settings = @{
    DOTNET_ENVIRONMENT='Development'; ASPNETCORE_URLS='http://127.0.0.1:8089'
    DatabaseBackup__Host__CutoverWorkerOnly='true'
    DatabaseBackup__EnvironmentId='development'
    DatabaseBackup__Sources__LocalWorkstation__Enabled='false'
    DatabaseBackup__Sources__LocalWorkstation__DryRun='true'
    DatabaseBackup__Sources__AwsCloud__AutomaticRetentionEnabled='false'
    DatabaseBackup__Sources__AwsCloud__WalSpoolPath=(Join-Path $state 'wal-spool')
    DatabaseBackup__Journal__Path=(Join-Path $state 'execution-journal.db')
    DatabaseBackup__OutputRoot=(Join-Path $repository '.artifacts/database-backup/output')
    DatabaseBackup__DevelopmentReplacement__Enabled='true'
    DatabaseBackup__DevelopmentReplacement__Environment='Development'
    DatabaseBackup__DevelopmentReplacement__RepositoryRoot=$repository
    DatabaseBackup__DevelopmentReplacement__PersistentRoot=$state
    DatabaseBackup__DevelopmentReplacement__PostgreSqlRestoreRoot=(Join-Path $BackupRoot 'restore/postgresql')
    DatabaseBackup__DevelopmentReplacement__ScyllaRestoreRoot=(Join-Path $BackupRoot 'restore/scylla')
    Nats__JetStreamEventListener__Url='nats://localhost:4222'
    Nats__JetStreamEventListener__DurableConsumerNamePrefix='ifm-database-cutover-development'
    Nats__JetStreamProducer__Url='nats://localhost:4222'
    AWS_PROFILE='development-cutover'; AWS_REGION='ca-central-1'
}
$credentialScript = Join-Path $PSScriptRoot 'Read-AwsDevelopmentBackupSession.ps1'
$configPath = Join-Path $state 'aws-config'
$credentialCommand = 'powershell.exe -NoProfile -File "' + $credentialScript + '"'
[IO.File]::WriteAllText($configPath, "[profile development-cutover]`ncredential_process = $credentialCommand`nregion = ca-central-1`n", (New-Object Text.UTF8Encoding($false)))
$settings.AWS_CONFIG_FILE=$configPath
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,[string]$settings[$key],'Process') }
# Force expiration-aware credential_process rather than inherited temporary environment keys.
foreach ($key in 'AWS_ACCESS_KEY_ID','AWS_SECRET_ACCESS_KEY','AWS_SESSION_TOKEN') { [Environment]::SetEnvironmentVariable($key,$null,'Process') }
$process = Start-Process -FilePath $exe -WorkingDirectory $output -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $state 'stdout.log') -RedirectStandardError (Join-Path $state 'stderr.log')
$receipt = @{ ProcessId=$process.Id; StartedAtUtc=$process.StartTime.ToUniversalTime().ToString('o'); ExecutablePath=$exe; HealthUrl='http://127.0.0.1:8089/health/ready' }
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $state 'session.json')
$receipt | ConvertTo-Json
