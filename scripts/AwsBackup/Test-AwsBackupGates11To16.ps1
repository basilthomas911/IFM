[CmdletBinding()]
param(
    [string] $RepositoryRoot,
    [switch] $SkipDependencyAudit
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..\..'
}
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$testProject = Join-Path $repository 'TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests\TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests.csproj'
$runbook = Join-Path $repository 'TomasAI.IFM.Application.Storage.Backup\Docs\AWS-Cloud-Backup-Restore-Operations-Runbook.md'
if (-not (Test-Path -LiteralPath $testProject -PathType Leaf)) { throw 'The AWS backup unit-test project is missing.' }
if (-not (Test-Path -LiteralPath $runbook -PathType Leaf)) { throw 'The Gate 15 operations runbook is missing.' }

$requiredRunbooks = @(
    'Credential failure', 'Wrong-account rejection', 'KMS/key recovery', 'WAL gap', 'Replication failure',
    'Journal PITR', 'Multipart reconciliation', 'Catalog rebuild', 'Primary-vault loss and recovery-only restore',
    'Legal hold', 'Retention-plan failure', 'Fresh-target cleanup'
)
$runbookText = Get-Content -LiteralPath $runbook -Raw
foreach ($section in $requiredRunbooks) {
    if ($runbookText.IndexOf("## $section", [StringComparison]::Ordinal) -lt 0) {
        throw "The Gate 15 runbook is missing section '$section'."
    }
}

$tracked = & git -C $repository ls-files
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate tracked files for secret scanning.' }
foreach ($relative in $tracked) {
    if ($relative -notmatch '\.(cs|json|md|ps1|yaml|yml|props|targets|csproj)$') { continue }
    $path = Join-Path $repository $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $content = Get-Content -LiteralPath $path -Raw
    if ($content -match '(?i)(AKIA|ASIA)(?![A-Z0-9]{0,16}(?:EXAMPLE|EXPIRED|FAKE|TEST))[A-Z0-9]{16}' -or
        $content -match '(?i)aws_secret_access_key\s*[:=]\s*[A-Za-z0-9/+=]{20,}') {
        throw "A tracked file contains an AWS credential pattern: $relative"
    }
}

$infrastructure = & (Join-Path $PSScriptRoot 'Test-AwsBackupInfrastructure.ps1')
if ($LASTEXITCODE -ne 0 -or $infrastructure.Result -ne 'Passed') {
    throw 'AWS infrastructure/IAM policy scanning failed.'
}

& dotnet build $testProject --no-restore --configuration Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Gate 16 warning-free Release build failed.' }
& dotnet test $testProject --no-build --no-restore --configuration Release `
    --filter 'Category=Gate11|Category=Gate12|Category=Gate13|Category=Gate14|Category=Gate15|Category=Gate16' `
    --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) { throw 'The Gates 11-16 deterministic qualification selection failed.' }

$dependencyAudit = 'Skipped'
if (-not $SkipDependencyAudit) {
    $auditOutput = & dotnet list $testProject package --vulnerable --include-transitive --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Dependency vulnerability scan failed: $($auditOutput -join [Environment]::NewLine)" }
    if (($auditOutput -join "`n") -match '(?i)has the following vulnerable packages') {
        throw 'Dependency vulnerability scan found one or more vulnerable packages.'
    }
    $dependencyAudit = 'Passed'
}

[pscustomobject]@{
    Result = 'Passed'
    Gates = '11-16'
    InfrastructureAndIamScan = 'Passed'
    SecretScan = 'Passed'
    WarningFreeReleaseBuild = 'Passed'
    DeterministicFaultAndPolicyTests = 'Passed'
    DependencyVulnerabilityAudit = $dependencyAudit
    MutationPerformed = $false
}
