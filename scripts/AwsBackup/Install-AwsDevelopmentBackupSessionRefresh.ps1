[CmdletBinding()]
param([switch]$StartNow)
$ErrorActionPreference='Stop'
$taskName='IFM-Development-AwsBackupSessionRefresh'
$refresh=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Refresh-AwsDevelopmentBackupSession.ps1'))
if(-not (Test-Path -LiteralPath $refresh)){throw 'Credential refresh script missing.'}
$user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$action=New-ScheduledTaskAction -Execute "$PSHOME\powershell.exe" -Argument ('-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $refresh + '"')
$logon=New-ScheduledTaskTrigger -AtLogOn -User $user
$periodic=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 20)
$principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 5) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$task=New-ScheduledTask -Action $action -Trigger @($logon,$periodic) -Principal $principal -Settings $settings -Description 'Refresh the temporary IFM Development AWS backup upload-role session after sign-in and every 20 minutes. Stores no AWS credentials in the task.'
Register-ScheduledTask -TaskName $taskName -InputObject $task -Force|Out-Null
if($StartNow){Start-ScheduledTask -TaskName $taskName}
Get-ScheduledTask -TaskName $taskName|Select-Object TaskName,State
