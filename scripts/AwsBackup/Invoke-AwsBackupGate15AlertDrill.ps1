[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet(
        'ifm-database-backup-kms-denial-development',
        'ifm-database-backup-runtime-replication-failure-development',
        'ifm-database-backup-retention-drift-development')]
    [string] $AlarmName = 'ifm-database-backup-kms-denial-development',
    [switch] $Execute
)

$ErrorActionPreference = 'Stop'
$accountId = '107651266250'
$region = 'ca-central-1'
$identity = aws sts get-caller-identity --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $identity.Account -ne $accountId) {
    throw 'The Gate 15 alert drill requires the allowlisted Development account.'
}

if (-not $Execute) {
    [pscustomobject]@{
        Result = 'Preview'
        AccountId = $identity.Account
        Region = $region
        AlarmName = $AlarmName
        MutationPerformed = $false
        NextCommand = ".\scripts\AwsBackup\Invoke-AwsBackupGate15AlertDrill.ps1 -AlarmName '$AlarmName' -Execute -Confirm"
    }
    return
}

if (-not $PSCmdlet.ShouldProcess($AlarmName, 'Trigger and reset the Development-only CloudWatch alarm drill')) {
    return
}

$utcTimestamp = [DateTime]::UtcNow.ToString(
    'yyyy-MM-ddTHH:mm:ssZ',
    [Globalization.CultureInfo]::InvariantCulture)
$reason = "IFM Gate 15 Development drill $utcTimestamp; follow the alarm runbook URL and inspect only safe operation identifiers."
aws cloudwatch set-alarm-state --region $region --alarm-name $AlarmName `
    --state-value ALARM --state-reason $reason --no-cli-pager
if ($LASTEXITCODE -ne 0) { throw 'Unable to trigger the Gate 15 Development alarm.' }

$alarm = aws cloudwatch describe-alarms --region $region --alarm-names $AlarmName `
    --query 'MetricAlarms[0].{Name:AlarmName,State:StateValue,Description:AlarmDescription,Actions:AlarmActions}' `
    --output json --no-cli-pager | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $alarm.State -ne 'ALARM' -or @($alarm.Actions).Count -ne 1 -or
    $alarm.Description -notmatch '^https://github\.com/basilthomas911/IFM/') {
    throw 'The Gate 15 alarm did not enter ALARM with one route and the approved runbook link.'
}

aws cloudwatch set-alarm-state --region $region --alarm-name $AlarmName `
    --state-value OK --state-reason 'IFM Gate 15 Development drill completed and reset.' --no-cli-pager
if ($LASTEXITCODE -ne 0) { throw 'The Gate 15 alarm drill passed but its state could not be reset.' }

[pscustomobject]@{
    Result = 'Passed'
    AccountId = $identity.Account
    Region = $region
    AlarmName = $alarm.Name
    Runbook = $alarm.Description
    AlarmAction = @($alarm.Actions)[0]
    ResetState = 'OK'
    MutationPerformed = $true
}
