[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^ifm-database-backup-journal-development-pitr-[0-9]{8}T[0-9]{6}Z$')]
    [string] $TargetTableName,

    [string] $SourceTableName = 'ifm-database-backup-journal-development',
    [string] $Region = 'ca-central-1',
    [string] $AccountId = '107651266250',
    [switch] $Execute
)

$ErrorActionPreference = 'Stop'
if ($SourceTableName -ne 'ifm-database-backup-journal-development' -or
    $Region -ne 'ca-central-1' -or $AccountId -ne '107651266250') {
    throw 'Journal PITR qualification is restricted to the approved Development table/account/Region.'
}

$awsExecutable = (Get-Command aws.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($awsExecutable)) {
    $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Amazon\AWSCLIV2\aws.exe'
    if (Test-Path -LiteralPath $candidate) { $awsExecutable = $candidate }
}
if ([string]::IsNullOrWhiteSpace($awsExecutable)) { throw 'AWS CLI v2 was not found.' }

function Invoke-AwsJson {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $json = & $awsExecutable @Arguments --region $Region --no-cli-pager --output json
    if ($LASTEXITCODE -ne 0) { throw "AWS CLI failed: aws $($Arguments -join ' ')" }
    if ([string]::IsNullOrWhiteSpace($json)) { return $null }
    return $json | ConvertFrom-Json
}

$source = Invoke-AwsJson @('dynamodb', 'describe-table', '--table-name', $SourceTableName)
$sourcePitr = Invoke-AwsJson @('dynamodb', 'describe-continuous-backups', '--table-name', $SourceTableName)
if ($sourcePitr.ContinuousBackupsDescription.PointInTimeRecoveryDescription.PointInTimeRecoveryStatus -ne 'ENABLED') {
    throw 'The source journal table does not have point-in-time recovery enabled.'
}

$plan = [pscustomobject]@{
    Action = 'RestoreJournalToNewTable'
    SourceTable = $SourceTableName
    TargetTable = $TargetTableName
    Region = $Region
    SourceStatus = $source.Table.TableStatus
    SourceLatestRestorableUtc = $sourcePitr.ContinuousBackupsDescription.PointInTimeRecoveryDescription.LatestRestorableDateTime
    TargetWillBeRetained = $true
    ExecuteRequested = [bool]$Execute
}
if (-not $Execute) { return $plan }
if (-not $PSCmdlet.ShouldProcess($TargetTableName, "restore latest PITR point from $SourceTableName and retain it")) {
    return $plan
}

Invoke-AwsJson @(
    'dynamodb', 'restore-table-to-point-in-time',
    '--source-table-name', $SourceTableName,
    '--target-table-name', $TargetTableName,
    '--use-latest-restorable-time'
) | Out-Null

& $awsExecutable dynamodb wait table-exists --table-name $TargetTableName --region $Region --no-cli-pager
if ($LASTEXITCODE -ne 0) { throw "Timed out waiting for restored table '$TargetTableName'." }

$target = Invoke-AwsJson @('dynamodb', 'describe-table', '--table-name', $TargetTableName)
$targetArn = [string]$target.Table.TableArn
Invoke-AwsJson @(
    'dynamodb', 'tag-resource', '--resource-arn', $targetArn,
    '--tags', 'Key=Application,Value=IFM', 'Key=Component,Value=DatabaseBackup',
    'Key=Environment,Value=development', 'Key=Qualification,Value=Gate5-PITR'
) | Out-Null
Invoke-AwsJson @(
    'dynamodb', 'update-continuous-backups', '--table-name', $TargetTableName,
    '--point-in-time-recovery-specification', 'PointInTimeRecoveryEnabled=true'
) | Out-Null

$target = Invoke-AwsJson @('dynamodb', 'describe-table', '--table-name', $TargetTableName)
$targetPitr = Invoke-AwsJson @('dynamodb', 'describe-continuous-backups', '--table-name', $TargetTableName)
$targetTags = Invoke-AwsJson @('dynamodb', 'list-tags-of-resource', '--resource-arn', $targetArn)
$sourceKeys = @($source.Table.KeySchema | ForEach-Object { "$($_.AttributeName):$($_.KeyType)" } | Sort-Object)
$targetKeys = @($target.Table.KeySchema | ForEach-Object { "$($_.AttributeName):$($_.KeyType)" } | Sort-Object)
$targetIndexes = @($target.Table.GlobalSecondaryIndexes | ForEach-Object { $_.IndexName })

if ((Compare-Object $sourceKeys $targetKeys).Count -ne 0) { throw 'Restored journal key schema differs from source.' }
if ($targetIndexes -notcontains 'WorkQueueIndex') { throw 'Restored journal is missing WorkQueueIndex.' }
if ($target.Table.TableStatus -ne 'ACTIVE') { throw 'Restored journal is not ACTIVE.' }
if ($targetPitr.ContinuousBackupsDescription.PointInTimeRecoveryDescription.PointInTimeRecoveryStatus -ne 'ENABLED') {
    throw 'Restored journal PITR was not enabled.'
}

[pscustomobject]@{
    Result = 'Passed'
    SourceTable = $SourceTableName
    TargetTable = $TargetTableName
    TargetArn = $targetArn
    TableStatus = $target.Table.TableStatus
    PointInTimeRecoveryStatus = $targetPitr.ContinuousBackupsDescription.PointInTimeRecoveryDescription.PointInTimeRecoveryStatus
    GlobalSecondaryIndexes = $targetIndexes
    Tags = $targetTags.Tags
    TargetRetainedForEvidence = $true
    AlarmReattachmentRequiredThroughReviewedInfrastructureChange = $true
}
