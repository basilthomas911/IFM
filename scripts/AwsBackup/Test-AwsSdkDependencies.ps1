[CmdletBinding()]
param([switch] $OnlineAudit)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\..\TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud\TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.csproj'
[xml] $xml = Get-Content -Raw -LiteralPath $project
$aws = @($xml.Project.ItemGroup.PackageReference | Where-Object { $_.Include -like 'AWSSDK.*' })
$expected = @('AWSSDK.DynamoDBv2', 'AWSSDK.KeyManagementService', 'AWSSDK.S3', 'AWSSDK.SecurityToken')
$actualNames = (@($aws.Include | Sort-Object) -join '|')
$expectedNames = (@($expected | Sort-Object) -join '|')
if ($actualNames -ne $expectedNames) {
    throw 'The AWS adapter must directly reference exactly the four approved AWS SDK packages.'
}
foreach ($package in $aws) {
    if ($package.Version -notmatch '^4\.\d+(?:\.\d+){1,3}$') { throw "Package $($package.Include) is not pinned to a stable AWS SDK v4 version." }
}
if ($OnlineAudit) {
    & dotnet list $project package --deprecated --include-transitive
    if ($LASTEXITCODE -ne 0) { throw 'Deprecated-package audit failed.' }
    & dotnet list $project package --vulnerable --include-transitive
    if ($LASTEXITCODE -ne 0) { throw 'Vulnerability audit failed.' }
}
[pscustomobject]@{ Result = 'Passed'; DirectAwsPackageCount = $aws.Count; OnlineAudit = [bool]$OnlineAudit }
