[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$preflight = Join-Path $PSScriptRoot 'Invoke-AwsBackupIdentityPreflight.ps1'
$wrongAccountPolicy = Join-Path $PSScriptRoot 'test-fixtures\gate0-wrong-account-allowlist.json'

$positive = & $preflight -Environment Development -Region ca-central-1
if ($positive.Result -ne 'Approved' -or -not $positive.AwsMutationAuthorized) {
    throw 'The approved development identity preflight did not return the expected Gate 4 mutation authorization.'
}

$wrongAccountRejected = $false
try {
    & $preflight -Environment Development -Region ca-central-1 -PolicyPath $wrongAccountPolicy | Out-Null
}
catch {
    $wrongAccountRejected = $_.Exception.Message -like '*Account*is not authorized*'
}
if (-not $wrongAccountRejected) {
    throw 'The preflight did not reject an unexpected AWS account.'
}

$wrongRegionRejected = $false
try {
    & $preflight -Environment Development -Region us-east-1 | Out-Null
}
catch {
    $wrongRegionRejected = $_.Exception.Message -like '*Region*is not authorized*'
}
if (-not $wrongRegionRejected) {
    throw 'The preflight did not reject an unexpected AWS Region.'
}

$unconfiguredEnvironmentRejected = $false
try {
    & $preflight -Environment Production -Region ca-central-1 | Out-Null
}
catch {
    $unconfiguredEnvironmentRejected = $_.Exception.Message -like '*has no authorized AWS account*'
}
if (-not $unconfiguredEnvironmentRejected) {
    throw 'The preflight did not reject an environment with no authorized AWS account.'
}

[pscustomobject]@{
    Result                          = 'Passed'
    ApprovedDevelopmentIdentity     = $true
    UnexpectedAccountRejected       = $wrongAccountRejected
    UnexpectedRegionRejected        = $wrongRegionRejected
    UnconfiguredProductionRejected  = $unconfiguredEnvironmentRejected
    AwsMutationAuthorized           = $positive.AwsMutationAuthorized
}
