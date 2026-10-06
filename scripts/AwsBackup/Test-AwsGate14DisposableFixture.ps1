[CmdletBinding()]
param([Parameter(Mandatory)][string] $FixturePath)
$ErrorActionPreference = 'Stop'
$fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
if ($fixture.environment -ne 'Development' -or $fixture.runId -notmatch '^[a-f0-9]{32}$' -or !$fixture.publicationComplete) { throw 'A complete Development fixture descriptor is required.' }
$account = (& aws sts get-caller-identity --query Account --output text --no-cli-pager)
if ($LASTEXITCODE -ne 0 -or $account -ne '107651266250') { throw 'Wrong AWS account.' }
$observations = @()
foreach ($item in $fixture.objects) {
    if ($item.role -notin @('candidate','keep') -or $item.objectKey -ne "v1/environment/development/gate14/disposable/$($fixture.runId)/$($item.role).json" -or $item.length -gt 1024 -or [string]::IsNullOrWhiteSpace($item.versionId)) { throw 'Invalid exact fixture identity or size.' }
    foreach ($replica in @('primary','recovery')) {
        $region = if ($replica -eq 'primary') { 'ca-central-1' } else { 'ca-west-1' }
        $bucket = "ifm-db-backup-development-$replica-107651266250"
        $path = Join-Path (Split-Path -Parent $FixturePath) "$($item.role)-$replica-readback.json"
        # S3 replication preserves source version IDs. Do not substitute a latest version if this exact ID is missing.
        $response = & aws s3api get-object --bucket $bucket --key $item.objectKey --version-id $item.versionId --region $region --no-cli-pager --output json $path
        if ($LASTEXITCODE -ne 0) { throw "Exact-version readback failed: $replica/$($item.role)" }
        $response = ($response -join "`n") | ConvertFrom-Json
        $actualLength = (Get-Item -LiteralPath $path).Length
        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualLength -ne $item.length -or $actualHash -ne $item.sha256 -or $response.VersionId -ne $item.versionId) { throw 'Exact-version content drift.' }
        if ($response.ObjectLockMode -ne 'GOVERNANCE' -or [string]::IsNullOrWhiteSpace($response.ObjectLockRetainUntilDate)) { throw 'Missing observed Governance retention.' }
        $observations += [ordered]@{ replica=$replica; role=$item.role; bucketName=$bucket; region=$region; objectKey=$item.objectKey; versionId=$response.VersionId; length=$actualLength; sha256=$actualHash; retainUntilUtc=$response.ObjectLockRetainUntilDate; legalHoldStatus=$response.ObjectLockLegalHoldStatus; legalHoldVerified=($response.ObjectLockLegalHoldStatus -eq 'OFF'); replicationStatus=$response.ReplicationStatus }
    }
}
$result = [ordered]@{ schemaVersion=1; runId=$fixture.runId; observedUtc=[DateTimeOffset]::UtcNow.ToString('O'); exactVersionContentVerified=$true; deletionAuthorized=$false; observations=$observations }
$output = Join-Path (Split-Path -Parent $FixturePath) 'verified-observations.json'
[IO.File]::WriteAllText($output, ($result | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
Write-Output $output
