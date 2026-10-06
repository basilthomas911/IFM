[CmdletBinding()]
param([string]$SessionDirectory, [switch]$Continuous)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SessionDirectory)) { $SessionDirectory = Join-Path $PSScriptRoot '../../.artifacts/aws-backup-session' }
$SessionDirectory = [IO.Path]::GetFullPath($SessionDirectory)
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $SessionDirectory.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Session directory must remain within the repository.' }
[IO.Directory]::CreateDirectory($SessionDirectory) | Out-Null
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$access = New-Object Security.AccessControl.DirectorySecurity
$access.SetAccessRuleProtection($true, $false)
$access.SetOwner($identity)
$rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$access.AddAccessRule($rule)
$systemRule = New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier('S-1-5-18')), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$access.AddAccessRule($systemRule)
(New-Object IO.DirectoryInfo($SessionDirectory)).SetAccessControl($access)
$encoding = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $SessionDirectory 'config'), "[profile development-backup]`ncredential_process = /bin/cat /run/aws/session.json`nregion = ca-central-1`n", $encoding)
do {
    try {
        $caller = aws sts get-caller-identity --region ca-central-1 --output json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or $caller.Arn -ne 'arn:aws:iam::107651266250:user/basil.thomas@live.ca') { throw 'Expected Development workstation identity unavailable.' }
        $session = aws sts assume-role --role-arn arn:aws:iam::107651266250:role/ifm-database-backup-upload-development --role-session-name ifm-development-backup-host --duration-seconds 3600 --region ca-central-1 --output json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0) { throw 'Upload role refresh failed.' }
        $document = @{ Version=1; AccessKeyId=$session.Credentials.AccessKeyId; SecretAccessKey=$session.Credentials.SecretAccessKey; SessionToken=$session.Credentials.SessionToken; Expiration=([DateTimeOffset]$session.Credentials.Expiration).ToUniversalTime().ToString('o') }
        $temporary = Join-Path $SessionDirectory 'session.next.json'
        $destination = Join-Path $SessionDirectory 'session.json'
        [IO.File]::WriteAllText($temporary, ($document | ConvertTo-Json -Compress), $encoding)
        if ([IO.File]::Exists($destination)) { [IO.File]::Replace($temporary, $destination, [System.Management.Automation.Language.NullString]::Value) } else { [IO.File]::Move($temporary, $destination) }
        $status = @{ roleArn='arn:aws:iam::107651266250:role/ifm-database-backup-upload-development'; refreshedUtc=[DateTimeOffset]::UtcNow.ToString('o'); expiresUtc=$document.Expiration; result='Ready' }
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'status.json'), ($status | ConvertTo-Json), $encoding)
        if (-not $Continuous) { Write-Output ('Temporary upload-role session ready; expires ' + $document.Expiration) }
    } catch {
        [IO.File]::WriteAllText((Join-Path $SessionDirectory 'status.json'), (@{ result='RefreshFailed'; observedUtc=[DateTimeOffset]::UtcNow.ToString('o') } | ConvertTo-Json), $encoding)
        if (-not $Continuous) { throw }
        Start-Sleep -Seconds 60
        continue
    }
    if ($Continuous) { Start-Sleep -Seconds 1200 }
} while ($Continuous)
