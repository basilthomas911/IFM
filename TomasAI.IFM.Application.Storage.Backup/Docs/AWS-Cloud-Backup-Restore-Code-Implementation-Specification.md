# AWS Cloud Database Backup and Restore Code Implementation Specification

**Status:** Gates 0-5 complete; Gates 6-10 implemented, live qualification pending

**Version:** 0.7

**Date:** 2026-08-22

**Implementation target:** `BackupSource.AwsCloud`

**Architecture authority:**

- `Database-Backup-Architecture-Overview.md`, version 0.9
- `AWS-Cloud-Backup-Restore-Architecture.md`, version 0.6
- `Local-Backup-Restore-Architecture.md`, version 0.3
- `Local-Workstation-Backup-Restore-Code-Implementation-Specification.md`, version 1.6

## 1. Purpose and required outcome

This specification is the ordered delivery plan for a complete AWS cloud backup, restore, retention, and disaster-
recovery implementation for IFM PostgreSQL and ScyllaDB. It converts the approved AWS architecture into code,
infrastructure, security, migration, test, operational, and production-readiness gates.

Completion means that IFM can create database-native recovery artifacts, publish them immutably to a primary AWS
vault, replicate them to an independently controlled recovery vault, discover and verify them without the workload
databases, and restore a dependency-complete recovery point to a fresh target. Completion also requires demonstrated
PostgreSQL point-in-time recovery, demonstrated Scylla protection-set recovery, documented break-glass procedures,
measured RPO/RTO, and a recovery-only production drill.

This document does not itself authorize AWS resource creation, native backup execution, retained-object deletion,
production restore, or production cutover. Each environment-changing gate requires its named approval and evidence.

## 2. Binding implementation decisions

1. AWS is a new processor for the existing source-neutral contracts. It uses `BackupSource.AwsCloud`; it does not add
   AWS-specific actor commands, queries, domain events, or projection tables.
2. Existing MessagePack numeric keys are append-only. AWS uses `RequestDatabaseBackupCommand.RequestedBackupMode` at
   key 31 and `DatabaseBackupEventContract.BackupLineage` at key 31.
3. `DatabaseBackupManifest` schema version 2 is the engine-manifest baseline. Schema version 1 remains readable as a
   legacy full backup.
4. AWS publication evidence is a separately signed document. AWS account, Region, bucket, object version, KMS,
   retention, and replication details do not leak into shared actor messages.
5. The existing `SystemAdminDbContext` projection schema is reused. `backup_lineage_json` remains the lineage column.
6. The AWS execution journal implements the same seven logical record families as the SQLite journal: operation,
   inbox, checkpoint, artifact replica, outbox, run statistics, and reconciliation.
7. Native artifacts never pass through NATS, actor state, DynamoDB, or `SystemAdminDbContext`.
8. PostgreSQL 17-or-later native incremental backups declare their direct parent. Restore materializes the complete
   chain with `pg_combinebackup` before recovery and uses the retained WAL stream for PITR.
9. Scylla Manager snapshots are logically complete restore points. Native deduplication is recorded as lineage, but
   IFM does not invent an SSTable dependency graph.
10. Every restore targets a fresh, isolated target. Production cutover remains a separately approved command.
11. Primary and recovery vaults use unique immutable S3 object keys, Versioning, Object Lock, SSE-KMS with customer-
    managed keys, and one-way cross-account, cross-Region replication.
12. The engine manifest and AWS publication record are signed with an asymmetric KMS signing key. Vault encryption
    uses separate regional symmetric KMS keys.
13. The catalog is reconstructable solely from immutable manifests and publication records in S3.
14. A backup is not recovery-eligible until its artifact, engine manifest, signature, publication record, checksums,
    and required dependency chain are verified. A recovery-vault candidate additionally requires replica evidence.
15. Production uses temporary role credentials. Long-lived access keys are forbidden in application configuration,
    container images, logs, evidence, test results, and committed files.
16. Destructive retention execution requires a revision-matched approved plan, a separate deletion role, an allowlisted
    object-version set, and an unexpired-retention/legal-hold check. There is no bucket-wide delete path.
17. A production restore or deletion can never be triggered merely by enabling the AWS processor in configuration.

## 3. Credential and environment-variable rule

The workstation currently exposes environment-variable names beginning with `aws_`. Their values must never be read
for documentation, printed, logged, copied into configuration, written into test output, or committed. On Windows,
environment-variable names are case-insensitive; Linux containers are case-sensitive. The .NET process therefore uses
the standard SDK names in executable environments:

```text
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
AWS_SESSION_TOKEN       # required when the supplied credentials are temporary
AWS_REGION              # or an explicit non-secret Region option
```

The existing lowercase workstation variables are allowed only as a local development/integration bootstrap. A local
launch wrapper may map their values in process memory to the standard uppercase names, but it must not echo them or
persist them. Production must use the AWS SDK default credential chain with workload identity and temporary role
sessions. No custom access-key options class is permitted.

Every live-AWS test begins with a read-only STS `GetCallerIdentity` preflight and rejects an account or partition not
on that test profile's allowlist. The preflight records only account ID, principal ARN, partition, Region, test-run ID,
and time. It never records access-key IDs or session tokens. The presence of credentials is not proof of authorization
to create resources or run a restore.

## 4. Current baseline and implementation gaps

| Capability | Current repository baseline | AWS implementation gap |
| --- | --- | --- |
| Shared actor contracts | Implemented and source-neutral | Compatibility tests for `AwsCloud` |
| Manifest and lineage | Schema v2 implemented locally | S3 representation and AWS publication record |
| SystemAdmin projections | Implemented with `BackupSource` and `backup_lineage_json` | AWS end-to-end projection qualification |
| Execution journal | SQLite implementation with seven record families | DynamoDB implementation and PITR |
| Processor registry | Supports multiple `IDatabaseRecoveryProcessor` instances | Register AWS without local-option coupling |
| Host composition | Currently binds local-workstation options directly | Source-neutral host options and per-source registration |
| Chain planning | Implemented in the local framework project | Extract/reuse destination-neutral policy |
| PostgreSQL capture/restore | Local native capability implemented | AWS staging, WAL publication, restore-source adapter |
| Scylla capture/restore | Local Manager capability implemented | AWS staging/publication and recovery-vault restore |
| Artifact repository | Filesystem vault/media implementation | S3 multipart, checksums, versions, Object Lock |
| Signing | Local signature implementation | KMS asymmetric signing and offline trust bundle |
| Catalog | Local immutable catalog | S3 append-only catalog and rebuild tool |
| Retention | Local dependency-aware retention | Object-version-aware AWS plan and execution |
| AWS SDK packages | None | Add only the required AWS SDK for .NET v4 packages |
| AWS infrastructure | Architecture only | Reviewed infrastructure as code and deployed environments |
| Restore drills | Local validation evidence exists | Primary-vault, recovery-vault, and recovery-only AWS drills |

## 5. Target solution structure

### 5.1 Production and test projects

```text
TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud/
  Configuration/
  Identity/
  Journal/DynamoDb/
  Storage/S3/
  Catalog/
  Manifest/
  Security/Kms/
  PostgreSql/
  Scylla/
  Replication/
  Retention/
  Evidence/
  Processing/
  Startup/

TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests/
TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests/

deploy/aws/database-backup/
  workload/
  primary-vault/
  recovery-vault/
  policy/
  environments/dev/
  environments/staging/
  environments/production/
```

The repository's selected infrastructure-as-code technology must be recorded in an architecture decision record in
Gate 0. The folder names above are stable regardless of whether the approved tool is CloudFormation, CDK, or Terraform.

### 5.2 AWS SDK dependencies

