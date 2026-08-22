[CmdletBinding()]
param(
    [string] $Root = (Join-Path $PSScriptRoot '..\..\deploy\aws\database-backup'),
    [switch] $ValidateWithAws,
    [string] $Region = 'ca-central-1',
    [string] $RecoveryRegion = 'ca-west-1'
)

$ErrorActionPreference = 'Stop'
$templates = Get-ChildItem -LiteralPath $Root -Filter '*.yaml' -Recurse | Sort-Object FullName
if ($templates.Count -lt 4) { throw 'Gate 4 requires workload, primary, recovery, and audit CloudFormation templates.' }

$required = @(
    'AWS::DynamoDB::Table', 'PointInTimeRecoveryEnabled: true', 'DeletionProtectionEnabled: true',
    'AWS::S3::Bucket', 'ObjectLockEnabled: true', 'VersioningConfiguration:', 'BucketOwnerEnforced',
    'BlockPublicAcls: true', 'aws:SecureTransport', 'AWS::KMS::Key', 'EnableKeyRotation: true',
    'AWS::CloudTrail::Trail', 'AWS::Config::ConfigurationRecorder', 'AWS::Budgets::Budget',
    'ReplicationConfiguration:', 'OperationsFailedReplication', 'DeletionPolicy: Retain', 'UpdateReplacePolicy: Retain',
    'PrimaryAuditBucketName:', 'RecoveryAuditBucketName:', 'AuditBucketName:', 'ConfigBucketName:',
    'AllowDynamoDbServiceUse', 'kms:CallerAccount:', 'kms:ViaService: dynamodb.*.amazonaws.com', 'Unit: USD',
    'AWSConfigKmsDelivery', 'AWSConfigRoleKmsDelivery', "'aws:PrincipalArn':", 'S3KmsKeyArn:',
    'AWSConfigBucketPermissionsCheck', 'AWSConfigBucketExistenceCheck', 'AWSConfigBucketDelivery',
    'AWSConfigRoleBucketCheck', 'AWSConfigRoleBucketDelivery', 'DependsOn: AuditBucketPolicy',
    'DependsOn: ConfigBucketPolicy', 'ConfigDeliveryBucketMustBeSeparate', 'SecurityAuditPrincipalArn:',
    'SecurityAuditList', 'SecurityAuditRead'
)
$all = ($templates | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
foreach ($token in $required) {
    if ($all.IndexOf($token, [StringComparison]::Ordinal) -lt 0) { throw "Required Gate 4 control is missing: $token" }
}

if ($all -match '(?im)^\s*AccessControl:\s*(PublicRead|PublicReadWrite)\s*$') { throw 'A public S3 ACL is prohibited.' }
if ($all -match '(?im)\bUnit:\s*CAD\b') { throw 'AWS Budgets supports USD; CAD budget units are prohibited.' }
$auditTemplate = Get-Content -LiteralPath (Join-Path $Root 'policy\audit.yaml') -Raw
$configBucketBlock = [regex]::Match($auditTemplate, '(?ms)^  ConfigBucket:\r?\n.*?(?=^  [A-Za-z][A-Za-z0-9]*:\r?$)').Value
if ([string]::IsNullOrWhiteSpace($configBucketBlock) -or $configBucketBlock -match 'ObjectLock') {
    throw 'The AWS Config delivery bucket must exist without Object Lock default retention.'
}
$deliveryChannelBlock = [regex]::Match($auditTemplate, '(?ms)^  ConfigurationDeliveryChannel:\r?\n.*?(?=^  [A-Za-z][A-Za-z0-9]*:\r?$)').Value
if ($deliveryChannelBlock -notmatch 'S3BucketName:\s*!Ref ConfigBucket') {
    throw 'The AWS Config delivery channel must target the separate Config bucket.'
}
foreach ($template in $templates) {
    $effect = ''
    foreach ($line in Get-Content -LiteralPath $template.FullName) {
        if ($line -match '^\s*-?\s*Effect:\s*(Allow|Deny)\s*$') { $effect = $Matches[1] }
        if ($effect -eq 'Allow' -and $line -match '^\s*Action:\s*.*(?:\*|s3:BypassGovernanceRetention|s3:PutReplicationConfiguration)') {
            throw "An allow statement grants a wildcard or protected control-plane action: $($template.FullName)"
        }
        if ($line -match '^\s*-\s*Sid:' ) { $effect = '' }
    }
}

foreach ($template in $templates) {
    $text = Get-Content -Raw -LiteralPath $template.FullName
    if ($text -match '\t') { throw "Tabs are prohibited in CloudFormation YAML: $($template.FullName)" }
    if ($text -notmatch "^AWSTemplateFormatVersion:") { throw "CloudFormation header missing: $($template.FullName)" }
    if ($ValidateWithAws) {
        $awsExecutable = (Get-Command aws.exe -ErrorAction SilentlyContinue).Source
        if ([string]::IsNullOrWhiteSpace($awsExecutable)) {
            $candidate = Join-Path $env:LOCALAPPDATA 'Programs\Amazon\AWSCLIV2\aws.exe'
            if (Test-Path -LiteralPath $candidate) { $awsExecutable = $candidate }
        }
        if ([string]::IsNullOrWhiteSpace($awsExecutable)) { throw 'AWS CLI v2 was not found.' }
        $templateRegion = if ($template.FullName -match '[\\/]recovery-vault[\\/]') { $RecoveryRegion } else { $Region }
        & $awsExecutable cloudformation validate-template --region $templateRegion --template-body "file://$($template.FullName)" --no-cli-pager --output json | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "AWS rejected CloudFormation template: $($template.FullName)" }
    }
}

$iamPolicyResult = & (Join-Path $PSScriptRoot 'Test-AwsBackupIamPolicies.ps1')

$changeSetScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'New-AwsBackupChangeSet.ps1') -Raw
if ($changeSetScript -notmatch "PSObject\.Properties\['AwsMutationAuthorized'\]") {
    throw 'The change-set workflow must enforce the identity-preflight AwsMutationAuthorized contract.'
}

[pscustomobject]@{
    Result = 'Passed'
    TemplateCount = $templates.Count
    IamPolicyCount = $iamPolicyResult.PolicyCount
    AwsValidation = [bool]$ValidateWithAws
    MutationPerformed = $false
}
