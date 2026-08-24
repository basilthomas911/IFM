# AWS Cloud Backup and Restore Gates 11-16 Validation Report

Date: 2026-08-23 (continued 2026-08-24 UTC)
Environment: Development  
Result: Development Gates 11-13 and 15-16 qualified; Gate 14 awaits the first lawfully expired non-evidence version

## Scope and safety boundary

This qualification pass implemented and tested the deterministic safety and runtime slice for Gates 11 through 16,
deployed the reviewed Development observability update, published the complete two-node Scylla snapshot to both
immutable vaults, restored from each vault, and ran a reversible alarm drill. It did not delete an object version,
shorten retention, replace retained Gates 4-10 evidence, or authorize Staging/Production.

## Results

| Gate | Implemented and verified | Remaining exit evidence |
| ---: | --- | --- |
| 11 | **Development complete.** The complete two-node snapshot `sm_20260824031940UTC` was published through both immutable AWS replicas as operation/restore point `13db2954f0dc484abd247606891cf6f9`. The signed contract bound two live nodes to 406 portable artifact references. Missing-node and Manager-restart reconciliation evidence also passed. | Literal production-scale topology/load remains a production-readiness activity. |
| 12 | **Development complete.** Fresh schema/table restores from both primary and recovery vaults passed Manager repair and `CONSISTENCY ALL`. Primary RTO was 32.149 seconds; recovery RTO was 75.349 seconds. | Production RPO/RTO targets require production-shaped staging. |
| 13 | **Development complete.** Exact signed versions, checksums, retention, Region, KMS key, replica identity, and recovery-only Scylla selection passed after the primary client was disposed. | Literal cross-account proof is deferred until a separately controlled recovery account exists. |
| 14 | Deterministic retention planning preserves newest restore points and dependency closure. Signed authorization and exact-version execution reject revision drift, legal hold, unexpired retention, replica gaps, object drift, duplicates, and partial failures. | Independently approve and execute a plan containing only expired non-evidence versions, then reconcile its AWS results. |
| 15 | **Development complete.** The stack is `UPDATE_COMPLETE`; dashboard, sixteen routed alarms, runbook URLs, SNS topic, and confirmed email subscription are deployed. The KMS-denial alarm entered `ALARM` with the exact SNS route/runbook and reset to `OK`. | AWS drift detection returns an internal failure only for `AWS::CloudWatch::Dashboard`; direct dashboard retrieval/validation passes and every other checkable resource is in sync. |
| 16 | **Development complete.** Live publication/replication/two-vault restore load, capacity limits, cost query, bounded metric backpressure, missing-node injection, Manager restart, warning-free build, secret/IAM scans, deterministic fault tests, and dependency vulnerability audit passed. | Tagged Cost Explorer allocation had not populated yet; production-scale load/security acceptance belongs to production-shaped staging. |

## Automated evidence

- AWS cloud unit suite: 90 passed, 0 failed.
- Current Gate 11-16 deterministic selection: 47 passed, 0 failed.
- Focused Gate 15-16 selection: 21 passed, 0 failed.
- AWS cloud integration suite: 28 passed, 0 failed.
- Synthetic Scylla capability and Manager CLI selection: 4 passed, 0 failed.
- Disposable native Scylla Docker restore: 1 passed, 0 failed.
- Deterministic Manager/capability Scylla selection after node binding: 5 passed, 0 failed.
- Full solution Release build: passed sequentially with 0 warnings and 0 errors.
- NuGet direct/transitive vulnerability audit: no vulnerable packages reported.
- Gate qualification script: infrastructure/IAM scan passed, tracked-source credential scan passed, warning-free Release build passed, and `MutationPerformed` was `False`.
- Live two-vault Scylla qualification: 1 passed in 7 minutes 38 seconds; operation/restore point
  `13db2954f0dc484abd247606891cf6f9`, 329,962 portable bytes, 406 artifact references, two source nodes.
- Gate 15 reversible alert drill: passed and reset to `OK`.