The AWS framework project adds only the current compatible stable AWS SDK for .NET v4 packages needed for:

- Amazon S3;
- Amazon DynamoDB;
- AWS Key Management Service;
- AWS Security Token Service; and
- CloudWatch only if direct custom-metric publication remains necessary after the observability design review.

Exact versions are centrally pinned or consistently pinned in project files, recorded in the dependency inventory,
and validated by restore integration tests. Package upgrades are not combined with a production rollout.

The NuGet.org package snapshot verified on 2026-08-21 is:

| Direct package | Candidate stable version | First required gate | Purpose |
| --- | ---: | ---: | --- |
| `AWSSDK.S3` | `4.0.102.3` | Gate 1 | S3 artifact, evidence, catalog, Object Lock, checksum, and replication APIs |
| `AWSSDK.DynamoDBv2` | `4.0.103.4` | Gate 1 | Conditional and transactional execution-journal persistence |
| `AWSSDK.KeyManagementService` | `4.0.100.10` | Gate 1 | Manifest/publication signing, verification, and KMS metadata validation |
| `AWSSDK.SecurityToken` | `4.0.100.10` | Gate 1 | STS caller-identity preflight and role-session support |

`AWSSDK.Core` is expected as a compatible transitive dependency and is not added directly unless restore/build
analysis proves a direct reference is required. `AWSSDK.CloudWatch` is deferred until Gate 15 establishes that direct
metric publication is needed; OpenTelemetry or existing metrics infrastructure remains preferred. Before installing,
Gate 1 must re-query NuGet.org, reject prerelease/deprecated/vulnerable versions, inspect the resolved dependency graph,
and pin the four packages as one compatible AWS SDK v4 set. Package installation is a code change and does not use or
validate AWS credentials.

### 5.3 Dependency direction

```text
Domain.SystemAdmin.Shared
          ^
          |
Application.DatabaseBackup
          ^
          |
Framework.Storage.DatabaseBackup.AwsCloud
          ^
          |
Api.DatabaseBackup.Host (composition only)
```

AWS SDK types, ARNs, bucket names, object keys, and KMS identifiers must remain inside the AWS framework project and
its safe configuration types. Application and domain projects remain AWS-independent.

## 6. Required AWS adapters

| Component | Required responsibility |
| --- | --- |
| `AwsCloudDatabaseBackupOptions` | Safe non-secret account, Region, bucket, key, role, table, retention, timeout, and feature settings |
| `AwsIdentityPreflight` | Resolve SDK credentials, call STS, enforce partition/account/Region allowlists |
| `DynamoDbDatabaseBackupExecutionJournal` | Implement all journal operations with conditional/idempotent writes and transactional transitions |
| `S3DatabaseBackupPublicationCapability` | Stage, checksum, multipart-upload, verify, lock, and publish immutable objects |
| `S3DatabaseRestoreSourceCapability` | Discover explicit replicas and download a verified dependency-complete set |
| `S3DatabaseBackupCatalog` | Append immutable catalog records and rebuild the catalog from signed evidence |
| `KmsManifestSignatureService` | Sign and verify SHA-256 digests; record key ARN/version and algorithm |
| `AwsPublicationRecordWriter` | Create canonical signed AWS replica/publication evidence |
| `AwsReplicaVerificationService` | Verify version IDs, encryption, retention, checksum, signature, dependencies, and replication state |
| `AwsPostgreSqlWalArchive` | Publish, index, verify, and restore continuous WAL segments without actor-message payloads |
| `AwsDatabaseRecoveryEngineSelector` | Select existing typed PostgreSQL or Scylla capabilities without exposing native commands |
| `AwsCloudDatabaseRecoveryProcessor` | Orchestrate the existing application ports for `BackupSource.AwsCloud` only |
| `AwsRetentionPlanner` | Produce a dependency-aware, object-version-specific, revisioned dry-run plan |
| `AwsRetentionExecutor` | Execute only an approved plan through the constrained deletion role |
| `AwsRecoveryEvidenceStore` | Persist bounded signed run evidence outside workload databases |
| `AwsCatalogRebuildCommand` | Reconstruct catalog state without Core, NATS, DynamoDB, PostgreSQL, or ScyllaDB |

The existing local chain planner and manifest validation rules should be moved to
`TomasAI.IFM.Application.DatabaseBackup` when they are destination-neutral. AWS must not copy policy code into a second
implementation that can drift.

## 7. Safe configuration model

Only non-secret identifiers and policy values may appear in `appsettings*.json`:

```json
{
  "DatabaseBackup": {
    "EnabledSources": [ "LocalWorkstation", "AwsCloud" ],
    "AwsCloud": {
      "Enabled": false,
      "Environment": "Development",
      "WorkloadAccountId": "000000000000",
      "PrimaryVaultAccountId": "000000000000",
      "RecoveryVaultAccountId": "000000000000",
      "PrimaryRegion": "ca-central-1",
      "RecoveryRegion": "us-east-2",
      "PrimaryBucketName": "replace-through-environment-configuration",
      "RecoveryBucketName": "replace-through-environment-configuration",
      "JournalTableName": "ifm-database-backup-journal-dev",
      "UploadRoleArn": "arn:aws:iam::000000000000:role/replace-me",
      "RecoveryReadRoleArn": "arn:aws:iam::000000000000:role/replace-me",
      "PrimaryEncryptionKeyArn": "arn:aws:kms:region:account:key/replace-me",
      "RecoveryEncryptionKeyArn": "arn:aws:kms:region:account:key/replace-me",
      "SigningKeyArn": "arn:aws:kms:region:account:key/replace-me",
      "ObjectLockMode": "Governance",
      "DefaultRetentionDays": 30,
      "MaximumIncrementalChainDepth": 6,
      "MaximumBaseAgeDays": 7,
      "LiveAwsTestsEnabled": false,
      "DestructiveTestsEnabled": false
    }
  }
}
```

The example values are placeholders, not production defaults. Configuration validation must fail startup for a
partially enabled AWS source, identical production vault accounts, identical production Regions, malformed ARNs,
unapproved accounts, nonpositive retention, or a recovery bucket that equals the primary bucket. Liveness remains
healthy when AWS is disabled; readiness reports the AWS processor independently when it is enabled but unavailable.

## 8. S3 object and evidence schema

All keys are generated from validated identifiers; no caller supplies a raw object key. A representative immutable
layout is:

```text
v1/environment/{environment}/protection-set/{protectionSetId}/
  engine/{postgresql|scylladb}/restore-point/{restorePointId}/
    artifacts/{artifactId}/{contentFileName}
    manifests/engine-manifest-v2.json
    manifests/engine-manifest-v2.signature.json
    publications/{replicaId}/publication-v1.json
    publications/{replicaId}/publication-v1.signature.json
    evidence/{operationId}/verification-v1.json

v1/environment/{environment}/protection-set/{protectionSetId}/postgresql/
  timeline/{timelineId}/wal/{segmentName}
  timeline/{timelineId}/wal-index/{utcPartition}/index-v1.json

v1/environment/{environment}/catalog/
  restore-point/{restorePointId}/{replicaId}/catalog-entry-v1.json
```

Every durable item records the S3 version ID. A mutable `latest` object is never recovery authority. S3 ETags are not
used as the IFM content checksum. IFM preserves its manifest SHA-256 and also requests and verifies an S3-supported
upload checksum. Multipart upload state is journaled; abandoned uploads are reconciled and aborted only after an
allowlisted age threshold.

The canonical AWS publication record contains at least:

