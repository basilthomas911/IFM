# AWS Cloud Backup and Restore Gates 5-10 Validation Report

**Date:** 2026-08-23

**Account:** `107651266250`

**Environment:** Development

**Regions:** `ca-central-1` primary, `ca-west-1` recovery

**Result:** Gates 5-8 and 10 complete; Gate 9 awaits deployment and live exercise of the recovery-region replication-lag alarm

## Scope and status

This report records the implementation and validation state for Gates 5 through 10. A gate is not marked **Complete**
until all exit evidence in the implementation specification has been exercised in the real Development environment.
Passing unit and adapter tests is implementation evidence, not a substitute for recovery qualification.

| Gate | Implemented result | Current qualification state |
| ---: | --- | --- |
| 5 | DynamoDB journal, seven record families, transactions, idempotent inbox/outbox, leases/fencing, checkpoints, `WorkQueueIndex`, consistent authoritative reads, and multipart checkpoints. | **Complete.** Live contract/concurrency and crash/restart flows pass; ambiguous admission response resolution is regression-tested; retained PITR target is active with schema/index parity, tags, PITR, TTL/stream parity, and its throttling alarm. |
| 6 | Immutable S3 object store, bounded single/multipart upload/resume, checksums, SSE-KMS context, Object Lock, exact version IDs, ordered publication records, catalog enumeration/rebuild, and stale-upload cleanup. | **Complete.** Live single/multipart resume, exact read-back, duplicate/corruption rejection, catalog rebuild, recovery replication, recovery-role write/delete denial, and isolated stale-upload cleanup pass. Lost single/multipart completion responses resolve only one exact verified immutable version. |
| 7 | KMS ECDSA-SHA-256 signing/verification and offline public trust-bundle verification with key identity, algorithm, validity, and fingerprint checks. | **Complete.** Live online/offline verification, wrong Region/account/key-use denial, recovery-role signing denial, direct primary-key-use denial, and two-key rollover overlap pass; disabled/untrusted signing fails closed in deterministic tests. |
| 8 | AWS recovery processor and engine selector over the shared durable state machine; independent AWS admission/execution/outbox runtime with source-scoped degraded health. | **Complete.** Exact duplicate, lease split-brain, reordered replay, publication failure, cancellation, and AWS/local isolation drills pass. A scoped live host restart returned healthy while NATS remained uninterrupted. |
| 9 | PostgreSQL artifact publication with WAL continuity plus signed deterministic WAL catalog records, identity/timeline validation, gap/lag detection, recovery-vault support, and bounded non-dropping spool behavior. | Full plus six native and signed AWS chains, WAL gap/fill/replay, recovery failover, persistent spool restart/full-pressure, and measured replication pass. The destination-Region `ReplicationLatency` alarm is AWS-validated but not yet deployed/exercised; this is the sole remaining blocker. |
| 10 | Exact-version dependency restoration from explicit primary/recovery vaults, artifact validation, WAL staging through a UTC PITR target, and PostgreSQL recovery configuration integrated with `pg_combinebackup`. | **Complete.** A signed full-plus-six chain from each vault feeds the native restore capability; real PostgreSQL 17 full, six-incremental, and UTC PITR targets boot and validate. Corrupt, missing-parent/WAL, wrong-timeline/version, KMS/credential-denial, and nonfresh-target cases pass. |

## Automated evidence recorded

- AWS cloud unit tests: 43 passed, including journal contention/idempotency, ambiguous-response resolution,
  immutable-publication/signature tests, signing rollover/disable failures, and bounded WAL spool pressure/restart.
- Gate 5 live DynamoDB selection: 2 passed, including durable checkpoint/outbox recovery across a reconstructed journal
  instance and fencing-token advancement from 1 to 2.
- Shared SQLite journal integration selection: 9 passed, including crash-after-outbox-write recovery, canonical
  idempotent sequencing, lease fencing, and restart recovery.
- AWS cloud integration tests: 27 passed with live mutation disabled, including the opt-in live harness, complete host
  dependency-injection graph, and Gate 8 replay/failure/source-isolation state-machine tests.
- SQLite journal and PostgreSQL native-capability regression selection: 5 passed.
- Shared DatabaseBackup contract selection: 33 passed.
- Full `TomasAI.IFM.sln` build: succeeded with zero warnings and zero errors.
- CloudFormation templates: repository infrastructure checks, IAM policy checks, `cfn-lint`, and live workload
  `ValidateTemplate` pass.
- Publication benchmarks, BenchmarkDotNet `ShortRun`:
  - canonical serialization of a 1,000-artifact publication: 1.110 ms mean, 565,806 bytes allocated;
  - immutable key generation: 919.347 ns mean, 440 bytes allocated.
