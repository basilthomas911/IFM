[CmdletBinding()]
param(
    [string] $Root = (Join-Path $PSScriptRoot '..\..\deploy\aws\database-backup'),
    [switch] $ValidateWithAws,
    [string] $Region = 'ca-central-1'
)

$ErrorActionPreference = 'Stop'
$templates = Get-ChildItem -LiteralPath $Root -Filter '*.yaml' -Recurse | Sort-Object FullName
if ($templates.Count -lt 4) { throw 'Gate 4 requires workload, primary, recovery, and audit CloudFormation templates.' }

$required = @(
    'AWS::DynamoDB::Table', 'PointInTimeRecoveryEnabled: true', 'DeletionProtectionEnabled: true',
    'AWS::S3::Bucket', 'ObjectLockEnabled: true', 'VersioningConfiguration:', 'BucketOwnerEnforced',
    'BlockPublicAcls: true', 'aws:SecureTransport', 'AWS::KMS::Key', 'EnableKeyRotation: true',
    'AWS::CloudTrail::Trail', 'AWS::Config::ConfigurationRecorder', 'AWS::Budgets::Budget',
    'ReplicationConfiguration:', 'OperationsFailedReplication', 'DeletionPolicy: Retain', 'UpdateReplacePolicy: Retain'
)
$all = ($templates | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
foreach ($token in $required) {
    if ($all.IndexOf($token, [StringComparison]::Ordinal) -lt 0) { throw "Required Gate 4 control is missing: $token" }
}

if ($all -match '(?im)^\s*AccessControl:\s*(PublicRead|PublicReadWrite)\s*$') { throw 'A public S3 ACL is prohibited.' }
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
        & aws cloudformation validate-template --region $Region --template-body "file://$($template.FullName)" --output json | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "AWS rejected CloudFormation template: $($template.FullName)" }
    }
}

[pscustomobject]@{
    Result = 'Passed'
    TemplateCount = $templates.Count
    AwsValidation = [bool]$ValidateWithAws
    MutationPerformed = $false
}