- schema version, operation ID, restore-point ID, artifact ID, replica ID, engine, and source;
- bucket ARN, Region, account ID, object key, object version ID, exact length, and media/checksum algorithms;
- IFM content digest and returned S3 checksum;
- symmetric KMS key ARN and encryption context;
- Object Lock mode, retain-until time, and legal-hold state;
- engine-manifest digest, signature key ARN, signing algorithm, and signature;
- dependency restore-point IDs and PostgreSQL WAL/timeline bounds when applicable;
- publication, verification, and replication observation times; and
- producing host, build identity, contract version, and correlation identifiers.

Canonical serialization is deterministic and culture-independent. Verification rejects duplicate properties, unknown
required enum values, non-UTC times, invalid identifier shapes, length mismatches, digest mismatches, untrusted keys,
shortened retention, or evidence referring to a different object version.

## 9. DynamoDB journal design

The production AWS profile uses one encrypted DynamoDB table in the workload trust boundary. The logical primary key
is source-prefixed and environment-scoped:

```text
PK = ENV#{environment}#OP#{operationId}
SK = OP | INBOX#{messageId} | CHECKPOINT#{sequence} | REPLICA#{replicaId}
     | OUTBOX#{sequence} | STATS#{sequence} | RECON#{sequence}
```

Secondary access patterns are introduced only for measured worker needs, such as due outbox records, unfinished
operations, reconciliation leases, and stale multipart uploads. Attribute names are explicit and versioned. Raw
exception text, credentials, database connection strings, filesystem paths, and native command output are excluded.

Required correctness rules:

- inbox acceptance and initial operation creation are idempotent;
- operation phase changes use expected-version conditions;
- checkpoint and outbox writes that form one state transition use `TransactWriteItems`;
- lease acquisition has an owner, fencing token, and expiration and cannot be extended by a stale owner;
- duplicate delivery returns the original durable outcome rather than starting another native operation;
- retries use bounded exponential backoff with jitter and honor cancellation;
- ambiguous AWS timeouts are resolved by a consistent read before retrying a state transition;
- item size is bounded and large evidence is stored in S3 by immutable reference;
- time-to-live is not used to delete authoritative recovery evidence; and
- PITR is enabled and a restore-to-new-table runbook is tested.

## 10. End-to-end workflows

### 10.1 PostgreSQL full or incremental backup

1. The Command Actor commits AWS execution intent with requested mode and lineage.
2. The host inbox records it idempotently and resolves `AwsCloudDatabaseRecoveryProcessor`.
3. Identity, configuration, native-version, staging-capacity, vault, KMS, and journal preflights pass.
4. The shared chain planner resolves Full or Incremental. Explicit ineligible Incremental fails; Automatic may fall
   back to Full and records why.
5. The typed PostgreSQL capability creates a base or native incremental backup in isolated staging.
6. The service verifies native metadata, dependency identity, sizes, and content digests.
7. Artifacts upload under unique keys using SSE-KMS, upload checksums, expected retention, and journaled multipart state.
8. The signed engine manifest is uploaded and read back by version ID.
9. The signed AWS publication record is uploaded and verified.
10. The append-only catalog entry is published last.
11. The primary replica becomes eligible only after independent read-back verification.
12. Replication is observed asynchronously; recovery eligibility is published only after the exact destination version,
    KMS key, retention, checksum, and signature are verified.
13. Bounded service events are delivered through the existing transactional outbox.

### 10.2 Continuous PostgreSQL WAL

WAL archival is a continuously monitored companion capability, not a stream of actor messages. Each segment has a
validated timeline and name, content digest, S3 checksum, encryption/retention evidence, and idempotent object identity.
The service detects gaps, duplicates, archive lag, and timeline changes. A base or incremental restore point is PITR-
eligible only when the required WAL interval is contiguous through its declared recoverable time. Restore tests must
cross at least one WAL segment and one timeline-transition scenario.

### 10.3 Scylla backup

1. The same AWS execution-intent and journal path is used.
2. Scylla Manager coordinates a protection-set-wide logically complete snapshot.
3. IFM records Manager task/run identity, participating nodes, schema evidence, token-ring/topology evidence, native
   snapshot identity, and deduplication lineage.
4. Service-controlled staging publishes the artifact set, signed manifest, publication record, and catalog entry.
5. Missing nodes, incomplete schema, unresolved Manager status, or partial object publication prevent eligibility.
6. Recovery-vault eligibility follows exact-version replication verification.

### 10.4 Restore and drill

1. An authorized restore command names the restore point, restore class, explicit replica, fresh target, and optional
   PostgreSQL recovery target time.
2. The service validates authorization, journal state, signatures, trust bundle, object versions, retention evidence,
   checksums, engine compatibility, capacity, and dependency completeness before download.
3. Archive objects are restored and availability is confirmed before the RTO clock's documented restore phase begins.
4. PostgreSQL incrementals are combined into a synthetic full restore input; required WAL is staged and PITR recovery
   runs to the requested target.
5. Scylla restores every required node/protection-set artifact and schema using the approved native sequence.
6. Native validation is followed by application-level validation against the isolated endpoint.
7. The operation stops at `ReadyForCutover`. No network, DNS, secret, or connection-string cutover occurs implicitly.
8. Evidence records exact source versions, digests, commands by allowlisted operation name, timings, validation result,
   and target disposal decision without recording credentials or sensitive native output.

### 10.5 Break-glass recovery

The recovery bundle must work when Core, NATS, the AWS journal table, primary vault account, PostgreSQL, and Scylla are
unavailable. It contains a pinned recovery executable or reproducible build, public signing-key trust bundle, catalog-
rebuild command, evidence schemas, restore runbooks, approved role-assumption instructions, and checksum/signature
verification. It never contains live credentials or private signing material.

## 11. Delivery gates

No later gate may be declared complete while an earlier gate has unresolved severity-1 or severity-2 findings. Every
gate produces a dated validation report under this documentation directory with commands, test results, resource IDs,
safe configuration, deviations, reviewer, and rollback result. Secrets and raw native output are redacted.

### 11.1 Implementation status dashboard

This table is the current implementation record. Update it in the same change that completes material gate work. A
gate may move to **Complete** only when every listed exit-evidence item has a dated validation report. Existing local
capabilities are baseline prerequisites and do not by themselves complete an AWS gate.