- Development workload stack after the Gate 5 schema update:
  - state: `UPDATE_COMPLETE` at `2026-08-22T18:51:40.446Z`;
  - drift detection: `05996eb0-9e5b-11f1-84e3-066b74d5127b`;
  - result: `DETECTION_COMPLETE`, `IN_SYNC`, zero drifted resources.

## Live-qualification authorization boundary

The bounded temporary policy is:

`deploy/aws/database-backup/environments/development/gate5-10-live-qualification-policy.json`

It is restricted to the Development journal table/index and PITR target prefix, Development primary object prefix,
recovery-vault reads, the recovery-read role, and the Development encryption/signing keys. Attach it only to the approved
Development qualification identity and remove it after evidence capture. Normal operation must use the workload roles,
not this user policy.

Five real restore attempts refined and then reconfirmed the target authorization boundary without creating a target table. Target
`ifm-database-backup-journal-development-pitr-20260822T204240Z` was denied for missing `dynamodb:Scan`; after that policy
update, target `ifm-database-backup-journal-development-pitr-20260822T212621Z` advanced to a missing `dynamodb:Query`
denial; after the read-set update, target `ifm-database-backup-journal-development-pitr-20260822T213112Z` advanced to a
missing `dynamodb:UpdateItem` denial. Target
`ifm-database-backup-journal-development-pitr-20260823T025435Z` reconfirmed that same denial because the reviewed
repository version was not the effective IAM default. The repository policy now mirrors the established journal
data-plane action set onto only the approved Development PITR target prefix, permits TTL parity reads, and permits only
the target-specific throttling alarm prefix. After the complete documented dependent-action set was made effective, the
sixth restore passed and retained the target table and alarm described below.

## Remaining qualification sequence

1. Promote the reviewed `IFM-Gate4-CloudFormationExecution` managed-policy document so CloudFormation may manage only
   `ifm-database-backup-replication-lag-development` in `ca-west-1`.
2. Promote the reviewed `IFM-Gates5-10-LiveQualification` document so the qualification identity may describe and
   temporarily set only the two Development replication alarms.
3. Deploy the reviewed recovery-vault update, exercise alarm/OK transitions, record the alarm ARN and stack drift, and
   restore the alarm to metric evaluation. Gate 9 may then be marked complete.

## Qualification activity log

### 2026-08-22 initial live pass

- AWS identity reconfirmed as `arn:aws:iam::107651266250:user/basil.thomas@live.ca` in account `107651266250`.
- Gate 5 stopped safely before mutation because `dynamodb:DescribeTable` remains denied. The temporary qualification
  policy must be attached before the AWS sequence can continue.
- Gate 8 exposed that the Development backup-host image dated 2026-08-13 predated the resilience implementation. A
  current rebuild then exposed a Docker context omission for the AWS adapter project; the ignore rules were corrected.
- The rebuilt Development backup host starts successfully, remains at restart count zero, reports Docker health
  `healthy`, and returns HTTP 200/`Healthy` from `/health/ready`.
- Gates 9-10 PostgreSQL 17 disposable-container native tests passed: physical full backup verification and fresh-target
  boot, plus physical incremental capture, `pg_combinebackup`, verification, fresh-target boot, and latest-row
  validation. Two tests passed in 2.22 minutes with zero build warnings/errors.
- This is partial evidence only. Six-incremental/WAL fault qualification and AWS primary/recovery PITR restores remain
  required before Gates 9-10 can close.

### 2026-08-22 live qualification continuation

- Gate 5 live DynamoDB journal contract passed against
  `ifm-database-backup-journal-development`: duplicate admission, competing lease, checkpoint, core acknowledgement,
  outbox publication, and zero recoverable residue. The table and `WorkQueueIndex` were `ACTIVE`.
- Gate 6 single-part retained evidence passed exact-version download and digest verification:
  operation `0df8a5934d3d48928fdd519aec284ae3`, object
  `v1/environment/development/evidence/operation/0df8a5934d3d48928fdd519aec284ae3/gate6-live-object.json`, version
  `m2Ms3fso2Aw2EWGZCodBLgRSWPK.up91`.
- Gate 6 multipart resume exposed that completed-part checksums were not copied into `CompleteMultipartUpload`. The S3
  adapter now requires and propagates each SHA-256 checksum. The live resume, duplicate rejection, and corrupt-digest
  rejection then passed: operation `ff005b28ccc24f828b150528311f024f`, version
  `_phBIwBf8kZ8CtAEPfqnTrYgfBppq5gB`, 5,242,881 verified bytes.
- Gate 6 signed publication/catalog resolve/enumerate/rebuild passed for restore point
  `d252402da4bf41f1a3ac95bbdc831650`, manifest
  `manifest-d252402da4bf41f1a3ac95bbdc831650`, 4,096 verified bytes.
