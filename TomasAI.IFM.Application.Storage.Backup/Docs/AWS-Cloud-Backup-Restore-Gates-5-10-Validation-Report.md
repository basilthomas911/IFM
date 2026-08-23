# AWS Cloud Backup and Restore Gates 5-10 Validation Report

**Date:** 2026-08-22

**Account:** `107651266250`

**Environment:** Development

**Regions:** `ca-central-1` primary, `ca-west-1` recovery

**Result:** Live qualification in progress; Gate 5 PITR policy promotion and remaining Gates 6-10 fault/negative drills pending

## Scope and status

This report records the implementation and validation state for Gates 5 through 10. A gate is not marked **Complete**
until all exit evidence in the implementation specification has been exercised in the real Development environment.
Passing unit and adapter tests is implementation evidence, not a substitute for recovery qualification.

| Gate | Implemented result | Current qualification state |
| ---: | --- | --- |
| 5 | DynamoDB journal, seven record families, transactions, idempotent inbox/outbox, leases/fencing, checkpoints, `WorkQueueIndex`, consistent authoritative reads, and multipart checkpoints. | Live journal contract/concurrency and crash/restart flows pass; ambiguous admission response resolution is regression-tested. Restore-to-new-table PITR remains blocked until the latest bounded policy version is made default. |
| 6 | Immutable S3 object store, bounded single/multipart upload/resume, checksums, SSE-KMS context, Object Lock, exact version IDs, ordered publication records, catalog enumeration/rebuild, and stale-upload cleanup. | Live single/multipart resume, duplicate/corruption rejection, exact read-back, catalog rebuild, and recovery replication pass. Remaining role-denial and dropped-response drills are pending. |
| 7 | KMS ECDSA-SHA-256 signing/verification and offline public trust-bundle verification with key identity, algorithm, validity, and fingerprint checks. | Live online/offline verification and tamper rejection pass. Controlled denial and rollover-overlap drills remain pending. |
| 8 | AWS recovery processor and engine selector over the shared durable state machine; independent AWS admission/execution/outbox runtime with source-scoped degraded health. | Rebuilt host is healthy; explicit restart returned healthy with NATS uninterrupted. Live AWS fault/reconciliation and health-isolation drill remains pending. |
| 9 | PostgreSQL artifact publication with WAL continuity plus signed deterministic WAL catalog records, identity/timeline validation, gap/lag detection, recovery-vault support, and bounded non-dropping spool behavior. | Physical full plus six incrementals and live signed AWS full-plus-six/WAL gap/recovery replication pass. Slow-S3, source-failover, and alarm qualification remain pending. |
| 10 | Exact-version dependency restoration from explicit primary/recovery vaults, artifact validation, WAL staging through a UTC PITR target, and PostgreSQL recovery configuration integrated with `pg_combinebackup`. | Exact dependency staging passes from both vaults; native chain boot and a real UTC-target PostgreSQL 17 PITR boot pass. End-to-end AWS-to-fresh-target, negative matrix, and component RPO/RTO remain pending. |

## Automated evidence recorded

- AWS cloud unit tests: 35 passed, including journal contention/idempotency, ambiguous-response resolution, and
  immutable-publication/signature tests.
- Gate 5 live DynamoDB selection: 2 passed, including durable checkpoint/outbox recovery across a reconstructed journal
  instance and fencing-token advancement from 1 to 2.
- Shared SQLite journal integration selection: 9 passed, including crash-after-outbox-write recovery, canonical
  idempotent sequencing, lease fencing, and restart recovery.
- AWS cloud integration tests: 7 passed with live mutation disabled, including the opt-in live harness and complete host
  dependency-injection graph.
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

Four real restore attempts refined and then reconfirmed the target authorization boundary without creating a target table. Target
`ifm-database-backup-journal-development-pitr-20260822T204240Z` was denied for missing `dynamodb:Scan`; after that policy
update, target `ifm-database-backup-journal-development-pitr-20260822T212621Z` advanced to a missing `dynamodb:Query`
denial; after the read-set update, target `ifm-database-backup-journal-development-pitr-20260822T213112Z` advanced to a
missing `dynamodb:UpdateItem` denial. Target
`ifm-database-backup-journal-development-pitr-20260823T025435Z` reconfirmed that same denial because the reviewed
repository version was not the effective IAM default. The repository policy now mirrors the established journal
data-plane action set onto only the approved Development PITR target prefix, permits TTL parity reads, and permits only
the target-specific throttling alarm prefix. Gate 5 PITR must be rerun after this current policy version is made default.

## Remaining qualification sequence

1. Attach the bounded policy and run the live DynamoDB contract, contention, crash/restart, ambiguous-response, and
   restore-to-new-table PITR test. Reapply required tags, alarms, and PITR to the restored table and retain the evidence.
2. Run S3 single/multipart boundary tests, interruption/resume, immutable denial, checksum/corruption failures, exact
   version read-back, replication observation, and catalog deletion/rebuild.
3. Run live KMS online/offline verification, wrong-key/Region/account and disabled-key failures, then a controlled key
   rollover overlap test.
4. Run AWS processor duplicate/reorder/cancellation/restart/fault tests and prove that AWS degradation does not stop the
   host, UI, actors, or local processor.
5. Publish a PostgreSQL full backup plus at least six direct-parent incrementals while archiving WAL. Inject a gap,
   restart, slow-S3, and source-failover conditions and verify eligibility/lag behavior.
6. Restore full, incremental, and selected PITR points from both primary and recovery vaults to fresh isolated targets;
   run native, schema, extension, privilege, row/application-invariant, and read/write checks; record component RPO/RTO.

No Gate 5-10 completion claim is permitted until these results are added to this report with immutable evidence IDs.

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

These results materially reduce the remaining qualification set but do not close any gate whose explicit negative,
fault-injection, alarm, rollover, end-to-end recovery, or PITR-table evidence is still listed above.