| Gate | Status | Results/evidence recorded as of 2026-08-23 | Remaining work or completion blocker |
| ---: | --- | --- | --- |
| 0 | **Complete** | Baseline frozen; CloudFormation/Canada Region/account/recovery/retention/crypto/staging decisions accepted; threat, cost, and deletion controls recorded; AWS CLI/PowerShell installed; read-only STS preflight passed and rejected wrong account/Region/production; 77 tests passed, 8 intentionally skipped, 0 failed; credential-pattern scan passed. See the Gate 0 baseline, decision record, control model, and validation report. | None. Staging/production remain intentionally deny-all and no AWS mutation is authorized. Existing local host finding `G0-F1` is assigned to Gate 1 before any AWS processor enablement. |
| 1 | **Complete** | AWS production/unit/integration projects and pinned SDK v4 dependencies added; source-neutral host controls, routing, health, independent source options, singleton composition, and bounded failed-operation deferral implemented. `G0-F1` is closed by a restart/noise regression test. Full solution builds with 0 warnings/errors and local regression tests pass. See Gate 1 validation report. | None. AWS admission remains disabled until Gate 8. |
| 2 | **Complete** | Canonical JSON, v1/v2 manifest validation, and chain planning moved to shared application policy; local adapter delegates to it. A golden fingerprint locks all 120 DatabaseBackup actor-contract MessagePack shapes; canonical, duplicate/unknown JSON, lineage, cycle, time, and schema tests pass. See Gate 2 validation report. | None. Persisted and wire schemas are unchanged. |
| 3 | **Complete** | Safe credential-free options, strict environment/account/Region/ARN/bucket validation, default SDK credential chain, temporary-session enforcement, singleton clients, bounded timeouts/retries, STS preflight, safe identity observation, failure classification, redaction tests, and source-specific degraded health implemented. Explicit live .NET STS test passed. See Gate 3 validation report. | None for Gate 3. Staging/production remain deny-all. |
| 4 | **Complete** | Four approved Development stacks are deployed across `ca-central-1`/`ca-west-1` and `IN_SYNC`; safe outputs are captured; nine live negative IAM checks passed; a retained canary proved versioning, SHA-256, independent regional KMS encryption, Governance Object Lock, replication, and immutable CloudTrail evidence. See the Gate 4 validation, approval, outputs, and machine-readable qualification evidence. | None. Canary retention expires no earlier than 2026-09-26; do not bypass it. Staging and Production remain deny-all. |
| 5 | **Complete** | Live journal duplicate admission, competing lease, checkpoint, acknowledgement, outbox, and crash/restart recovery pass; fencing advanced from 1 to 2 across the reconstructed journal instance. Ambiguous admission response resolution is regression-tested. Retained PITR target `ifm-database-backup-journal-development-pitr-20260823T031459Z` is active with schema/`WorkQueueIndex` parity, required tags, PITR enabled, TTL/stream parity, and a validated target throttling alarm. | None. Preserve the retained Development table/alarm as qualification evidence and remove the temporary user policy after the remaining approved live qualification window. |
| 6 | **Complete** | Exact immutable single/multipart publication, lost-response resolution, resume/replay, checksum/encryption/retention verification, isolated stale cleanup, signed catalog rebuild, recovery replication, and denied recovery-role mutation pass live. | None. Preserve retained Development evidence until its approved retention expires. |
| 7 | **Complete** | Online/offline KMS evidence, wrong Region/account/key usage, recovery-role and direct-key denial, disabled/untrusted failure, and revision-2 two-key rollover overlap pass. | None. A production rollover still requires its own authorization. |
| 8 | **Complete** | Exact duplicate/lease split-brain, reordered restart, publication failure, cancellation, AWS/local isolation, and live host-only restart pass; NATS remains uninterrupted. | None. |
| 9 | **Complete** | PostgreSQL 17 full plus six physical incrementals verify/combine/boot; signed AWS chain and WAL gap/fill/replay recover through both vaults; persistent bounded spool pressure/restart passes; measured recovery lag is recorded. The recovery-region `ReplicationLatency` alarm passed a controlled `ALARM`/`OK` transition, the failure alarm is `OK`, and the recovery stack is `IN_SYNC` with zero drift. | None. Detach the temporary interactive qualification policy after evidence commit. |
| 10 | **Complete** | A signed full-plus-six chain from both vaults feeds fresh native roots and native combine/restore; real PostgreSQL 17 full, six-incremental, and UTC PITR boots pass; the full required corruption/dependency/WAL/timeline/version/KMS/credential/freshness negative matrix passes. | None for Development Gate 10. Production recovery remains unauthorized. |
| 11 | **In progress** | Typed Scylla topology/snapshot evidence now crosses the native boundary, signed AWS publication, catalog, recovery selection, and native restore. Protection-set policy rejects incomplete or fabricated dependency sets; deterministic tests and a disposable native Scylla Docker restore pass. | Qualify a live multi-node Manager protection set through AWS and record reconciliation plus node/Manager partial-failure evidence. |
| 12 | **In progress** | Native restore now enforces the exact signed Scylla topology and snapshot expectation before mutation; deterministic incomplete/topology/corruption rejection tests pass. | Restore complete live Scylla protection sets from both vaults, run the full negative matrix, and measure RPO/RTO. |
| 13 | **In progress** | Independent primary/recovery catalog qualification now compares signed logical identity, lineage, exact immutable versions, checksums, retention, Region, KMS key, and replica identity. | Run authorized recovery-only PostgreSQL and Scylla restores with primary access blocked, including archive retrieval. A distinct recovery account is still required for literal cross-account proof. |
| 14 | **In progress** | Deterministic exact-version retention planning, dependency closure, signed authorization, drift/hold/retention/replica checks, constrained deletion, and partial-progress reconciliation are implemented and tested. | Obtain independent approval and execute only an authorized expired non-evidence plan in AWS; retain all Gates 4-10 evidence until its approved retention expires. |
| 15 | **In progress** | Bounded AWS runtime/health telemetry, state projection policy, failure recording, and the required operations runbooks are implemented and tested. | Export the meters, deploy dashboards/alarms, and record UI/Console drill, alert-routing, retry, and exception-noise evidence. |
| 16 | **In progress** | Capacity/concurrency safeguards, a component-complete rate-injected cost model, warning-free build, infrastructure/IAM and credential scans, fault-semantics tests, and dependency vulnerability audit pass. | Complete authorized workload/load/fault/security qualification, measure real costs and capacity, remediate findings, and obtain formal risk acceptance. |
| 17 | **Not started** | Staging topology, soak, game-day, ownership, and readiness criteria are documented. | Deploy production-shaped staging, complete soak/recovery game days, close findings, and obtain readiness approvals. |
| 18 | **Not started** | Controlled rollout, overlap, canary, production-derived drill, and acceptance criteria are documented. | Obtain production authorization, roll out gradually, complete recovery-vault drills, prove RPO/RTO, and obtain final acceptance. |

**Current overall result:** Gates 0 through 10 are complete in Development. Gates 11 through 16 have an implemented
deterministic qualification slice but remain open for live AWS qualification; see
`AWS-Cloud-Backup-Restore-Gates-11-16-Validation-Report.md`. The post-Gate-9 Development recovery stack
is `UPDATE_COMPLETE` and `IN_SYNC` with zero drifted resources. See
`AWS-Cloud-Backup-Restore-Gates-5-10-Validation-Report.md`. Staging and Production remain empty-account deny-all.

### Gate 0 - Baseline, decisions, and authorization boundary

**Implementation result:** Complete on 2026-08-21. Evidence is recorded in
`AWS-Cloud-Backup-Restore-Gate-0-Baseline.md`, `AWS-Cloud-Backup-Restore-Gate-0-Decision-Record.md`,
`AWS-Cloud-Backup-Restore-Gate-0-Threat-Cost-Deletion-Model.md`, and
`AWS-Cloud-Backup-Restore-Gate-0-Validation-Report.md`.

**Steps**

1. Freeze and record the four architecture/specification versions named above.
2. Inventory existing contracts, manifest fields, journal methods, projections, local processor behavior, PostgreSQL
   and Scylla native versions, host composition, and all currently passing tests.
3. Record ADRs for infrastructure-as-code technology, production accounts/Regions, recovery objectives, retention
   classes, signing algorithm/key topology, staging location, and Scylla transfer mechanism.
4. Define development, staging, and production AWS account allowlists and change approvers.
5. Confirm environment-variable names without reading or displaying values.
6. Add a read-only STS preflight command and prove it rejects an unexpected account/Region.
7. Produce threat model, data classification, cost estimate, and resource-deletion policy.

**Exit evidence**

- Baseline tests pass unchanged; ADRs and threat model are approved.
- No secret exists in tracked files, logs, command history captured in reports, or configuration output.
- The approved caller/account/Region matrix and RPO/RTO targets are explicit.

**Rollback**: Documentation and read-only tooling can be removed; no AWS mutation has occurred.

### Gate 1 - Solution scaffolding and source-neutral host composition