- Gate 7 live KMS online/offline verification and tamper rejection passed for operation
  `69e267767a624ead8ddbe138bd7a16c2`; signing-key public fingerprint
  `705F75DDB510986F565B97B9329996EF6B9786E27CBE21E0FE796301908107CB`.
- The signing and recovery KMS key policies originally did not delegate the intended runtime roles. The reviewed
  in-place CloudFormation updates added bounded role principals without replacing either key. Signed recovery-vault
  resolution then passed for restore point `9b55b9ea325948bdb65f98a429d0ec46` in 25.841 seconds.
- Safe live denial checks passed: the recovery read role was denied signing-key use, and it was denied deletion of exact
  retained recovery catalog version `iQYX.VnZPZ3pkzVevzyofgYwebuNYgcG` for restore point
  `eac7c02975b941d0b3ac556ab99fa0ed`; that fresh replication was verified in 33.661 seconds.
- Workload drift detection `45fb7500-9e6c-11f1-b40e-02cdaaa60ec7` and recovery-vault drift detection
  `46c1ab30-9e6c-11f1-b8c6-0ad72d111197` both completed `IN_SYNC` with zero drifted resources.
- Gate 8 backup-host restart qualification passed: before restart it was healthy with restart count zero; after the
  explicit restart it returned `running/healthy`, `/health/ready` returned HTTP 200/`Healthy`, and NATS remained healthy
  and uninterrupted.
- Gate 9 native PostgreSQL 17 full plus six direct-parent incrementals verified, combined, booted a fresh target, and
  returned the expected `native-depth-6` row in 2 minutes 53 seconds.
- Gate 9 live AWS full-plus-six signed chain staged all seven restore points from both vaults. Protection set
  `postgresql-gate9-2473293c38f146e3922be653c09d6f81`, base
  `ceaa9d01ad77471ab402558f5d64b427`, final `f176de744be14316977f9699edc85282`; recovery replication and staging took
  19.836 seconds.
- Gate 9 signed WAL qualification detected the intentional missing segment, became contiguous after the gap was filled,
  returned the same immutable version on replay, and verified all three records through recovery in 22.379 seconds.
  Protection set `postgresql-wal-5f1c72c026764ce4adcaa2a72ffd44f9`, timeline `00000001`.
- Gate 10 real PostgreSQL 17 PITR initially exposed an invalid `recovery_target_time` `T...Z` configuration value. The
  implementation now writes PostgreSQL's accepted timestamp-with-offset form, with a regression assertion. A physical
  base backup plus completed WAL then booted at the selected UTC timestamp and returned the pre-target row, excluding the
  later committed row. The passing recovery run took 39 seconds.

### 2026-08-23 Gate 5 continuation

- The AWS cloud unit suite passed 35 tests, including a new deterministic ambiguous-response test that simulates a
  lost successful DynamoDB transaction response and resolves the durable inbox with a consistent read.
- The shared SQLite journal integration selection passed all 9 tests, including simulated process termination after
  an outbox write, restart recovery, unique canonical service-event sequencing, and stale-fence rejection.
- Two live Development DynamoDB journal tests passed. Contract/concurrency operation
  `6c8420722c48474897a0dc73a799faf5` passed duplicate admission, competing lease, checkpoint, core acknowledgement,
  outbox publication, and zero recoverable residue. Restart operation `748646c1491b40428c3191380b27f1d4`
  was recovered at the durable `Started` checkpoint by a reconstructed journal instance; its pending outbox entry was
  recovered and published, its fencing token advanced from 1 to 2, and terminal completion left no recoverable residue.
- The retained PITR runbook was extended to prove source/target TTL and stream parity and to create and validate the
  restored table's bounded `ThrottledRequests` alarm. The reviewed qualification policy was extended with only the
  corresponding Development target-prefix read and alarm permissions.
- PITR target `ifm-database-backup-journal-development-pitr-20260823T025435Z` stopped without creating a table because
  the attached/default IAM policy version still denied target-table `dynamodb:UpdateItem`. The current identity also
  lacks `iam:GetPolicy`, `iam:ListPolicyVersions`, and `iam:CreatePolicyVersion`, so an IAM administrator must promote
  the reviewed repository policy before the final retained PITR rerun.
- After that version was promoted, the preview passed, including the new TTL read. PITR target
  `ifm-database-backup-journal-development-pitr-20260823T030632Z` then stopped without creating a table because
  target-table `dynamodb:BatchWriteItem` was absent. AWS's service-authorization reference identifies it as the one
  remaining dependent restore action beyond the already bounded `DeleteItem`, `GetItem`, `PutItem`, `Query`, `Scan`,
  and `UpdateItem` set. The repository policy now includes the complete documented target-table dependency set.
