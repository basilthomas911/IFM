[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [ValidateSet('Development', 'Staging', 'Production')] [string] $Environment,
    [Parameter(Mandatory)] [string] $StackName,
    [Parameter(Mandatory)] [string] $TemplateFile,
    [Parameter(Mandatory)] [string] $Region,
    [Parameter(Mandatory)] [string] $ApprovalReference,
    [Parameter(Mandatory)] [string[]] $Parameters,
    [string] $AllowlistPath = (Join-Path $PSScriptRoot 'gate0-identity-allowlist.json')
)

$ErrorActionPreference = 'Stop'
if ($ApprovalReference.Length -lt 8 -or ($ApprovalReference.ToCharArray() | Where-Object { [char]::IsControl($_) })) {
    throw 'A bounded reviewed implementation-plan approval reference is required.'
}
$identity = & (Join-Path $PSScriptRoot 'Invoke-AwsBackupIdentityPreflight.ps1') -Environment $Environment -Region $Region -PolicyPath $AllowlistPath
if (-not $identity.MutationAuthorized) { throw 'AWS mutation is not authorized by the selected environment policy.' }
& (Join-Path $PSScriptRoot 'Test-AwsBackupInfrastructure.ps1') | Out-Null

$changeSetName = "ifm-backup-$($Environment.ToLowerInvariant())-$(Get-Date -Format 'yyyyMMddHHmmss')"
if ($PSCmdlet.ShouldProcess("$StackName in $Region", "Create reviewed CloudFormation change set $changeSetName")) {
    & aws cloudformation create-change-set --stack-name $StackName --change-set-name $changeSetName `
        --change-set-type UPDATE --template-body "file://$((Resolve-Path -LiteralPath $TemplateFile).Path)" `
        --parameters $Parameters --capabilities CAPABILITY_NAMED_IAM --region $Region `
        --description "IFM Gate 4 approval $ApprovalReference" --output json
    if ($LASTEXITCODE -ne 0) { throw 'CloudFormation change-set creation failed.' }
}

# Deliberately no execute-change-set call. Independent review and execution are separate actions.