**Implementation result:** Complete on 2026-08-21. Evidence is recorded in
`AWS-Cloud-Backup-Restore-Gate-1-Validation-Report.md`.

**Steps**

1. Add the AWS production, unit-test, and integration-test projects to the solution.
2. Add current compatible stable AWS SDK for .NET v4 dependencies and dependency-audit automation.
3. Refactor `AddDatabaseBackupHost` so LocalWorkstation and AwsCloud registrations bind independent options and can be
   enabled separately or together.
4. Replace local-options dependencies in dispatcher/outbox services with source-neutral host options.
5. Register one processor per enabled source and retain strict rejection of unsupported sources.
6. Add configuration validation, redaction tests, structured logging scopes, and source-specific health indicators.

**Exit evidence**

- Full solution builds with warnings treated according to repository policy.
- Existing local unit/integration tests remain green.
- AWS-disabled startup performs no AWS API call and leaves local behavior unchanged.
- Enabling an incomplete AWS profile fails with one bounded configuration error, not repeated exceptions.

**Rollback**: Disable `AwsCloud`; local composition and message compatibility remain intact.

### Gate 2 - Shared policy extraction and compatibility lock

**Implementation result:** Complete on 2026-08-21. Evidence is recorded in
`AWS-Cloud-Backup-Restore-Gate-2-Validation-Report.md`.

**Steps**

1. Move destination-neutral manifest validation, canonical serialization rules, chain planning, and retention dependency
   graph logic from the local framework into `Application.DatabaseBackup`.
2. Keep local public behavior unchanged through adapter shims where necessary.
3. Add golden MessagePack fixtures for every AWS-used command/event and golden JSON fixtures for manifest v1/v2.
4. Add property and mutation tests for identifier validation, lineage, cycles, depth, base age, dependency completeness,
   unknown enum values, time normalization, and schema evolution.

**Exit evidence**

- Local and AWS implementations consume the same policy code.
- Golden bytes/JSON prove no existing key, field meaning, or projection mapping changed.
- Manifest v1 read and v2 round-trip tests pass.

**Rollback**: Revert internal extraction without changing persisted or wire schemas.

### Gate 3 - AWS identity, options, and client lifecycle

**Implementation result:** Complete on 2026-08-21. Evidence is recorded in
`AWS-Cloud-Backup-Restore-Gate-3-Validation-Report.md`.

**Steps**

1. Implement safe options and validation for accounts, Regions, ARNs, buckets, table, retention, timeouts, and feature
   flags; prohibit credential fields.
2. Use the SDK default credential chain and role assumption with bounded refresh behavior.
3. Implement STS identity preflight and partition/account/Region enforcement.
4. Create singleton AWS service clients through dependency injection; do not create a client per request.
5. Centralize retries, timeouts, cancellation, clock, correlation, AWS request-ID capture, and exception classification.
6. Map expected transient failures to bounded status observations without first-chance exception noise loops.

**Exit evidence**

- Unit tests cover absent, static-dev, temporary-session, expired, wrong-account, wrong-Region, and role-denied cases.
- Logs and serialized options pass secret scanning.
- A read-only live-AWS test records the expected caller identity and no credential material.

**Rollback**: Disable AWS registration; no persistent resource dependency exists yet.

### Gate 4 - Reviewed infrastructure as code in development

**Implementation result:** Complete on 2026-08-22. Development deployment, clean drift, safe output capture, live
negative IAM tests, and immutable canary qualification passed under `IFM-GATE4-20260822`. Evidence is recorded in
`AWS-Cloud-Backup-Restore-Gate-4-Validation-Report.md`.

**Steps**

1. Define workload DynamoDB journal with customer-managed encryption, PITR, deletion protection, alarms, and tags.
2. Define primary and recovery S3 general-purpose buckets with Versioning, Object Lock, public-access blocks, bucket-
   owner-enforced ownership, TLS-only policies, inventory, lifecycle, and access logging/audit events.
3. Define independent primary/recovery symmetric KMS keys and an asymmetric signing key with aliases, rotation/renewal
   procedure, deletion protection controls, and least-privilege key policies.
4. Define upload, verification, replication, recovery-read, retention-plan, retention-execution, legal-hold, and audit
   roles. Separate administration from data operations.
5. Configure cross-account, cross-Region replication including encrypted objects, Object Lock metadata, metrics, and
   failure alarms.
6. Configure CloudTrail management events, S3 object data events, KMS events, configuration compliance, budgets, and
   immutable audit-log destination.
7. Add policy-as-code and infrastructure tests for wildcard actions/resources, public access, unencrypted writes,
   retention bypass, key deletion, source-account conditions, and confused-deputy protections.
8. Generate resource outputs consumed as non-secret deployment configuration.

**Exit evidence**

- Change set/plan has the approval required for the environment. A sole-owner exception may satisfy Development only
  when recorded with bounded scope and compensating controls; later environments retain independent approval.
- Development deployment is idempotent and drift detection is clean.
- Negative policy tests prove the normal workload role cannot delete versions, bypass retention, administer keys,
  modify replication, or access the recovery vault.
- A disposable test object demonstrates Versioning, expected encryption, retention, replication, and audit events.

**Rollback**: Remove only disposable development resources using reviewed IaC after retention permits it; compliance-
locked objects are allowed to expire and are never bypassed.

### Gate 5 - DynamoDB execution journal

**Steps**

1. Implement all `IDatabaseBackupExecutionJournal` methods and seven logical record families.
2. Implement conditional state versions, transactions, inbox idempotency, outbox sequencing, leases with fencing,
   reconciliation scans/indexes, pagination, consistent-read resolution, and bounded item serialization.
3. Add fault injection for throttling, conditional conflicts, ambiguous timeouts, cancellation, process termination,
   duplicate delivery, and clock skew.
4. Add journal migration/version handling and a PITR restore-to-new-table/runbook test.

**Exit evidence**

- Contract tests pass unchanged against SQLite and DynamoDB.
- Two competing workers cannot execute one operation twice.
- Crash/restart resumes from the last durable checkpoint and outbox delivery remains at-least-once/idempotent.
- Restored journal table is reconfigured with required tags, alarms, streams/TTL settings if applicable, and PITR.

**Rollback**: Disable AWS processor and preserve the table for forensic review; never downgrade in-place journal data.

### Gate 6 - S3 immutable artifact publication

**Steps**

1. Implement generated object keys, bounded staging, multipart upload, upload checkpointing/resume, checksums, SSE-KMS,
   encryption context, retention headers, exact version-ID capture, and read-back verification.
2. Implement safe abort/reconciliation of stale multipart uploads.
3. Implement immutable engine-manifest, publication-record, evidence, and catalog-entry writes in publication order.
4. Reject overwrite semantics, missing version IDs, unexpected encryption/key/retention, checksum mismatch, and partial
   publication.
5. Implement catalog enumeration and full rebuild solely from signed immutable records.

**Exit evidence**

- Single-part and multipart integration tests pass at boundary sizes.
- Corruption, truncation, duplicate key, dropped response, wrong key, and wrong retention tests fail closed.
- A catalog deleted from working state is rebuilt to the same logical content from S3 evidence.
- Normal application roles cannot alter or delete published versions.

**Rollback**: Stop new uploads; retain immutable test artifacts until expiry and abort only verified incomplete uploads.

### Gate 7 - KMS signatures and recovery trust bundle

**Steps**

1. Define canonical digest input for engine manifests and publication records.
2. Implement KMS `Sign`/`Verify` using an asymmetric `SIGN_VERIFY` key and explicit algorithm.
3. Record signing key ARN/version identity and algorithm in signature envelopes.
4. Export only the public key and approved key metadata into a versioned offline recovery trust bundle.
5. Implement key rollover with an overlap period; old evidence remains verifiable for its full retention.
6. Test disabled key, wrong Region/account, untrusted key, changed document, changed version ID, and signature replay.

