[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [ValidateSet('Development', 'Staging', 'Production')] [string] $Environment,
    [Parameter(Mandatory)] [string] $StackName,
    [Parameter(Mandatory)] [string] $TemplateFile,
    [Parameter(Mandatory)] [string] $Region,
    [Parameter(Mandatory)] [string] $ApprovalReference,
    [Parameter(Mandatory)] [ValidateSet('CREATE', 'UPDATE')] [string] $ChangeSetType,
    [Parameter(Mandatory)] [ValidatePattern('^arn:aws:iam::[0-9]{12}:role/[A-Za-z0-9+=,.@_/-]+$')] [string] $CloudFormationExecutionRoleArn,
    [Parameter(Mandatory)] [string[]] $Parameters,
    [string] $AllowlistPath = (Join-Path $PSScriptRoot 'gate0-identity-allowlist.json')
)

$ErrorActionPreference = 'Stop'
if ($ApprovalReference.Length -lt 8 -or ($ApprovalReference.ToCharArray() | Where-Object { [char]::IsControl($_) })) {
    throw 'A bounded reviewed implementation-plan approval reference is required.'
}
$expectedStackPrefix = "ifm-database-backup-$($Environment.ToLowerInvariant())-"
if (-not $StackName.StartsWith($expectedStackPrefix, [StringComparison]::Ordinal)) {
    throw "Stack name must start with '$expectedStackPrefix'."
}
$identity = & (Join-Path $PSScriptRoot 'Invoke-AwsBackupIdentityPreflight.ps1') -Environment $Environment -Region $Region -PolicyPath $AllowlistPath
$mutationAuthorization = $identity.PSObject.Properties['AwsMutationAuthorized']
if ($null -eq $mutationAuthorization) { throw 'AWS identity preflight returned an invalid authorization contract.' }
if (-not [bool]$mutationAuthorization.Value) { throw 'AWS mutation is not authorized by the selected environment policy.' }
if ($CloudFormationExecutionRoleArn -notmatch "^arn:aws:iam::$($identity.AccountId):role/") {
    throw 'The CloudFormation execution role must belong to the preflight-approved account.'
}
& (Join-Path $PSScriptRoot 'Test-AwsBackupInfrastructure.ps1') | Out-Null

$changeSetName = "ifm-backup-$($Environment.ToLowerInvariant())-$(Get-Date -Format 'yyyyMMddHHmmss')"
if ($PSCmdlet.ShouldProcess("$StackName in $Region", "Create reviewed CloudFormation change set $changeSetName")) {
    $awsExecutable = (Get-Command aws.exe -ErrorAction SilentlyContinue).Source
    if ([string]::IsNullOrWhiteSpace($awsExecutable)) {
        $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Amazon\AWSCLIV2\aws.exe'
        if (Test-Path -LiteralPath $candidate) { $awsExecutable = $candidate }
    }
    if ([string]::IsNullOrWhiteSpace($awsExecutable)) { throw 'AWS CLI v2 was not found.' }

    $arguments = @(
        'cloudformation', 'create-change-set',
        '--stack-name', $StackName,
        '--change-set-name', $changeSetName,
        '--change-set-type', $ChangeSetType,
        '--template-body', "file://$((Resolve-Path -LiteralPath $TemplateFile).Path)",
        '--role-arn', $CloudFormationExecutionRoleArn,
        '--capabilities', 'CAPABILITY_NAMED_IAM',
        '--region', $Region,
        '--description', "IFM Gate 4 approval $ApprovalReference",
        '--tags', 'Key=Application,Value=IFM', 'Key=Component,Value=DatabaseBackup', "Key=Environment,Value=$($Environment.ToLowerInvariant())", "Key=ApprovalReference,Value=$ApprovalReference",
        '--parameters'
    )
    $arguments += $Parameters
    if ($ChangeSetType -eq 'CREATE') { $arguments += @('--on-stack-failure', 'DO_NOTHING') }
    $arguments += @('--no-cli-pager', '--output', 'json')

    & $awsExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw 'CloudFormation change-set creation failed.' }
}

# Deliberately no execute-change-set call. Independent review and execution are separate actions.
