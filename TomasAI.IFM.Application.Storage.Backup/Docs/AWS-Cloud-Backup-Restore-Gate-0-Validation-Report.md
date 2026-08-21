# AWS Cloud Backup and Restore Gate 0 Validation Report

**Gate:** 0 - Baseline, decisions, and authorization boundary

**Result:** Complete

**Date:** 2026-08-21

**AWS mutation performed:** None

## 1. Completion summary

Gate 0 is complete for local implementation to proceed to Gate 1. The governing architecture was frozen, repository
and native baselines were inventoried, architecture decisions and recovery objectives were accepted, a threat/cost/
deletion control model was recorded, a fail-closed STS preflight was implemented, and the existing backup test boundary
passed.

The repository-owner instruction to proceed and complete Gate 0 is the acceptance for this development baseline. It
does not substitute for security/operations/database/business approvals required by staging, production, restore,
cutover, legal-hold, or deletion gates.

## 2. Evidence index

| Requirement | Evidence |
| --- | --- |
| Frozen architecture and code/schema/native inventory | `AWS-Cloud-Backup-Restore-Gate-0-Baseline.md` |
| IaC, account/Region, RPO/RTO, retention, crypto, staging, Scylla, and cost decisions | `AWS-Cloud-Backup-Restore-Gate-0-Decision-Record.md` |
| Threat model, data classification, cost estimate model, and resource-deletion policy | `AWS-Cloud-Backup-Restore-Gate-0-Threat-Cost-Deletion-Model.md` |
| Executable identity policy | `scripts/AwsBackup/gate0-identity-allowlist.json` |
| Read-only preflight | `scripts/AwsBackup/Invoke-AwsBackupIdentityPreflight.ps1` |
| Positive/negative acceptance test | `scripts/AwsBackup/Test-AwsBackupIdentityPreflight.ps1` |

## 3. AWS tool installation

| Tool | Result |
| --- | --- |
| Official AWS CLI v2 per-user MSI | Signature valid; signer `Amazon Web Services, Inc.`; installed version 2.36.29 |
| `AWS.Tools.Installer` | Installed for current user |
| `AWS.Tools.Common` | Version 5.0.282 |
| `AWS.Tools.SecurityToken` | Version 5.0.282; `Get-STSCallerIdentity` available |
| `AWS.Tools.S3` | Version 5.0.282; `Get-S3Bucket` available |
| `AWS.Tools.DynamoDBv2` | Version 5.0.282; `Get-DDBTableList` available |
| `AWS.Tools.KeyManagementService` | Version 5.0.282; `Get-KMSKeyList` available |
| PowerShell AWS SDK Core runtime | Version 4.0.102.0 |

The modules were installed under the current user's redirected OneDrive WindowsPowerShell module folder, which was
added to the user `PSModulePath`. Temporary installers/logs created in the workspace were removed after verification.

## 4. Read-only identity qualification

The initial discovery operation was:

```powershell
aws sts get-caller-identity --region ca-central-1 --output json
```

Safe result:

| Field | Result |
| --- | --- |
| Partition | `aws` |
| Account | `107651266250` |
| Principal | `arn:aws:iam::107651266250:user/basil.thomas@live.ca` |
| Region | `ca-central-1` |

The committed acceptance test then returned:

```text
Result: Passed
ApprovedDevelopmentIdentity: True
UnexpectedAccountRejected: True
UnexpectedRegionRejected: True
UnconfiguredProductionRejected: True
AwsMutationAuthorized: False
```

The preflight validates environment, account, partition, and Region and returns only safe identity metadata. It does
not print access-key IDs, secret keys, or session tokens. Staging and production deliberately contain no account IDs.

## 5. Test results

| Project/test | Passed | Skipped | Failed | Result |
| --- | ---: | ---: | ---: | --- |
| `TomasAI.IFM.Domain.SystemAdmin.UnitTests` | 33 | 0 | 0 | Passed |
| `TomasAI.IFM.Domain.SystemAdmin.BDDTests` | 1 | 0 | 0 | Passed |
| `TomasAI.IFM.Domain.SystemAdmin.IntegrationTests` | 3 | 0 | 0 | Passed |
| `TomasAI.IFM.Framework.Storage.IntegrationTests` | 40 | 8 | 0 | Passed |
| AWS identity preflight acceptance | 4 checks | 0 | 0 | Passed |
| **.NET total** | **77** | **8** | **0** | **Passed** |

The eight storage skips are pre-existing explicitly skipped reader/Azure tests, not backup failures. The storage suite
ran for 2 minutes 39 seconds and included the existing backup journal, chain, publication, native capability, and
disposable Docker restore boundary. No current application database was overwritten.

The first sandboxed restore attempt could not reach NuGet (`NU1301`). It was rerun with approved network access; all
final test results above passed. This was an execution-environment restriction, not a product failure.

## 6. Security and format validation

| Check | Result |
| --- | --- |
| PowerShell parser for both new scripts | Passed |
| JSON parse for default and wrong-account policies | Passed |
| Changed-file `AKIA`/`ASIA`/secret-key/session-token assignment scan | Passed; no matches |
| `git diff --check` | Passed |
| Credential values read or written into evidence | No |
| S3/DynamoDB/KMS/IAM/CloudFormation mutable API called | No |

The environment-variable names `aws_access_key_id` and `aws_secret_access_key` were observed. Their values were never
printed, logged, copied, serialized, or committed.

## 7. Runtime inventory and finding disposition

`ifm-nats-server` remained running and healthy. PostgreSQL, Scylla, Scylla Manager, the Scylla backup object store, and
Redis remained running. No Gate 0 command stopped or recreated them.

`G0-F1`: the pre-existing local Database Backup Host container is restarting because 23 journal operations are
recoverable while its online-vault enrollment file is missing. A `DirectoryNotFoundException` escapes the dispatcher
and stops the host. This finding is assigned to Gate 1 before AwsCloud may be enabled. It does not block Gate 0 because:

- AWS is disabled and no AWS resource/evidence exists;
- all shared actor, journal, publication, native, and Docker integration tests pass;
- the missing local enrollment is an existing environment/runtime condition, not a Gate 0 design or AWS identity
  failure; and
- executing stale journal work or fabricating vault enrollment would be unsafe and was not done.

Gate 1 exit evidence must show that an unavailable source/vault produces a bounded failed/degraded operation while the
host remains alive, and that AWS-disabled startup is independent of local source readiness.

## 8. Gate result

| Exit criterion | Result |
| --- | --- |
| Baseline tests pass unchanged | Passed |
| ADRs and threat/control model accepted | Passed for development implementation baseline |
| No credential secret in tracked changes, logs, or evidence | Passed |
| Caller/account/partition/Region matrix explicit | Passed; development allowlisted, staging/production deny all |
| RPO/RTO targets explicit | Passed |
| Read-only STS preflight and negative account/Region tests | Passed |
| No AWS mutation | Passed |

**Final decision:** Gate 0 is complete. Gate 1 may begin. No staging/production identity and no AWS mutation is
authorized by this result.