**Exit evidence**

- Online and offline verification agree on golden signed fixtures.
- Private key material is never exportable or present in the repository/runtime.
- Recovery still validates pre-rollover restore points after rollover.

**Rollback**: Keep old trusted public keys; disable new signing without invalidating existing evidence.

### Gate 8 - AWS processor orchestration and publication state machine

**Steps**

1. Implement `AwsCloudDatabaseRecoveryProcessor` and engine selector over existing application interfaces.
2. Enforce source, operation kind, phase, cancellation, idempotency, and explicit restore-replica rules.
3. Journal every irreversible transition before acknowledging it and publish observations through the existing outbox.
4. Implement restart reconciliation for native staging, multipart uploads, object versions, catalog publication, and
   replication-pending states.
5. Add bounded health/readiness details for journal, primary vault, KMS, WAL, replication lag, and native capabilities.

**Exit evidence**

- State-machine, duplicate-message, reorder, cancellation, restart, and split-brain tests pass.
- No operation reports success before catalog publication and verification.
- AWS unavailability degrades only the AWS processor; the host, UI, actors, and local processor remain available.

**Rollback**: Stop accepting new AWS intents, drain/reconcile in-flight work, retain evidence, and keep local enabled.

### Gate 9 - PostgreSQL capture and continuous WAL publication

**Steps**

1. Validate PostgreSQL version, system identifier, timeline, permissions, replication/backup settings, WAL archive
   configuration, staging space, and supported native tools.
2. Integrate existing typed full/incremental capture with AWS publication.
3. Implement WAL ingestion, deterministic identity, idempotent publication, gap/lag detection, timeline-history
   handling, retention linkage, and alarms.
4. Enforce Automatic fallback and explicit Incremental failure rules, maximum chain depth, maximum base age, and direct-
   parent manifest dependencies.
5. Exercise simultaneous backup/WAL activity, slow S3, network loss, process restart, and source failover.

**Exit evidence**

- Full plus at least six incrementals publish and verify with correct direct-parent lineage.
- WAL is contiguous across the declared PITR interval; intentional gaps make affected points ineligible.
- No backup mode silently changes except documented Automatic fallback with recorded reason.
- Primary and recovery replica lag alarms operate on measured recovery impact.

**Rollback**: Stop new AWS captures while preserving WAL locally under an approved bounded spool policy; alert before
spool exhaustion and never discard required WAL silently.

### Gate 10 - PostgreSQL restore and PITR qualification

**Steps**

1. Implement explicit primary/recovery replica selection and dependency-complete download.
2. Verify every object version, signature, digest, length, native metadata, system identifier, timeline, and WAL range.
3. Use `pg_combinebackup` for incremental chains and prove the resulting restore input is complete.
4. Restore to fresh isolated PostgreSQL targets for full, every supported chain depth, and selected PITR timestamps.
5. Run native consistency, schema/migration, required extension, role/privilege, row-level/application invariant, and
   read/write smoke validation.
6. Measure download, archive retrieval, combine, recovery, and application-validation time separately.

**Exit evidence**

- Full, incremental-chain, and PITR restores pass from both primary and recovery vaults.
- Tests include corrupted artifact, missing parent, missing WAL, wrong timeline, incompatible version, KMS denial,
  expired credentials, and target-not-fresh rejection.
- Measured RPO/RTO meets the approved class or the policy is revised before production.

**Rollback**: Dispose only the approved isolated target; source databases and published evidence are untouched.

### Gate 11 - Scylla capture and AWS publication

**Steps**

1. Validate Manager version/API, cluster identity, schema, topology/token ownership, node reachability, staging, and
   supported restore sequence.
2. Implement protection-set snapshot orchestration and AWS artifact publication.
3. Record Manager task/run identity, node completeness, schema/topology evidence, and deduplicated lineage.
4. Handle partial node failure, Manager timeout, topology change, repair conflict, duplicate completion, slow upload,
   and restart reconciliation.

**Exit evidence**

- Multi-node full and deduplicated snapshot publications pass with complete signed evidence.
- A missing node or unresolved Manager task prevents restore-point eligibility.
- The manifest contains no fabricated IFM dependency chain for deduplicated Scylla snapshots.

**Rollback**: Stop new tasks and reconcile/expire staging; do not delete valid Manager snapshots or immutable objects.

### Gate 12 - Scylla restore qualification

**Steps**

1. Provision a fresh isolated cluster with a compatible topology/version.
2. Download and verify the exact selected replica's schema, topology, and node artifact set.
3. Execute the approved native restore sequence and complete required repair/consistency operations.
4. Validate schema, partitions, representative data, application queries, consistency levels, and post-restore health.
5. Test missing/corrupt node data, incompatible topology/version, wrong cluster identity, KMS denial, expired
   credentials, and recovery-vault-only operation.

**Exit evidence**

- Complete restore succeeds from both vaults and passes application-level validation.
- Partial protection sets and stale topology evidence fail before native mutation.
- Measured RPO/RTO is documented and approved.

**Rollback**: Dispose only the isolated target according to the approved drill plan.

### Gate 13 - Cross-account replication and recovery-source qualification

**Steps**

1. Correlate source and destination object versions for every artifact and evidence object.
2. Verify destination ownership, recovery KMS key, checksum, length, retention, legal hold, signature, and dependency
   completeness independently of source status.
3. Model pending, completed, delayed, failed, and permanently failed replication without treating S3 replication as
   native restore verification.
4. Test primary Region denial and primary account denial while rebuilding the catalog and restoring through only the
   recovery role.
5. Test storage-class/archive retrieval and include retrieval delay in the relevant RTO.

**Exit evidence**

- Exact-version recovery evidence exists for every policy-required restore point.
- Replication delay/failure produces actionable alarms and accurate UI/Console state without exception spam.
- Recovery-only PostgreSQL and Scylla restores pass with primary access blocked.

**Rollback**: Pause new AWS publication if recovery protection falls outside policy; never delete primary evidence to
force replication convergence.

### Gate 14 - Retention, legal hold, and controlled deletion

**Steps**

1. Build a catalog-derived graph that includes PostgreSQL direct parents, bases, WAL intervals, manifests,
   publications, catalogs, evidence, and all required replicas.
2. Generate a deterministic dry-run plan containing plan ID, revision, policy revision, exact bucket/key/version IDs,
   reason, retain-until/legal-hold observations, and expected reclaimed bytes.
3. Require independent approval and assume a narrowly scoped execution role only after plan validation.
4. Re-read object state immediately before each delete; reject drift, new legal hold, unexpired retention, dependency,
   wrong version, wrong environment, or changed plan revision.
5. Reconcile lifecycle outcomes and prove at least one policy-compliant recovery chain remains for each required class.

**Exit evidence**

- Dry-run, stale-plan, legal-hold, compliance-retention, dependency, wrong-account, and partial-execution tests pass.
- No API accepts a prefix, wildcard, bucket sweep, or caller-supplied raw object key for deletion.
- Deletion evidence is immutable and catalog rebuild reflects the lawful outcome.

**Rollback**: There is no undo for an expired, authorized object-version deletion; prevention gates and retained
independent replicas are mandatory. Stop immediately on drift or partial failure and reconcile before continuing.

### Gate 15 - Operator surfaces, observability, and runbooks

**Steps**

1. Keep UI, Console, and ScheduledTask on existing source-neutral commands/queries; expose AWS by source selection,
   replica state, lineage, recoverable time, verification, retention, and bounded failure reason.
