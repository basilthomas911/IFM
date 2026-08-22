[CmdletBinding()]
param(
    [string] $PolicyRoot = (Join-Path $PSScriptRoot '..\..\deploy\aws\database-backup\environments\development')
)

$ErrorActionPreference = 'Stop'
$accountId = '107651266250'
$executionRoleArn = "arn:aws:iam::$accountId`:role/IFM-Gate4-CloudFormationExecutionRole"
$paths = [ordered]@{
    Trust = Join-Path $PolicyRoot 'cloudformation-execution-role-trust-policy.json'
    Execution = Join-Path $PolicyRoot 'cloudformation-execution-policy.json'
    Deployer = Join-Path $PolicyRoot 'gate4-deployer-policy.json'
    Inputs = Join-Path $PolicyRoot 'deployment-inputs.json'
    Outputs = Join-Path $PolicyRoot 'deployed-stack-outputs.json'
    Qualification = Join-Path $PolicyRoot 'gate4-live-qualification.json'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required Gate 4 IAM artifact is missing: $path" }
}

$trust = Get-Content -LiteralPath $paths.Trust -Raw | ConvertFrom-Json
$execution = Get-Content -LiteralPath $paths.Execution -Raw | ConvertFrom-Json
$deployer = Get-Content -LiteralPath $paths.Deployer -Raw | ConvertFrom-Json
$inputs = Get-Content -LiteralPath $paths.Inputs -Raw | ConvertFrom-Json
$outputs = Get-Content -LiteralPath $paths.Outputs -Raw | ConvertFrom-Json
$qualification = Get-Content -LiteralPath $paths.Qualification -Raw | ConvertFrom-Json

if (@($trust.Statement).Count -ne 1 -or $trust.Statement[0].Effect -ne 'Allow' -or
    $trust.Statement[0].Principal.Service -ne 'cloudformation.amazonaws.com' -or
    $trust.Statement[0].Action -ne 'sts:AssumeRole') {
    throw 'The execution-role trust policy must allow only CloudFormation to assume the role.'
}

$executionActions = @($execution.Statement | ForEach-Object { @($_.Action) })
$deployerActions = @($deployer.Statement | ForEach-Object { @($_.Action) })
$allActions = @($executionActions + $deployerActions)
if ($allActions | Where-Object { $_ -eq '*' -or $_ -match ':\*$' }) {
    throw 'Wildcard IAM actions are prohibited in Gate 4 deployment policies.'
}

$prohibited = @(
    'account:DisableRegion', 'account:EnableRegion', 'cloudformation:DeleteStack',
    'iam:AttachUserPolicy', 'iam:CreatePolicyVersion', 'kms:DisableKey',
    'kms:ScheduleKeyDeletion', 'organizations:LeaveOrganization', 's3:BypassGovernanceRetention'
)
foreach ($action in $prohibited) {
    if ($allActions -contains $action) { throw "Prohibited Gate 4 deployment action found: $action" }
}
if ($deployerActions | Where-Object { $_ -match '^s3:Delete' }) {
    throw 'The Gate 4 deployer must not receive any S3 deletion permission.'
}

$approvedQualificationActions = @(
    'iam:SimulatePrincipalPolicy', 'kms:Decrypt', 'kms:DescribeKey', 'kms:Encrypt', 'kms:GenerateDataKey',
    's3:GetObject', 's3:GetObjectAttributes', 's3:GetObjectRetention', 's3:GetObjectVersion',
    's3:ListBucket', 's3:ListBucketVersions', 's3:PutObject'
)
$unexpectedDeployerActions = $deployerActions | Where-Object {
    $_ -notmatch '^cloudformation:' -and $_ -notin @('iam:GetRole', 'iam:PassRole') -and
    $_ -notin $approvedQualificationActions
}
if ($unexpectedDeployerActions) { throw "The deployer policy grants direct service access: $($unexpectedDeployerActions -join ', ')" }
foreach ($action in $approvedQualificationActions) {
    if ($deployerActions -notcontains $action) {
        throw "The deployer policy is missing required Development qualification action: $action"
    }
}
foreach ($driftAction in @('cloudformation:DetectStackDrift', 'cloudformation:DetectStackResourceDrift')) {
    if ($deployerActions -notcontains $driftAction) {
        throw "The deployer policy is missing required drift action: $driftAction"
    }
}
if ($executionActions -notcontains 'cloudwatch:ListTagsForResource') {
    throw 'The execution role must be able to read CloudWatch alarm tags for drift detection.'
}
if ($executionActions -notcontains 'config:DescribeComplianceByConfigRule') {
    throw 'The execution role must be able to read AWS Config rule compliance for drift detection.'
}

