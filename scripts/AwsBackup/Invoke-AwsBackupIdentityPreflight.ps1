[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Development', 'Staging', 'Production')]
    [string] $Environment = 'Development',

    [Parameter()]
    [ValidatePattern('^[a-z]{2}(?:-gov)?-[a-z]+-\d$')]
    [string] $Region = 'ca-central-1',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $PolicyPath = (Join-Path $PSScriptRoot 'gate0-identity-allowlist.json'),

    [Parameter()]
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

function Stop-Preflight {
    param([Parameter(Mandatory)][string] $Message)

    throw "AWS backup identity preflight rejected: $Message"
}

$resolvedPolicyPath = [System.IO.Path]::GetFullPath($PolicyPath)
if (-not (Test-Path -LiteralPath $resolvedPolicyPath -PathType Leaf)) {
    Stop-Preflight "The identity policy file was not found."
}

try {
    $policy = Get-Content -LiteralPath $resolvedPolicyPath -Raw | ConvertFrom-Json
}
catch {
    Stop-Preflight "The identity policy file is not valid JSON."
}

if ($policy.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace($policy.partition)) {
    Stop-Preflight "The identity policy schema or partition is invalid."
}

$environmentPolicy = $policy.environments.$Environment
if ($null -eq $environmentPolicy) {
    Stop-Preflight "Environment '$Environment' is not configured."
}

$allowedAccounts = @($environmentPolicy.accountIds | Where-Object { $_ -match '^\d{12}$' })
$allowedRegions = @($environmentPolicy.regions | Where-Object { $_ -match '^[a-z]{2}(?:-gov)?-[a-z]+-\d$' })
if ($allowedAccounts.Count -eq 0) {
    Stop-Preflight "Environment '$Environment' has no authorized AWS account."
}
if ($allowedRegions -notcontains $Region) {
    Stop-Preflight "Region '$Region' is not authorized for environment '$Environment'."
}

try {
    Import-Module AWS.Tools.SecurityToken -ErrorAction Stop
}
catch {
    Stop-Preflight "AWS.Tools.SecurityToken is not installed or cannot be loaded."
}

try {
    $identity = Get-STSCallerIdentity -Region $Region -ErrorAction Stop
}
catch {
    Stop-Preflight "STS GetCallerIdentity failed; inspect credentials and network access without logging credential values."
}

$arn = [string] $identity.Arn
$accountId = [string] $identity.Account
$arnParts = $arn.Split(':')
if ($arnParts.Count -lt 6 -or $arnParts[0] -ne 'arn') {
    Stop-Preflight "STS returned a malformed principal ARN."
}

$partition = $arnParts[1]
if ($partition -ne [string] $policy.partition) {
    Stop-Preflight "Partition '$partition' is not authorized."
}
if ($allowedAccounts -notcontains $accountId) {
    Stop-Preflight "Account '$accountId' is not authorized for environment '$Environment'."
}

$result = [pscustomobject]@{
    SchemaVersion         = 1
    Result                = 'Approved'
    Environment           = $Environment
    AccountId             = $accountId
    PrincipalArn          = $arn
    Partition             = $partition
    Region                = $Region
    AwsMutationAuthorized = [bool] $environmentPolicy.awsMutationAuthorized
    CheckedAtUtc          = [DateTimeOffset]::UtcNow.ToString('O')
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 3
}
else {
    $result
}
