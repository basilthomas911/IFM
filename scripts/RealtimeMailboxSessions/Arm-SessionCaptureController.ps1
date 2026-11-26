[CmdletBinding()]
param([Parameter(Mandatory)][string]$Config,
    [string]$SecretFile='D:/IFM/SessionQualification/nov30-dec1/control/capture-token.dpapi',
    [switch]$Register)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$absolute=[IO.Path]::GetFullPath($Config)
$value=Get-Content -LiteralPath $absolute -Raw|ConvertFrom-Json
if(-not(Test-Path -LiteralPath $SecretFile -PathType Leaf)){throw 'Existing CurrentUser DPAPI token is required; arming never silently replaces a runtime token.'}
& python (Join-Path $PSScriptRoot 'capture_controller.py') validate --config $absolute
if($LASTEXITCODE -ne 0){throw 'Operational configuration is not complete; no task registered.'}
& (Join-Path $PSScriptRoot 'Run-SessionCaptureController.ps1') -Config $absolute -SecretFile $SecretFile -ValidateNow
$document=[System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($absolute))
try{$armText=$document.RootElement.GetProperty('armUtc').GetString()}finally{$document.Dispose()}
# ConvertFrom-Json can materialize UTC strings as DateTime; implicit string conversion loses the offset.
$at=[DateTimeOffset]::Parse($armText,[Globalization.CultureInfo]::InvariantCulture)
if($at -le [DateTimeOffset]::UtcNow -or $at -gt [DateTimeOffset]::UtcNow.AddDays(8)){throw 'Arming must use an explicit bounded future start.'}
$name='IFM-OriginalSessions-'+$value.taskId
if($name -notmatch '^IFM-OriginalSessions-[a-f0-9]{12}$'){throw 'Task identity must be a bounded unique 12-hex id.'}
$wrapper=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Run-SessionCaptureController.ps1'))
foreach($path in @($absolute,$wrapper,$SecretFile)){if($path.Contains('"')){throw 'A scheduled argument cannot contain quotes.'}}
$args="-NoProfile -NonInteractive -File `"$wrapper`" -Config `"$absolute`" -SecretFile `"$SecretFile`""
$action=New-ScheduledTaskAction -Execute (Get-Command pwsh).Source -Argument $args
$trigger=New-ScheduledTaskTrigger -Once -At $at.LocalDateTime
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$principal=New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Days 8) -MultipleInstances IgnoreNew -StartWhenAvailable -WakeToRun -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$task=New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings
if($Register){
    if(Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue){throw 'Existing scheduled evidence job will not be overwritten.'}
    Register-ScheduledTask -TaskName $name -InputObject $task|Out-Null
    $actual=Get-ScheduledTask -TaskName $name
    if($actual.Actions.Execute -ne $action.Execute -or $actual.Actions.Arguments -ne $args -or -not $actual.Settings.WakeToRun){throw 'Scheduled action/settings readback differs.'}
    if(@($actual.Triggers).Count -ne 1 -or [DateTimeOffset]::Parse($actual.Triggers[0].StartBoundary,[Globalization.CultureInfo]::InvariantCulture).ToUniversalTime() -ne $at.ToUniversalTime()){
        throw 'Scheduled UTC trigger readback differs; capture must not be treated as armed.'
    }
}
$receipt=@{version=1;state=$(if($Register){'ArmedInteractiveLogonRequired'}else{'ValidatedNotArmed'});taskName=$name;startUtc=$at.ToUniversalTime().ToString('o');configSha256=(Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash;protectedSecretPath=[IO.Path]::GetFullPath($SecretFile);captureStarted=$false;qualified=$false}
$receipt|ConvertTo-Json -Depth 5|Set-Content -LiteralPath ($absolute+'.arm.json') -Encoding utf8
Write-Output $receipt.state