2. Add metrics and alarms for intent age, operation phase age, journal conflicts, outbox backlog, upload throughput,
   stale multipart uploads, WAL lag/gaps, replication lag/failure, KMS denial, restore verification, retention drift,
   RPO, RTO, and cost anomalies.
3. Ensure routine after-hours or disabled-source states do not make the rest of IFM unavailable.
4. Write runbooks for credential failure, wrong-account rejection, KMS/key recovery, WAL gap, replication failure,
   journal PITR, multipart reconciliation, catalog rebuild, primary-vault loss, recovery-only restore, legal hold,
   retention-plan failure, and fresh-target cleanup.
5. Add dashboards with links from alerts to operation IDs, never to credentials or unredacted native output.

**Exit evidence**

- UI and Console show the same projected state and never call AWS directly.
- Alert drills reach the correct runbook and distinguish unavailable, degraded, pending, and failed.
- Log-volume tests show bounded retries and no repeated first-chance exception noise for expected conditions.

**Rollback**: Hide AWS operator actions with a feature flag while preserving query/history visibility.

### Gate 16 - Security, resilience, performance, and cost qualification

**Steps**

1. Run static analysis, dependency/vulnerability scanning, secret scanning, IaC policy scanning, least-privilege review,
   and threat-model reassessment.
2. Inject S3 throttling/timeouts, DynamoDB throttling/conflicts, STS/KMS expiry/denial, DNS/network partitions, host
   termination, disk pressure, corrupted staging, duplicate NATS delivery, clock skew, and Region/account isolation.
3. Load-test the largest supported backup, deepest chain, WAL rate, Scylla protection set, multipart concurrency,
   catalog rebuild, and restore download without starving the production database.
4. Measure storage, requests, replication, KMS, DynamoDB, inventory, audit, retrieval, egress, and drill costs.
5. Confirm backup and restore hosts have CPU, memory, disk, bandwidth, and concurrency limits and safe backpressure.

**Exit evidence**

- No unresolved critical/high security finding; exceptions require named owner, expiry, and approval.
- Recovery correctness survives every approved fault scenario or fails closed with actionable evidence.
- Capacity headroom, RPO/RTO, and monthly cost bounds are approved.

**Rollback**: Reduce or disable new capture concurrency; never weaken immutability, verification, or required retention
to meet performance/cost targets.

### Gate 17 - Staging dress rehearsal and production readiness

**Steps**

1. Deploy the exact reviewed topology to staging through the approved pipeline.
2. Run scheduled full/incremental PostgreSQL, continuous WAL, Scylla snapshot, replication, retention dry-run, catalog
   rebuild, primary restore, and recovery-only restore for the agreed soak period.
3. Execute operator game days with primary account/Region access blocked and Core/NATS/journal unavailable.
4. Finalize on-call ownership, escalation, recovery approvers, cutover approvers, access reviews, budget alarms, evidence
   retention, drill calendar, and rollback decision tree.
5. Produce a signed readiness report mapping every architecture requirement to evidence.

**Exit evidence**

- Zero unexplained missed protection points during soak.
- Recovery-only PostgreSQL PITR and Scylla restores meet approved RPO/RTO.
- Security, database, operations, application, and business owners sign the readiness report.

**Rollback**: Staging remains available for diagnosis; production AWS source remains disabled.

### Gate 18 - Controlled production rollout and acceptance

**Steps**

1. Deploy infrastructure, then software, with AWS request acceptance still disabled.
2. Run read-only identity/configuration/permission checks and a non-production canary artifact in the production vault
   under an explicitly approved canary retention class.
3. Enable AWS backups for one protection set, observe journal/publication/replication/WAL evidence, then expand by an
   approved sequence.
4. Keep the local destination operational until AWS production acceptance and the agreed overlap period complete.
5. Execute production-data restore drills into isolated non-production targets from primary and recovery vaults.
6. Declare AWS trusted only after complete signed evidence and owner approval; update recovery policy and runbooks.

**Exit evidence**

- Required scheduled restore points and WAL coverage are continuously eligible in both vaults.
- At least one full production-derived PostgreSQL PITR drill and one full Scylla drill pass from the recovery vault.
- RPO/RTO, replication, retention, alerts, catalog rebuild, and break-glass acceptance are signed.
- No credential, destructive permission, or private data leaked into logs/evidence.

**Rollback**: Stop new AWS intents and preserve all evidence. Continue local protection and WAL spool according to the
approved overlap plan; do not destroy AWS resources or locked objects as part of application rollback.

## 12. Test matrix

| Level | Required scope | AWS mutation |
| --- | --- | --- |
| Unit | Serialization, validation, key generation, policy, state machine, retry classification, redaction | None |
| Contract | Same journal/publication/planner behavior across local and AWS adapters | None or isolated fixture |
| Component | SDK clients behind deterministic HTTP/test doubles; timeout, retry, checksum, signing, pagination | None |
| Development AWS | Dedicated account/buckets/table/keys; publication, replication, PITR, retention, restore | Approved disposable resources |
| Staging | Production-shaped three-account/two-Region topology and native database targets | Approved staging resources |
| Production canary | Identity/policy checks and tightly scoped canary publication | Explicit approval only |
| Production drill | Read-only recovery evidence plus isolated restore target | Separate restore approval |

Emulators may accelerate component tests but cannot qualify IAM, KMS policy, S3 Object Lock, cross-account replication,
CloudTrail, archive retrieval, or production recovery. Those require live AWS tests in allowlisted accounts.

The minimum regression suite includes:

- all pre-existing local backup/restore tests;
- shared MessagePack and manifest golden fixtures;
- duplicate/reordered NATS delivery and process restart;
- wrong account/Region/key/bucket/table and expired credentials;
- S3 single/multipart checksum and ambiguous completion;
- DynamoDB conditional conflict, transaction cancellation, throttling, PITR restore, and pagination;
- KMS sign/verify, rollover, disabled key, and offline public-key verification;
- PostgreSQL full, incremental depths 1 through maximum, PITR, WAL gap, wrong timeline, and corrupted parent;
- Scylla complete/partial protection set, topology change, corrupted node artifact, and recovery-only restore;
- replication delay/failure, archive retrieval, primary isolation, and catalog rebuild;
- Object Lock, legal hold, stale retention plan, dependency retention, and constrained deletion; and
- UI/Console projection parity, health isolation, bounded errors, and secret/log scans.

## 13. CI/CD and environment controls

1. Pull requests run unit, contract, component, schema compatibility, secret, dependency, and IaC-policy tests only.
2. Live development AWS tests require an explicit pipeline environment, account allowlist, concurrency lock, and test
   prefix/run ID. They never run from an ordinary build.
3. Staging deployment requires an reviewed infrastructure plan and software artifact digest.
4. Production infrastructure and software approvals are separate. Production restore, legal hold, and deletion use
   separate protected workflows and identities.
5. Pipelines use workload identity/role federation, not repository secrets containing long-lived keys.
6. Every deployment records source commit, package lock/inventory, IaC digest, account, Region, role ARN, safe resource
   identifiers, approver, start/end time, tests, and rollback result.
7. The NATS service remains running throughout backup-host test cycles unless a specific fault-injection case explicitly
   isolates the backup host. Tests must not stop unrelated application infrastructure.

## 14. Required operational artifacts

Before production acceptance, the repository must contain or link to reviewed versions of:

