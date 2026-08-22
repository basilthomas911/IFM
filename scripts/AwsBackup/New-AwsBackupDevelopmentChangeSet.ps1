[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('RecoveryVault', 'PrimaryVault', 'Workload', 'Audit')]
    [string] $Stack,

    [Parameter(Mandatory)]
    [ValidateSet('CREATE', 'UPDATE')]
    [string] $ChangeSetType,

    [ValidatePattern('^arn:aws:kms:ca-west-1:[0-9]{12}:key/.+$')]
    [string] $RecoveryEncryptionKeyArn,

    [ValidatePattern('^arn:aws:kms:ca-central-1:[0-9]{12}:key/.+$')]
    [string] $PrimaryEncryptionKeyArn,

    [string] $InputsPath = (Join-Path $PSScriptRoot '..\..\deploy\aws\database-backup\environments\development\deployment-inputs.json')
)

$ErrorActionPreference = 'Stop'
$inputs = Get-Content -LiteralPath $InputsPath -Raw | ConvertFrom-Json
if ($inputs.environment -ne 'Development' -or $inputs.accountId -ne '107651266250' -or
    $inputs.approvalReference -ne 'IFM-GATE4-20260822') {
    throw 'Development deployment inputs do not match the approved Gate 4 scope.'
}

$root = Join-Path $PSScriptRoot '..\..\deploy\aws\database-backup'
$accountId = [string]$inputs.accountId
$environmentName = $inputs.environment.ToLowerInvariant()
$common = @(
    "ParameterKey=EnvironmentName,ParameterValue=$environmentName"
)

switch ($Stack) {
    'RecoveryVault' {
        $stackName = [string]$inputs.stackNames.recoveryVault
        $template = Join-Path $root 'recovery-vault\template.yaml'
        $region = [string]$inputs.recoveryRegion
        $parameters = $common + @(
            "ParameterKey=RecoveryBucketName,ParameterValue=$($inputs.resourceNames.recoveryBucket)",
            "ParameterKey=RecoveryAuditBucketName,ParameterValue=$($inputs.resourceNames.recoveryAuditBucket)",
            "ParameterKey=WorkloadAccountId,ParameterValue=$accountId",
            "ParameterKey=ReplicationRoleArn,ParameterValue=$($inputs.resourceNames.replicationRoleArn)",
            "ParameterKey=RetentionDays,ParameterValue=$($inputs.retentionDays)",
            "ParameterKey=ObjectLockMode,ParameterValue=$($inputs.objectLockMode)"
        )
    }
    'PrimaryVault' {
        if ([string]::IsNullOrWhiteSpace($RecoveryEncryptionKeyArn)) {
            throw 'PrimaryVault requires the deployed recovery stack RecoveryEncryptionKeyArn output.'
        }
        $stackName = [string]$inputs.stackNames.primaryVault
        $template = Join-Path $root 'primary-vault\template.yaml'
        $region = [string]$inputs.primaryRegion
        $parameters = $common + @(
            "ParameterKey=WorkloadAccountId,ParameterValue=$accountId",
            "ParameterKey=PrimaryBucketName,ParameterValue=$($inputs.resourceNames.primaryBucket)",
            "ParameterKey=PrimaryAuditBucketName,ParameterValue=$($inputs.resourceNames.primaryAuditBucket)",
            "ParameterKey=RecoveryBucketArn,ParameterValue=arn:aws:s3:::$($inputs.resourceNames.recoveryBucket)",
            "ParameterKey=RecoveryAccountId,ParameterValue=$accountId",
            "ParameterKey=RecoveryEncryptionKeyArn,ParameterValue=$RecoveryEncryptionKeyArn",
            "ParameterKey=RetentionDays,ParameterValue=$($inputs.retentionDays)",
            "ParameterKey=ObjectLockMode,ParameterValue=$($inputs.objectLockMode)"
        )
    }
    'Workload' {
        if ([string]::IsNullOrWhiteSpace($PrimaryEncryptionKeyArn)) {
            throw 'Workload requires the deployed primary stack PrimaryEncryptionKeyArn output.'
        }
        $stackName = [string]$inputs.stackNames.workload
        $template = Join-Path $root 'workload\template.yaml'
        $region = [string]$inputs.primaryRegion
        $parameters = $common + @(
            "ParameterKey=PrimaryVaultBucketArn,ParameterValue=arn:aws:s3:::$($inputs.resourceNames.primaryBucket)",
            "ParameterKey=RecoveryVaultBucketArn,ParameterValue=arn:aws:s3:::$($inputs.resourceNames.recoveryBucket)",
            "ParameterKey=PrimaryEncryptionKeyArn,ParameterValue=$PrimaryEncryptionKeyArn",
            "ParameterKey=SecurityAuditPrincipalArn,ParameterValue=$($inputs.securityAuditPrincipalArn)",
            "ParameterKey=MonthlyBudgetUsd,ParameterValue=$($inputs.monthlyBudgetUsd)",
            "ParameterKey=BudgetNotificationEmail,ParameterValue=$($inputs.budgetNotificationEmail)"
        )
    }
    'Audit' {
        $stackName = [string]$inputs.stackNames.audit
        $template = Join-Path $root 'policy\audit.yaml'
        $region = [string]$inputs.primaryRegion
        $parameters = $common + @(
            "ParameterKey=PrimaryBucketName,ParameterValue=$($inputs.resourceNames.primaryBucket)",
            "ParameterKey=RecoveryBucketName,ParameterValue=$($inputs.resourceNames.recoveryBucket)",
            "ParameterKey=AuditBucketName,ParameterValue=$($inputs.resourceNames.auditBucket)",
            "ParameterKey=ConfigBucketName,ParameterValue=$($inputs.resourceNames.configBucket)",
            "ParameterKey=SecurityAuditPrincipalArn,ParameterValue=$($inputs.securityAuditPrincipalArn)",
            "ParameterKey=ObjectLockMode,ParameterValue=$($inputs.objectLockMode)"
        )
    }
}

$arguments = @{
    Environment = 'Development'
    StackName = $stackName
    TemplateFile = $template
    Region = $region
    ApprovalReference = [string]$inputs.approvalReference
    ChangeSetType = $ChangeSetType
    CloudFormationExecutionRoleArn = [string]$inputs.cloudFormationExecutionRoleArn
    Parameters = $parameters
}

if ($PSCmdlet.ShouldProcess("$stackName in $region", "Prepare $ChangeSetType Gate 4 change set")) {
    & (Join-Path $PSScriptRoot 'New-AwsBackupChangeSet.ps1') @arguments
}
