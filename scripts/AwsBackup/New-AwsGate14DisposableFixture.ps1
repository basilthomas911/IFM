[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $Publish
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot '../../.artifacts/aws-gate14-fixture' }
$account = '107651266250'
$bucket = 'ifm-db-backup-development-primary-107651266250'
$region = 'ca-central-1'
$keyArn = 'arn:aws:kms:ca-central-1:107651266250:key/4772d4b1-82d9-49fc-acca-b97e73fe93df'
function Read-Aws([string[]] $Arguments) {
    $response = & aws @Arguments --region $region --no-cli-pager --output json
    if ($LASTEXITCODE -ne 0) { throw "AWS operation failed: $($Arguments[0..1] -join ' ')" }
    return ($response -join "`n") | ConvertFrom-Json
}
$identity = Read-Aws @('sts', 'get-caller-identity')
if ($identity.Account -ne $account) { throw 'Gate 14 preparation requires the Development account.' }
$lock = Read-Aws @('s3api', 'get-object-lock-configuration', '--bucket', $bucket)
$retention = $lock.ObjectLockConfiguration.Rule.DefaultRetention
if ($lock.ObjectLockConfiguration.ObjectLockEnabled -ne 'Enabled' -or $retention.Mode -ne 'GOVERNANCE' -or $retention.Days -ne 35) {
    throw 'Unexpected Development Object Lock policy. No fixture will be published.'
}
$runId = [guid]::NewGuid().ToString('N')
$directory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $runId
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$objects = @()
foreach ($role in @('candidate', 'keep')) {
    $payload = [ordered]@{ schemaVersion=1; fixtureRunId=$runId; purpose='Gate14DisposableRetentionFixture'; role=$role; containsApplicationData=$false }
    $path = Join-Path $directory ($role + '.json')
    [IO.File]::WriteAllText($path, ($payload | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
    $bytes = [IO.File]::ReadAllBytes($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = $sha.ComputeHash($bytes) } finally { $sha.Dispose() }
    $objects += [ordered]@{
        role=$role; bucketName=$bucket; region=$region
        objectKey="v1/environment/development/gate14/disposable/$runId/$role.json"
        localPath=$path; length=$bytes.Length
        sha256=([BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant())
        checksumSha256Base64=[Convert]::ToBase64String($hash)
        versionId=$null; observedRetainUntilUtc=$null
    }
}
$request = [ordered]@{
    schemaVersion=1; runId=$runId; environment='Development'
    preparationUtc=[DateTimeOffset]::UtcNow.ToString('O'); defaultRetentionDays=35
    purpose='Retention-only synthetic fixture; not a database backup or verified restore point'
    protectedRoles=@('keep'); deletionCandidateRoles=@('candidate')
    publicationComplete=$false; approvedForDeletion=$false; objects=$objects
}
$requestPath = Join-Path $directory 'fixture-request.json'
function Save-Request { [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false)) }
Save-Request
$objectArns = @($objects | ForEach-Object { "arn:aws:s3:::$bucket/$($_.objectKey)" })
$policy = [ordered]@{ Version='2012-10-17'; Statement=@(
    @{ Sid='PublishExactGate14FixtureObjects'; Effect='Allow'; Action=@('s3:PutObject','s3:GetObjectVersion','s3:GetObjectRetention','s3:GetObjectLegalHold'); Resource=$objectArns },
    @{ Sid='ReadDevelopmentLock'; Effect='Allow'; Action=@('s3:GetBucketObjectLockConfiguration'); Resource="arn:aws:s3:::$bucket" },
    @{ Sid='FixtureEncryption'; Effect='Allow'; Action=@('kms:GenerateDataKey','kms:Decrypt'); Resource=$keyArn; Condition=@{ StringEquals=@{ 'kms:ViaService'='s3.ca-central-1.amazonaws.com' }; StringLike=@{ 'kms:EncryptionContext:aws:s3:arn'=@("arn:aws:s3:::$bucket", "arn:aws:s3:::$bucket/v1/environment/development/gate14/disposable/$runId/*") } } }
) }
[IO.File]::WriteAllText((Join-Path $directory 'fixture-publication-policy.json'), ($policy | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
if ($Publish) {
    foreach ($item in $objects) {
        $put = Read-Aws @('s3api','put-object','--bucket',$bucket,'--key',$item.objectKey,'--body',$item.localPath,
            '--if-none-match','*','--server-side-encryption','aws:kms','--ssekms-key-id',$keyArn,
            '--checksum-algorithm','SHA256','--checksum-sha256',$item.checksumSha256Base64,
            '--metadata',"qualification=gate14-disposable,fixture-run=$runId,fixture-role=$($item.role)")
        if ([string]::IsNullOrWhiteSpace($put.VersionId)) { throw 'S3 did not return an exact version.' }
        $item.versionId = $put.VersionId
        Save-Request # Preserve partial publication evidence before further verification.
        $observed = Read-Aws @('s3api','get-object-retention','--bucket',$bucket,'--key',$item.objectKey,'--version-id',$item.versionId)
        $item.observedRetainUntilUtc = $observed.Retention.RetainUntilDate
        if ($observed.Retention.Mode -ne 'GOVERNANCE' -or [DateTimeOffset]$item.observedRetainUntilUtc -le [DateTimeOffset]::UtcNow) { throw 'Fixture retention could not be verified.' }
        Save-Request
    }
    $request.publicationComplete = $true
    Save-Request
}
Write-Output $requestPath