- account/Region/resource inventory and ownership matrix;
- IAM and KMS role/permission matrix;
- data-flow and threat model;
- RPO/RTO and retention policy by protection set;
- AWS configuration reference with all secret fields explicitly prohibited;
- journal schema/access-pattern/migration document;
- engine manifest, signature envelope, publication record, catalog, and evidence schemas;
- PostgreSQL WAL continuity and PITR runbook;
- Scylla complete-cluster restore runbook;
- primary-vault and recovery-only restore runbooks;
- catalog rebuild and DynamoDB PITR runbooks;
- KMS rollover/loss and trust-bundle runbooks;
- replication failure and archive retrieval runbooks;
- legal-hold, retention planning, deletion, and audit runbooks;
- monitoring dashboard/alert catalog and on-call routing;
- cost model and budget thresholds;
- test-data disposal and fresh-target cleanup procedure; and
- gate validation reports plus final traceability/readiness report.

## 15. Definition of done

The AWS implementation is fully complete only when all statements below are true:

1. Gates 0 through 18 have approved validation reports with no unresolved blocking finding.
2. `BackupSource.AwsCloud` works through the existing UI, Console, ScheduledTask, actor, projection, and NATS contracts.
3. Local and AWS sources can run in the same host without configuration, health, journal, or processor collisions.
4. PostgreSQL full/incremental capture, continuous WAL, chain materialization, and PITR restore are qualified.
5. Scylla complete protection-set capture and fresh-cluster restore are qualified.
6. Primary and recovery vault objects are immutable, encrypted under independent customer-managed keys, versioned,
   signed, checksummed, cataloged, and independently verified.
7. Catalog rebuild and recovery work without Core, NATS, workload databases, journal, or primary-vault access.
8. Production roles use temporary credentials; repository, configuration, logs, evidence, and artifacts contain no AWS
   secrets.
9. Normal runtime identities cannot delete object versions, bypass retention, administer keys, modify replication, or
   access the independent recovery role.
10. Retention cannot delete an object needed by an eligible chain, WAL window, legal hold, policy minimum, or replica.
11. Expected unavailability produces bounded degraded state and actionable alerts, not host/UI termination or repeated
    debug-console exceptions.
12. Production-derived recovery-vault drills demonstrate approved RPO/RTO for PostgreSQL and ScyllaDB.
13. Break-glass recovery and signing verification work from the controlled offline bundle.
14. Operations accepts ownership, access reviews, alert response, drill schedule, and cost controls.

## 16. Open decisions that Gate 0 must close

| Decision | Required owner/evidence |
| --- | --- |
| IaC technology | Platform owner; repository/tooling fit and deployment pipeline |
| Primary/recovery accounts and Regions | Security/business; isolation, residency, and service availability |
| RPO/RTO per protection set | Business/database owners; measured workload requirements |
| Retention classes and Object Lock mode transition | Legal/security/operations; governance test then production compliance approval |
| KMS signing algorithm and rollover interval | Security; SDK/runtime support and offline verification |
| PostgreSQL WAL spool size and outage behavior | Database/operations; peak WAL rate and maximum AWS outage |
| Scylla Manager transfer/staging mechanism | Database/platform; installed Manager version and supported native process |
| Archive storage class by recovery class | Business/operations; retrieval time and total cost |
| Production soak/overlap duration with local backup | Database/business; risk acceptance |
| Drill frequency and production-derived data handling | Security/business; recovery assurance and data controls |

None of these decisions may be silently inferred from the presence of workstation AWS credentials.

## 17. Primary implementation sequence and critical path

```text
G0 decisions/security boundary
  -> G1 scaffolding/composition
  -> G2 shared compatibility
  -> G3 identity/options
  -> G4 development infrastructure
  -> G5 journal
  -> G6 S3 publication
  -> G7 KMS signing
  -> G8 orchestration
       -> G9 PostgreSQL capture/WAL -> G10 PostgreSQL restore
       -> G11 Scylla capture       -> G12 Scylla restore
  -> G13 recovery-vault qualification
  -> G14 retention
  -> G15 operator/observability
  -> G16 security/resilience/performance
  -> G17 staging readiness
  -> G18 controlled production acceptance
```

G9/G10 and G11/G12 may proceed in parallel only after G8, but G13 requires successful restore evidence from both
engines. Retention execution must not be enabled before G13 proves independent replicas and G14 passes.

## 18. Authoritative AWS references

Gate 0 implementation evidence:

- [Gate 0 baseline](AWS-Cloud-Backup-Restore-Gate-0-Baseline.md)
- [Gate 0 decision record](AWS-Cloud-Backup-Restore-Gate-0-Decision-Record.md)
- [Gate 0 threat, cost, and deletion model](AWS-Cloud-Backup-Restore-Gate-0-Threat-Cost-Deletion-Model.md)
- [Gate 0 validation report](AWS-Cloud-Backup-Restore-Gate-0-Validation-Report.md)

- AWS SDK for .NET v4 credential resolution:
  https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-assign.html
- AWS SDK shared credentials files:
  https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-file.html
- AWS STS `GetCallerIdentity`:
  https://docs.aws.amazon.com/STS/latest/APIReference/API_GetCallerIdentity.html
- S3 Object Lock:
  https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock.html
- S3 multipart uploads and checksums:
  https://docs.aws.amazon.com/AmazonS3/latest/userguide/mpuoverview.html
- S3 replication requirements:
  https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication-requirements.html
- Replicating SSE-KMS encrypted objects:
  https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication-config-for-kms-objects.html
- DynamoDB transactional writes:
  https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_TransactWriteItems.html
- DynamoDB point-in-time recovery:
  https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Point-in-time-recovery.html
- AWS KMS `Sign`:
  https://docs.aws.amazon.com/kms/latest/APIReference/API_Sign.html
- AWS KMS asymmetric signing keys:
  https://docs.aws.amazon.com/kms/latest/developerguide/asymm-create-key.html
- NuGet `AWSSDK.S3`:
  https://www.nuget.org/packages/AWSSDK.S3
- NuGet `AWSSDK.DynamoDBv2`:
  https://www.nuget.org/packages/AWSSDK.DynamoDBv2
- NuGet `AWSSDK.KeyManagementService`:
  https://www.nuget.org/packages/AWSSDK.KeyManagementService
- NuGet `AWSSDK.SecurityToken`:
  https://www.nuget.org/packages/AWSSDK.SecurityToken

## 19. Version history

| Version | Date | Change |
| --- | --- | --- |
| 0.1 | 2026-08-21 | Created the complete Gate 0 through Gate 18 AWS implementation, qualification, and production-acceptance plan. |
| 0.2 | 2026-08-21 | Added the implementation status dashboard, current result/blocker for every gate, readiness boundary, and verified AWS SDK v4 NuGet package snapshot. |
| 0.3 | 2026-08-21 | Completed Gate 0: froze the baseline, accepted the ADR/control set, installed and verified AWS operator tools, added and qualified the fail-closed STS preflight, passed the shared/native regression baseline, and retained deny-all staging/production plus no-mutation policy. |
| 0.4 | 2026-08-21 | Completed Gates 1-3 and the repository-side Gate 4 implementation: added source-neutral/AWS composition, shared compatibility policy, safe identity/client lifecycle, tested CloudFormation/policy controls, benchmarks, validation evidence, and retained the mandatory no-mutation approval boundary. |
| 0.5 | 2026-08-22 | Recorded the sole-owner Development approval exception, enabled and live-validated `ca-west-1`, prepared bounded CloudFormation execution/deployer policies and deterministic inputs, hardened deployment ordering and log/inventory delivery policies, and retained the fail-closed mutation boundary. |
| 0.6 | 2026-08-22 | Completed Gate 4: deployed four Development stacks, separated AWS Config delivery from immutable CloudTrail retention, proved clean drift and bounded IAM denies, captured safe outputs, and qualified a retained KMS-encrypted cross-Region canary with immutable audit evidence. |