The repeatable local command is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/AwsBackup/Test-AwsBackupGates11To16.ps1
```

Use `-SkipDependencyAudit` only when the package advisory source is unavailable; the audit was run separately and passed for this report.

## Deferred qualification boundaries

The remaining work is not safe to infer from Development authorization alone. In particular:

1. The bounded live-qualification user policy is detached between qualification windows and retained for Gate 14.
2. Do not delete or shorten retention for the retained Gates 4-10 qualification objects, journal PITR target, alarm, or recovery evidence.
3. Gate 13 can prove cross-Region recovery in the current account, but literal cross-account qualification requires a separately controlled recovery account or an approved equivalent boundary.
4. Gate 14 requires a reviewed, signed plan that names only expired non-evidence bucket/key/version tuples, followed by independent authorization before the constrained executor runs.

Development Gates 11-13 and 15-16 are **Complete**. Gate 14 remains **In progress** until an eligible version expires;
Gate 17 must not start before that exact-version retention qualification is complete.

The temporary `IFM-Gates5-10-LiveQualification` policy was detached from `basil.thomas@live.ca` after qualification.
An exact `cloudwatch:GetDashboard` request then returned the expected identity-policy `AccessDenied`, while the
permanent workload stack remained `UPDATE_COMPLETE`. The policy object is retained for a bounded Gate 14 window.

## Continuation evidence and current blocker

The local Manager evidence is now real multi-node evidence rather than a single-node approximation:

- Source cluster: `ifm-gate11-source`, nodes `172.30.20.12` and `172.30.20.13`, both `UN`, CQL/REST `UP`.
- Complete reconciliation task: `backup/gate11-reconciliation-20260824`, one success, zero errors, snapshot
  `sm_20260824031940UTC`, 312.873 KiB, two nodes.
- Partial-node task: `backup/gate11-node-failure-20260824`. Manager reported node 2 `DN`, held the task at zero-progress
  `RUNNING`, and created `sm_20260824031918UTC`, 118.891 KiB, one node. The exact task was stopped and disabled; the
  incomplete snapshot is retained only as local failure evidence and cannot satisfy signed node completeness.
- Manager was restarted after the healthy retry; the complete task and two-node snapshot remained visible.
- The application cluster `ifm-development` was not stopped or modified.
- After the successful two-vault qualification, both isolated source nodes and both disposable restore nodes were
  stopped. Their named volumes, Manager metadata, local object store, and immutable AWS evidence remain intact.
- Idle post-drill capacity was captured read-only: the two isolated source nodes used 91.38 MiB and 84.58 MiB,
  Manager used 28.17 MiB, and MinIO used 122.9 MiB. CPU was below 1% per source node. The `E:` NTFS volume reported
  `Healthy`/`OK` with 1,969,027,309,568 bytes free of 2,000,381,014,016 bytes. These are Development observations,
  not a production-size load approval.

The first AWS publication attempt failed closed while reading the SHA-256 checksum of a KMS-encrypted object. The
bounded qualification policy was promoted and attached, after which checksum retrieval, immutable publication,
cross-Region replication, and both restores passed. The execution policy was split into base and Gate 15 supplemental
documents to keep each customer-managed policy below IAM's 6,144-character limit. No verification was weakened and no
failed-operation object version was deleted.

Gate 14 cannot lawfully execute an exact-version deletion today because every observed qualification object remains
under Governance retention. Creating a short-lived object cannot bypass the bucket's configured default retention.
The dry-run and all negative/partial-execution tests remain valid, but live deletion must wait for an explicitly named
non-evidence version to expire and for a separate approval of that exact tuple. Gates 4-10 evidence and the failed
Gate 11/KMS probe versions are excluded from deletion scope.

The reviewed workload change set `ifm-backup-development-20260823233635` was executed on 2026-08-24 and the stack
reached `UPDATE_COMPLETE`. CloudFormation reports the dashboard, SNS topic/email subscription, and all sixteen alarms
as `CREATE_COMPLETE`; `UploadRole`, `VerificationRole`, and `RetentionPlanRole` were updated in place with no
replacement or deletion. After the supplemental policy added exact `sns:ListTagsForResource` access, the SNS topic
drift cleared and no resource was `MODIFIED` or `DELETED`. AWS drift detection still returns an internal failure and
`UNKNOWN` only for `BackupOperationsDashboard`; direct `GetDashboard` succeeds with no validation messages. The
reversible KMS-denial alarm drill passed with the exact SNS action and approved runbook, then reset to `OK`.

Cost Explorer for 2026-08-01 through 2026-08-25 reported no allocated cost yet for the `Application=IFM` tag. The
account-level service view reported approximately USD 0.0003166 S3 usage, USD 0.000000004 KMS usage, and offsetting
Data Transfer/DynamoDB credits. These Development observations are estimated and do not replace production capacity
or cost acceptance.

The read-only primary-vault inventory contained 276 Development object versions. Its earliest version is the Gate 4
canary tuple `v1/environment/development/gate4/canary/20260822T180312800Z/gate4-canary.json` /
`IsntMAVSrMbB7QGV5.hMiNMv.IwKwt8l`; AWS reports Governance retention through
`2026-09-26T18:03:14.698000Z`. It is immutable qualification evidence and is not a deletion candidate. All other
observed versions were created later under the same 35-day Development retention policy, so no exact version is both
expired and eligible for an approved Gate 14 plan on this date.