- After the complete policy was promoted, retained PITR target
  `ifm-database-backup-journal-development-pitr-20260823T031459Z` passed. The table reached `ACTIVE`; retained the
  source PK/SK schema and `WorkQueueIndex`; was tagged `Application=IFM`, `Component=DatabaseBackup`,
  `Environment=development`, and `Qualification=Gate5-PITR`; and had PITR re-enabled. Source/target parity was proven
  with TTL `DISABLED` and streams disabled.
- Throttling alarm `ifm-database-backup-journal-throttling-development-pitr-20260823T031459Z` was created and validated
  against the restored table's `AWS/DynamoDB` `ThrottledRequests` metric. Its ARN is
  `arn:aws:cloudwatch:ca-central-1:107651266250:alarm:ifm-database-backup-journal-throttling-development-pitr-20260823T031459Z`.
- Gate 5 is complete. The restored table and alarm are retained as Development qualification evidence; no cleanup or
  production cutover was performed.

### 2026-08-23 Gates 6-10 fault and recovery continuation

- Gate 6 live qualification passed all five selected tests. Fresh evidence included single-part operation
  `809ab93f58b34b27b64b276776624c5d`/version `vtsBbA2jsNJCCNPWcHNbLBtti2Vm5Ob8`, multipart operation
  `13178b60b8bd419688eb81134dab705c`/version `eH0x5n8tl9fZ5ZCj6Y0z_Epd8RtAR1hr`, catalog operation
  `c6c01b50e93c49aaa74d11b5146f13d6`, and recovery replication operation
  `db6f7367a29a4d66bede9a8d8808a5f1`. The recovery role was denied both deletion and overwrite. Cleanup aborted exactly
  one isolated stale multipart upload and first proved that no unrelated in-flight upload existed.
- Lost successful S3 single/multipart completion responses now resolve the one exact durable version and then perform
  full length, checksum, encryption-key, retention, and content verification. Wrong-key/short-retention and truncated
  reads fail closed.
- Gate 7 live qualification passed four selected tests. Online/offline evidence operation
  `fe1b9bc5bd524d1bb127e6dfabdb9dc1` used signing key
  `arn:aws:kms:ca-central-1:107651266250:key/2edd60e5-be19-483d-b4df-88df45aa2fb2`, fingerprint
  `705F75DDB510986F565B97B9329996EF6B9786E27CBE21E0FE796301908107CB`. Wrong Region/account/key usage and recovery-role
  signing were denied. A revision-2 two-key overlap bundle kept pre-rollover evidence valid; disabled/untrusted keys fail
  closed in unit coverage.
- Gate 8 state-machine qualification passed five tests covering exact duplicate admission, unavailable lease,
  last-sequence replay ordering, publication failure without terminal success, cancellation, and same-cycle AWS/local
  source isolation. The live backup host restarted from `2026-08-23T03:33:51.85873218Z` and returned healthy; NATS kept
  its original `2026-08-23T02:51:16.0660615Z` start time and zero restarts.
- Gate 9 spool pressure/restart tests prove required WAL remains on disk when the bound is reached and that same-length
  altered replay is rejected by SHA-256. A fresh live full-plus-six chain staged from both vaults: protection set
  `postgresql-gate9-ad372e7f3f61462595e29c3fceaa8a15`, base
  `2b1bc8234fe84f3c89daed8c98203545`, final `4fd5e67b3aac49dcbe9c49437f66e024`, recovery replication 23.662 seconds.
- A recovery-region `ReplicationLatency` maximum alarm at the 900-second policy boundary was added to the recovery
  CloudFormation stack. The repository policy and all four templates pass local checks and live AWS `ValidateTemplate`.
  Deployment and alarm-state exercise await the two managed-policy promotions listed above.
- Gate 10 passed six deterministic negative cases, two AWS object/WAL negative cases, and five live negative cases:
  corrupt/truncated evidence, missing signed parent, missing WAL, wrong timeline, incompatible PostgreSQL tools,
  direct KMS denial, invalid/expired session credentials, and nonfresh target rejection. No protected source was changed.
- The connected qualification published a full plus six direct-parent chain, downloaded every exact signed dependency
  from each of `aws-primary` and `aws-recovery` into fresh native roots, and invoked verification plus one actual combine
  operation per vault; it passed in 49 seconds. Separately, five real Docker-native full, incremental, full-plus-six,
  PITR, and Scylla regression restores passed in 5.67 minutes. The PostgreSQL full-plus-six case took 2 minutes 32
  seconds; selected-UTC PITR took 34 seconds and returned only the pre-target row.

These results close Gates 6, 7, 8, and 10. Gate 9 remains open only for the reviewed replication-lag alarm deployment
and live alarm-state qualification; no production cutover or retained-version deletion was performed.