$approvedWildcardResourceSids = @(
    'CreateTaggedBackupKeys', 'ManageDevelopmentConfig', 'PermitRequiredBillingApiForDevelopmentBudget',
    'ReadGate4StackListsAndDrift', 'ValidateGate4Templates'
)
foreach ($statement in @($execution.Statement) + @($deployer.Statement)) {
    if (@($statement.Resource) -contains '*' -and $approvedWildcardResourceSids -notcontains $statement.Sid) {
        throw "Unapproved wildcard resource scope found in statement '$($statement.Sid)'."
    }
}

$roleResource = 'arn:aws:iam::107651266250:role/ifm-database-backup-*-development'
$roleStatements = @($execution.Statement | Where-Object { $_.Sid -in @('ManageDevelopmentBackupRoles', 'PassBackupServiceRoles') })
if ($roleStatements.Count -ne 2 -or $roleStatements.Resource | Where-Object { $_ -ne $roleResource }) {
    throw 'The execution role may manage/pass only development-suffixed IFM backup roles.'
}

$passRole = @($deployer.Statement | Where-Object { @($_.Action) -contains 'iam:PassRole' })
if ($passRole.Count -ne 1 -or $passRole[0].Resource -ne $executionRoleArn -or
    $passRole[0].Condition.StringEquals.'iam:PassedToService' -ne 'cloudformation.amazonaws.com') {
    throw 'The deployer may pass only the approved execution role to CloudFormation.'
}

if ($inputs.environment -ne 'Development' -or $inputs.accountId -ne $accountId -or
    $inputs.cloudFormationExecutionRoleArn -ne $executionRoleArn -or
    $inputs.approvalReference -ne 'IFM-GATE4-20260822') {
    throw 'The development deployment inputs do not match the approved Gate 4 scope.'
}
if ([string]::IsNullOrWhiteSpace($inputs.resourceNames.configBucket) -or
    $inputs.resourceNames.configBucket -eq $inputs.resourceNames.auditBucket) {
    throw 'AWS Config must use a dedicated bucket separate from the immutable audit bucket.'
}
$configBucketResources = @(
    "arn:aws:s3:::$($inputs.resourceNames.configBucket)",
    "arn:aws:s3:::$($inputs.resourceNames.configBucket)/*"
)
$executionResources = @($execution.Statement | ForEach-Object { @($_.Resource) })
foreach ($resource in $configBucketResources) {
    if ($executionResources -notcontains $resource) {
        throw "The execution role is missing the dedicated AWS Config bucket resource: $resource"
    }
}
if ($outputs.schemaVersion -ne 1 -or $outputs.environment -ne 'Development' -or
    $outputs.accountId -ne $accountId -or $outputs.primaryRegion -ne 'ca-central-1' -or
    $outputs.recoveryRegion -ne 'ca-west-1') {
    throw 'The captured non-secret stack outputs do not match the approved Development deployment.'
}
if ($qualification.schemaVersion -ne 1 -or $qualification.gate -ne 4 -or
    $qualification.result -ne 'Passed' -or $qualification.approvalReference -ne 'IFM-GATE4-20260822') {
    throw 'The Gate 4 live qualification evidence is incomplete or does not match the approved scope.'
}
$allowedNegativeDecision = @('implicitDeny', 'explicitDeny')
if (@($qualification.negativeIam).Count -lt 9 -or
    @($qualification.negativeIam | Where-Object { $_.decision -notin $allowedNegativeDecision }).Count -gt 0) {
    throw 'Gate 4 negative IAM evidence must contain only denied decisions.'
}
if ($qualification.canary.sourceReplicationStatus -ne 'COMPLETED' -or
    $qualification.canary.destinationReplicationStatus -ne 'REPLICA' -or
    $qualification.canary.objectLockMode -ne 'GOVERNANCE' -or
    $qualification.auditEvidence.httpStatusCode -ne 200 -or
    $qualification.auditEvidence.versionId -ne $qualification.canary.sourceVersionId) {
    throw 'Gate 4 canary replication, retention, or immutable audit evidence is incomplete.'
}
$evidenceText = (Get-Content -LiteralPath $paths.Outputs -Raw) + (Get-Content -LiteralPath $paths.Qualification -Raw)
if ($evidenceText -match '(?i)(AKIA|ASIA)[A-Z0-9]{16}' -or
    $evidenceText -match '(?i)aws_secret_access_key\s*[:=]') {
    throw 'Captured Gate 4 evidence contains an AWS credential pattern.'
}

$allowlistPath = Join-Path $PSScriptRoot 'gate0-identity-allowlist.json'
$allowlist = Get-Content -LiteralPath $allowlistPath -Raw | ConvertFrom-Json
if ($allowlist.environments.Staging.awsMutationAuthorized -or
    $allowlist.environments.Production.awsMutationAuthorized) {
    throw 'Gate 4 Development preparation must not authorize staging or production mutation.'
}

[pscustomobject]@{
    Result = 'Passed'
    PolicyCount = 3
    Environment = $inputs.environment
    AccountId = $inputs.accountId
    MutationPerformed = $false
}
