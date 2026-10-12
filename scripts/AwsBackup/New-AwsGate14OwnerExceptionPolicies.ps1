[CmdletBinding()]
param([string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$directory = Join-Path $repository 'deploy/aws/database-backup/environments/development'
$approvalPath = Join-Path $directory 'gate14-development-exception-approval-20261010.json'
$approval = Get-Content -LiteralPath $approvalPath -Raw | ConvertFrom-Json
$packet = Get-Content -LiteralPath (Join-Path $directory 'gate14-readiness-packet-20261010.json') -Raw | ConvertFrom-Json
$candidate = '3291d2ff9099419aa6c9b3e08ecde95b'
$preserved = 'b4c472227f044f419ac975f29a2463af'
$principal = 'arn:aws:iam::107651266250:user/basil.thomas@live.ca'
if ($approval.environment -ne 'Development' -or $approval.candidateRestorePointId -ne $candidate -or
    $approval.preservedRestorePointId -ne $preserved -or $approval.objects.Count -ne 16 -or
    $approval.approvalReference -ne 'UserApproval-Gate14-16DisposableVersions-20261010') {
    throw 'The exact owner-approved Development exception is required.'
}
$expiry = [DateTimeOffset]::Parse($approval.exceptionExpiresUtc).ToUniversalTime()
if ($expiry -le [DateTimeOffset]::UtcNow -or $expiry -gt [DateTimeOffset]::UtcNow.AddHours(24)) {
    throw 'The exception must be unexpired and bounded to 24 hours. Do not extend it automatically.'
}
$expected = @($packet.objects | Where-Object role -eq 'candidate' | ForEach-Object exactObjects)
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($item in $approval.objects) {
    $identity = "$($item.BucketName)|$($item.ObjectKey)|$($item.VersionId)"
    if (!$seen.Add($identity) -or [string]::IsNullOrWhiteSpace($item.VersionId) -or
        $item.ObjectKey.Contains($preserved) -or !$item.ObjectKey.Contains($candidate) -or
        $item.ObjectKey -match '[*?]') { throw 'Invalid, duplicate or unapproved object identity.' }
    $matching = @($expected | Where-Object {
        $_.bucket -ceq $item.BucketName -and $_.key -ceq $item.ObjectKey -and
        $_.versionId -ceq $item.VersionId -and $_.length -eq $item.Length -and $_.sha256 -eq $item.Sha256
    })
    if ($matching.Count -ne 1) { throw 'The exception does not match the original approved inventory.' }
}
$files = @()
foreach ($replica in @('primary', 'recovery')) {
    $bucket = "ifm-db-backup-development-$replica-107651266250"
    $items = @($approval.objects | Where-Object BucketName -eq $bucket)
    if ($items.Count -ne 8) { throw 'Exactly eight versions per vault are required.' }
    $keys = @($items | ForEach-Object { "arn:aws:s3:::$bucket/$($_.ObjectKey)" })
    if (@($keys | Select-Object -Unique).Count -ne 8) { throw 'Exactly eight distinct keys per vault are required.' }
    $commonCondition = @{
        DateLessThan = @{ 'aws:CurrentTime' = $expiry.ToString('o') }
        ArnEquals = @{ 'aws:PrincipalArn' = $principal }
    }
    $deletion = @()
    $index = 0
    foreach ($item in $items) {
        $index++
        $deletion += [ordered]@{
            Sid = "ApprovedVersion$index"; Effect = 'Allow'
            Action = @('s3:DeleteObjectVersion', 's3:GetObjectVersion')
            Resource = "arn:aws:s3:::$bucket/$($item.ObjectKey)"
            Condition = @{
                StringEquals = @{ 's3:VersionId' = $item.VersionId }
                DateLessThan = $commonCondition.DateLessThan
                ArnEquals = $commonCondition.ArnEquals
            }
        }
    }
    # AWS does not support s3:VersionId conditions for these protection actions.
    # These are KEY permissions. Actual mutations remain restricted to the approved version IDs.
    $protection = @(
        [ordered]@{ Sid = 'ReadAndShortenDisposableProtection'; Effect = 'Allow'
            Action = @('s3:GetObjectLegalHold', 's3:GetObjectRetention', 's3:PutObjectRetention', 's3:BypassGovernanceRetention')
            Resource = $keys; Condition = $commonCondition },
        [ordered]@{ Sid = 'SetDisposableHoldOffOnly'; Effect = 'Allow'; Action = 's3:PutObjectLegalHold'
            Resource = $keys; Condition = @{
                StringEquals = @{ 's3:object-lock-legal-hold' = 'OFF' }
                DateLessThan = $commonCondition.DateLessThan
                ArnEquals = $commonCondition.ArnEquals
            } }
    )
    foreach ($part in @(@{ Name = 'deletion'; Statements = $deletion }, @{ Name = 'protection'; Statements = $protection })) {
        $policy = [ordered]@{ Version = '2012-10-17'; Statement = @($part.Statements) }
        $compact = $policy | ConvertTo-Json -Depth 12 -Compress
        if ($compact.Length -gt 6144) { throw 'Policy exceeds the customer-managed policy size limit.' }
        $filename = "gate14-owner-$replica-$($part.Name)-policy-20261010.json"
        [IO.File]::WriteAllText((Join-Path $directory $filename), ($policy | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
        $files += [ordered]@{ file = $filename; compactCharacters = $compact.Length; replica = $replica; scope = $part.Name }
    }
}
$approval | Add-Member -NotePropertyName ownerConsolePermissions -NotePropertyValue ([ordered]@{
    iamUser = $principal; mfaRoleRequired = $false; policyDocuments = $files
    protectionPermissionScope = '16 exact object keys; not version-bound in IAM. Actual changes are authorized only for the 16 recorded version IDs.'
    deletionPermissionScope = '16 exact bucket/key/version tuples'
    deployed = $false; cloudPolicyValidation = 'Pending console validation and installation by owner'
}) -Force
$approval | Add-Member -NotePropertyName administratorActionRequired -NotePropertyValue 'Owner signs in using existing AWS console access and attaches the four validated temporary customer-managed policies directly to the named IAM user. No root access keys or MFA role are required.' -Force
$approval | Add-Member -NotePropertyName blocker -NotePropertyValue 'Temporary direct-user policies await console installation. No protection change or deletion has occurred.' -Force
[IO.File]::WriteAllText($approvalPath, ($approval | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ Result = 'Prepared'; ExactVersions = 16; ObjectKeys = 16; ExpiresUtc = $expiry; AwsMutationPerformed = $false; Policies = $files }