# IFM AWS Cloud Database Backup and Restore Architecture

Status: Architecture aligned to the implemented LocalWorkstation contract and storage baseline; AWS adapters remain planned
Version: 0.6
Date: 2026-08-21
Scope: AWS reference architecture for PostgreSQL and ScyllaDB backup, restore, retention, and disaster recovery
Parent architecture: Database-Backup-Architecture-Overview.md version 0.9

## 1. Purpose

This document defines the AWS reference architecture for protecting and recovering the IFM PostgreSQL and ScyllaDB
clusters. It specializes the shared architecture in Database-Backup-Architecture-Overview.md without changing its
SystemAdmin actors, event-sourced state model, database-native recovery formats, restore governance, or Docker/Aspire
host boundary.

The design answers the AWS-specific questions intentionally deferred by the overview:

- AWS account and Region isolation;
- Amazon S3 bucket roles, object layout, immutability, replication, and publication;
- AWS Identity and Access Management roles and separation of duties;
- AWS Key Management Service encryption and recovery-key controls;
- network and credential boundaries;
- PostgreSQL WAL and full/incremental physical-backup movement to AWS;
- ScyllaDB cluster-backup movement to AWS;
- destination-resident catalog, manifest, verification, and break-glass recovery;
- lifecycle, retention, legal hold, audit, observability, cost, and failure behavior; and
- the production acceptance evidence required before AWS becomes a trusted recovery destination.

This remains an architecture and design document. It does not prescribe Terraform or CloudFormation modules, AWS
resource names, command-line scripts, or a delivery task breakdown. Where the LocalWorkstation implementation has now
established shared C# message, manifest, projection, and journal schemas, those schemas are the AWS implementation
baseline and are named explicitly. The AWS implementation adds destination adapters and AWS evidence; it does not fork
the shared contracts.

## 2. Relationship to the shared architecture

This document inherits the following non-negotiable rules from the overview:

1. SystemAdmin contains exactly three backup actor roles: Command Actor, Event Actor, and Query Actor.
2. The SystemAdmin Command Actor is the authority for event-sourced operation state and execution intent.
3. The SystemAdmin Event Actor translates service observations into commands and acknowledges only after durable
   Command Actor application or an idempotent prior application.
4. The SystemAdmin Query Actor reads projected models and never queries AWS or the Database Backup Service as a hidden
   source of truth.
5. The Database Backup Service executes all native backup, transfer, verification, retention, and restore behavior.
6. The service boundary is implemented first as the standalone **TomasAI.IFM.Api.DatabaseBackup.Host** Worker and never
   runs inside **Api.Server**. Ubuntu 24.04 Docker packaging follows functional paper-trading qualification; Aspire is
   deferred to the later full-system Linux production migration.
7. NATS carries bounded control and outcome events between Core and the Database Backup Host in every environment.
   Native backup payloads never travel through NATS or actor mailboxes.
8. PostgreSQL and ScyllaDB are protected at their physical cluster or declared protection-set boundaries.
9. Restore uses fresh targets by default and stops at **ReadyForCutover** until separately approved.
10. A break-glass recovery path works without Core, NATS, PostgreSQL, or ScyllaDB.
11. AWS and local destinations use the same operation identities, manifest meaning, checksums, catalog semantics,
    verification levels, retention dependencies, and restore qualification.
12. AWS credentials exist only within the Database Backup Service trust boundary. Core actors and ordinary database
    workloads never receive them.
13. DatabaseBackup event, command, and query types are shared with local workstation backup. The shared BackupSource
    enum is **None**, **LocalWorkstation**, and **AwsCloud**. This processor accepts only **AwsCloud** operations; every
    AWS source-bound event carries it and no AWS-specific event type is introduced.
14. UI callers, Database Backup Console callers, and SystemAdmin ScheduledTask actors use the same DatabaseBackup
    command and query contracts with distinct RequestOrigin and authenticated identities. ScheduledTask event
    integration remains outside this document.
15. PostgreSQL and ScyllaDB operations are exposed only through the shared high-level, allowlisted backup/restore
    capability interfaces; database-native protocols, utilities, and Manager APIs are adapter details.
16. `SystemAdminDbContext` stores rebuildable projections and bounded run statistics in Core PostgreSQL; the AwsCloud
    processor never writes that context directly.
17. The Database Backup Host owns an external durable execution journal, while immutable S3 manifests and run evidence
    remain independently usable when Core, the journal, or the workload account is unavailable.
18. The implemented MessagePack contracts are append-only. Existing numeric keys are not reordered or reused. AWS uses
    `RequestDatabaseBackupCommand.RequestedBackupMode` at key 31 and `DatabaseBackupEventContract.BackupLineage` at key
    31 exactly as LocalWorkstation does.
19. The shared backup modes are `None`, `Automatic`, `Full`, and `Incremental`; accepted new backup requests resolve
    `None` as the legacy/default full behavior. AWS must not introduce destination-specific backup-mode enums.
20. Signed IFM engine manifests use `DatabaseBackupManifest` schema version 2. Schema version 1 remains readable as a
    legacy full backup. AWS-specific object-version, KMS, Object Lock, account, and Region evidence belongs in signed
    AWS publication records, not new actor-message fields.
21. `DatabaseBackupLineage` has the same meaning in commands, service/domain events, manifests, catalogs, projections,
    Console, ScheduledTask, and UI state for both sources.
22. PostgreSQL incremental manifests declare exactly their direct parent in `Dependencies`. Scylla Manager snapshots
    are logically complete restore points: deduplicated Scylla lineage is recorded, but IFM does not invent an SSTable
    dependency graph.
23. AWS reuses the implemented `SystemAdminDbContext` projection schema. Source discrimination is the existing
    `BackupSource.AwsCloud`; there are no parallel AWS projection tables.

If this document conflicts with the approved overview, the overview controls until both documents are deliberately
revised and reviewed.

## 3. Architectural decisions

The AWS design adopts these decisions for production:

| Concern | Decision |
| --- | --- |
| Durable object store | Amazon S3 general purpose buckets |
| Workload separation | Production backup storage is not owned by the IFM workload account |
| Primary vault | Dedicated backup account and primary backup Region |
| Recovery vault | Separate recovery account and a distinct AWS Region |
| Replication | One-way S3 Cross-Region Replication from primary vault to recovery vault |
| Immutability | S3 Versioning and S3 Object Lock on both vaults |
| Production lock mode | Compliance mode for published recovery objects after policy validation |
| Encryption | SSE-KMS using a customer-managed regional symmetric key in each vault account |
| Key topology | Independent regional keys; replication decrypts and re-encrypts with the destination key |
| Ownership | Bucket-owner-enforced Object Ownership; ACLs disabled |
| Credentials | Temporary role credentials only; no long-lived AWS access keys in application configuration |
| Artifact publication | Unique immutable object keys, manifest, commit record, then append-only catalog entry |
| Catalog | Destination-resident and reconstructable from immutable manifests; no database dependency |
| Restore source | Explicit replica selection after independent validation; no automatic opaque failover |
| PostgreSQL | Periodic physical full or incremental backup plus continuous WAL archiving for PITR |
| Incremental request policy | Shared `Full | Automatic | Incremental`; `Automatic` may fall back to full, explicit `Incremental` fails when ineligible |
| PostgreSQL incremental | PostgreSQL 17-or-later native incremental capture and dependency-complete `pg_combinebackup` restore, plus continuous WAL for PITR |
| ScyllaDB | Scylla Manager coordinates logically complete snapshots and native physical deduplication; AWS movement remains controlled by the Backup Service |
| Native AWS access | Database and Scylla agents do not receive AWS credentials under this architecture |
| BackupSource | Shared enum is `None`, `LocalWorkstation`, and `AwsCloud`; this processor accepts `AwsCloud`; `AwsPrimary` and `AwsRecovery` remain physical replica identities within that source |
| AWS Backup service | Optional tertiary protection only; it does not replace native capture or the IFM catalog |
| Audit | Organization CloudTrail plus S3 object data events, KMS events, configuration checks, and immutable log storage |
| Deep archive | Policy-controlled and allowed only when its retrieval time still satisfies the recovery class |
| Deletion | A revision-matched retention plan and a separate deletion role; never an unbounded bucket sweep |
| SystemAdmin projections | `SystemAdminDbContext` in Core PostgreSQL; rebuildable and never an execution authority |
| Execution journal | Common execution-journal capability; production AWS profile uses conditional, encrypted durable storage in the workload trust boundary, while immutable recovery evidence remains in the backup/recovery accounts |
| Shared schema baseline | Existing MessagePack key layout, manifest schema v2, SystemAdmin projection tables, and seven logical journal record families from LocalWorkstation |

Production uses three AWS trust domains:

1. **Workload account**: runs or identifies the Database Backup Service and contains no vault administration authority.
2. **Primary backup account**: owns the primary immutable vault, primary KMS key, replication role, and primary catalog.
3. **Recovery account**: owns the cross-Region replica vault, recovery KMS key, and independent recovery-read role.

Non-production may consolidate accounts for cost and convenience, but a consolidated environment cannot be used as
evidence that production account-isolation or disaster-recovery controls work.

## 4. Goals and non-goals

### 4.1 Goals

The AWS architecture must:

- survive loss of the Core Actor Host and active database volumes;
- survive loss or compromise of the workload account without permitting normal workload credentials to erase history;
- retain a geographically separate, independently owned copy of every policy-required restore point;
- provide PostgreSQL point-in-time recovery within the retained WAL window;
- provide complete ScyllaDB cluster recovery for the declared protection set;
- make the backup catalog discoverable without IFM application databases;
- preserve proof of artifact identity, native consistency, checksums, encryption, retention, and verification;
- make incomplete or unreplicated work visibly ineligible for recovery;
- support fresh-target restore drills and production recovery;
- measure actual RPO and RTO;
- constrain cost without weakening declared recovery objectives; and
- run in an independently constrained Database Backup Host, with Docker/Aspire deployment added at their approved gates.

### 4.2 Non-goals

This design does not:

- convert PostgreSQL or ScyllaDB into managed AWS database services;
- use logical row export as the primary cluster recovery mechanism;
- send database artifacts through NATS, HTTP management APIs, or SystemAdmin actors;
- grant Core actors direct S3, KMS, STS, or CloudTrail access;
- treat Amazon S3 replication as proof that a database backup is natively restorable;
- make AWS Backup vaults the authoritative IFM backup catalog;
- use S3 Multi-Region Access Point failover to hide replica identity during recovery;
- permit automatic production cutover;
- put permanent AWS credentials on PostgreSQL or ScyllaDB nodes; or
- promise an RPO or RTO before drills demonstrate it.

## 5. AWS system context

The normal production data path is:

    UI, Console, or ScheduledTask
        |
        | common DatabaseBackup command/query API
        v
    SystemAdmin DatabaseBackup actors
        |
        | committed execution-intent event
        v
    Docker/Aspire Database Backup Service
        |-- PostgreSQL replication/backup interface
        |-- PostgreSQL WAL ingress
        |-- Scylla Manager and service-controlled staging
        |
        | temporary AWS role session
        v
    Primary immutable S3 vault
        |
        | one-way CRR, new encryption under recovery key
        v
    Recovery immutable S3 vault

The control path remains:

    Service observation
        -> SystemAdmin Event Actor
        -> translated SystemAdmin command
        -> SystemAdmin Command Actor
        -> committed domain event
        -> read-model projection
        -> SystemAdmin Query Actor
        -> UI, Console, or ScheduledTask query caller

AWS service notifications and metrics are infrastructure observations. They first enter the Database Backup Service,
which correlates them with its operation journal and publishes a bounded service event. They do not write SystemAdmin
state directly.

## 6. Account, Region, and failure-boundary design

### 6.1 Workload account

The workload account owns the compute identity used by the Database Backup Service and, for the production AWS-hosted
profile, the encrypted DynamoDB execution-journal table and its least-privilege access policy. It does not own either
production vault, either vault KMS key, Object Lock configuration, replication configuration, lifecycle policy, or
recovery audit trail. Journal loss or mutation therefore cannot erase immutable recovery evidence.

The service assumes narrowly scoped roles in the primary backup account. A compromise of the normal application role,
Core actors, or ordinary application database credentials must not grant:

- vault administration;
- object-version deletion;
- Object Lock bypass;
- legal-hold removal;
- KMS administration;
- replication-policy changes; or
- recovery-account access.

### 6.2 Primary backup account

The primary backup account owns:

- the primary S3 vault in the selected primary backup Region;
- the primary vault customer-managed KMS key;
- the service upload and verification roles;
- the S3 replication role;
- the primary retention-execution role;
- primary replication metrics and alarms; and
- primary catalog and manifest evidence.

The primary backup Region should normally match the Database Backup Service or primary database Region to minimize
latency and transfer cost. This is a policy choice, not an actor-contract field.

### 6.3 Recovery account

The recovery account owns:

- the recovery S3 vault in a distinct approved AWS Region;
- the recovery customer-managed KMS key;
- replica ownership and Object Lock policy;
- an independent recovery-read role;
- tightly controlled legal-hold and post-expiry deletion roles; and
- recovery-region audit and alarms.

The source replication role may write encrypted replicas. It cannot read recovery objects, shorten retention, remove
legal holds, change the recovery key, or delete object versions.

### 6.4 Region selection

The primary and recovery Regions must:

- be distinct AWS Regions;
- satisfy legal, residency, and brokerage-data constraints;
- support all selected S3, Object Lock, KMS, CloudTrail, and replication capabilities;
- have a tested network and operational path from the recovery environment;
- avoid a consciously shared failure dependency where practical; and
- be recorded in the destination configuration and recovery runbook.

Region names are deployment configuration, not embedded in domain event schemas.

### 6.5 One-way replication

Replication is deliberately one-way. Bi-directional replication and replica-modification synchronization are disabled
for the immutable vault path because they increase the chance that an unwanted mutation or configuration error crosses
the recovery boundary.

Delete-marker replication is disabled. Retention deletion is independently planned and executed against each vault
after its own eligibility checks.

## 7. S3 resource topology

### 7.1 Required buckets

Production requires at least:

| Bucket role | Account and Region | Purpose |
| --- | --- | --- |
| Primary immutable vault | Primary backup account, primary Region | First durable AWS copy of artifacts, manifests, commit records, and catalog entries |
| Recovery immutable vault | Recovery account, recovery Region | Cross-account and cross-Region replica used for disaster recovery |
| Security audit log archive | Security or log-archive account | CloudTrail and security evidence independent of application and backup operators |

The security audit bucket is not used for database artifacts. Its ownership and retention follow the organization audit
architecture.

Optional operational buckets, such as S3 Inventory report destinations, must not become authoritative recovery sources.

### 7.2 Mandatory vault settings

Both immutable vault buckets require:

- S3 Versioning enabled and never suspended;
- S3 Object Lock enabled;
- Block Public Access enabled at account and bucket level;
- bucket-owner-enforced Object Ownership with ACLs disabled;
- default SSE-KMS encryption using the bucket's own customer-managed key;
- rejection of plaintext transport;
- rejection of uploads that do not use the approved encryption and key;
- rejection of primary published-object uploads that omit the approved Object Lock mode or fall below the
  policy-calculated retain-until time;
- no public access points;
- no website hosting;
- no Requester Pays on the replication destination;
- CloudTrail management and object data-event coverage;
- replication and inventory visibility;
- lifecycle rules reviewed against Object Lock and recovery objectives; and
- infrastructure drift detection.

The buckets are created with Object Lock as a foundational control. Production does not rely on enabling immutability
after backup objects already exist.

### 7.3 Object Lock policy

Published production artifacts, manifests, run-evidence objects, commit records, catalog entries, verification records,
and recovery records use S3 Object Lock Compliance mode for their declared retention interval.

The policy progression is:

1. development tests publication and cleanup without representing production immutability;
2. pre-production validates Governance mode, retention calculation, legal hold, and cleanup;
3. production enables Compliance mode only after a simulated long-retention error and recovery review; and
4. retention can be extended but never shortened for an already published compliance-protected object version.

Legal hold is independent of time-based retention. It may be applied for incident response, audit, investigation, or
operator-directed preservation. Removing legal hold requires a separate authorized workflow and does not override an
unexpired retention period.

Object Lock protects a specific object version. All manifests therefore record bucket, key, version ID, retention mode,
and retain-until time for every required object version. Because legal hold can change after original publication, the
manifest records its publication-time state while later changes create signed append-only hold records. Recovery always
checks the current S3 legal-hold state as well as that history.

### 7.4 Why the design uses S3 directly

The protected databases are self-managed PostgreSQL and ScyllaDB clusters. Database-native capture and restore semantics
remain necessary even when AWS stores the results. Direct S3 vaults provide:

- stable native artifact storage;
- object-version identity;
- WORM retention;
- cross-account and cross-Region replication;
- explicit KMS boundaries;
- a destination-resident catalog; and
- break-glass access that does not require the IFM application.

AWS Backup may be evaluated as a tertiary copy of eligible S3 data or supporting infrastructure. It does not replace
PostgreSQL WAL/PITR, Scylla cluster completeness, IFM manifests, SystemAdmin state, or restore drills.

## 8. Immutable object namespace

### 8.1 General rules

Every published object key is unique and immutable. The service never overwrites a logical "latest backup" object.
Human-readable database names, credentials, host paths, account numbers, and sensitive strategy identifiers are not
placed in object keys.

The normative logical layout preserves the implemented LocalWorkstation namespace so a catalog can be interpreted by
the same recovery model. AWS adds immutable object-version evidence but does not rename the shared logical paths:

    vault/schema-v1/
      environments/{EnvironmentId}/
        protection-sets/{ProtectionSetId}/
          operations/{OperationId}/
            engines/{EngineId}/
              artifacts/{ManifestId}/{NativeRelativePath}
              ifm-engine-manifest/{ManifestId}.json[.sig]
            publication/{ManifestId}/commit.json[.sig]
        catalog/entries/{yyyyMMdd}/{RestorePointId}/{ManifestId}-{ReplicaId}.json[.sig]
        drill-evidence/{RecoveryOperationId}/final.json[.sig]
        recovery-records/{RecoveryOperationId}/final.json[.sig]
        retention/plans/{RetentionPlanId}/{Revision}.json[.sig]

AWS-only signed evidence may extend an operation with exact object-version, verification, run-evidence, replication,
and coordinated-backup-set records. Those additions reference the common manifest/catalog identities above and cannot
replace or rename them. Database-native manifests remain inside the artifact tree at their native relative paths.

Identifiers are opaque and stable. The manifest contains the bounded descriptive metadata needed to interpret them.
Run evidence contains the final structured `DatabaseRecoveryRunStatistics` summary for successful, failed, or cancelled work
when enough evidence exists to publish it safely. Only eligible completed recovery points are referenced by catalog
entries.

The `schema-v1` path segment versions the object namespace, not the IFM engine-manifest payload. A manifest stored
beneath this namespace is currently `DatabaseBackupManifest.SchemaVersion = 2`; the two version numbers are independent.

### 8.2 Object identity

An artifact replica identity includes:

- destination identity;
- AWS partition, account identity, and Region;
- bucket;
- object key;
- S3 version ID;
- content length;
- native checksum where available;
- IFM cryptographic content digest;
- S3 checksum algorithm and value;
- SSE-KMS key ARN;
- Object Lock mode and retain-until time;
- legal-hold state;
- storage class;
- replication status; and
- publication and verification revisions.

The ETag is recorded for diagnostics but is never treated as a universal content checksum.

### 8.3 Multipart uploads

Large artifacts use bounded parallel multipart upload. The service:

- journals upload ID, object key, part size, completed part numbers, and checksums;
- uses consecutive part numbers;
- supplies supported S3 checksums;
- independently calculates the manifest's cryptographic content digest;
- resumes only when the journal, remote upload, object identity, and policy revision match;
- aborts abandoned multipart uploads after the diagnostic window; and
- does not publish a manifest until the completed object passes a HEAD and checksum check.

Concurrency is limited by configured memory, network, disk, and KMS request budgets. The independent Database Backup
Host enforces those bounds without consuming **Api.Server** process resources.

### 8.4 Atomic publication in S3

S3 has no directory rename transaction, so visibility is established through immutable records:

1. Upload every artifact to its final unique key.
2. Complete multipart uploads and verify length, version ID, encryption, Object Lock, and checksum.
3. Upload and verify the database-native manifest.
4. Upload the signed IFM engine manifest.
5. For a coordinated set, upload the signed backup-set manifest referencing every engine manifest.
6. Upload a signed publication commit record as the last operation object.
7. Upload an append-only catalog entry referencing the commit record.
8. Report the replica as published to SystemAdmin only after the applicable publication policy is satisfied.

A restore point is visible only when a valid commit record and catalog entry refer to a complete, verifiable manifest
chain. Listing an artifact prefix is never sufficient.

S3's strong consistency permits the commit and catalog objects to be read and listed after their successful writes, but
the service still validates explicit version IDs and signatures rather than relying on timing.

### 8.5 Manifest signing

The IFM engine manifest, backup-set manifest, publication commit, catalog entry, and break-glass recovery record are
cryptographically signed.

The production signing design uses an AWS KMS asymmetric signing key controlled by the backup security boundary. Its
public-key trust material, algorithm, key identity, and validity period are included in the recovery trust bundle. The
public key is stored in the recovery vault and in an independently controlled offline recovery package so signature
verification does not require a live call to the failed Core environment.

Signing authorization is separate from artifact upload. The signing role signs a digest only after policy validation
confirms artifact identity, checksum evidence, retention, and encryption.

### 8.6 Implemented manifest schema v2 baseline

The AWS engine manifest is the shared `DatabaseBackupManifest` schema version 2 with `Source = AwsCloud`. Its portable,
signed fields remain identical to LocalWorkstation:

| Field group | Required meaning |
| --- | --- |
| Identity | `ManifestId`, `OperationId`, `RestorePointId`, `Source`, `Engine`, and `ProtectionSetId` |
| Boundary | bounded `SafeBoundaryReference`, UTC `CreatedUtc`, and positive `Revision` |
| Restore graph | `Dependencies` containing only the direct PostgreSQL incremental parent when applicable |
| Portable artifacts | relative path, byte length, and SHA-256 digest for every native artifact |
| Publication | logical `DatabaseArtifactReplicaId` values and bounded run statistics |
| Lineage | requested/resolved mode, native kind, base restore point, parent restore point, chain depth, and bounded native identity |

Version-2 validation is source-neutral except for the concrete source value:

- a full manifest identifies itself as `BaseRestorePointId`, has no parent, has depth zero, and has no restore
  dependency;
- a PostgreSQL incremental uses `PostgreSqlIncremental`, has a base and direct parent, has positive depth, and lists
  exactly that direct parent in `Dependencies`;
- a Scylla full uses `ScyllaManagerSnapshot`;
- a Scylla incremental request resolved by Manager uses `ScyllaManagerDeduplicatedSnapshot`, records base/parent/depth
  lineage for selection and audit, and keeps `Dependencies` empty because the selected Manager restore point is
  logically complete; and
- a version-1 manifest is normalized as a legacy full backup when read.

AWS destination binding is a signed, destination-specific publication layer around this common manifest. The AWS
commit/catalog evidence records bucket, key, S3 version ID, account/Region, length, checksums, KMS key, Object Lock,
legal hold, storage class, and replication status. These values are deliberately not placed in
`DatabaseBackupLineage`, actor messages, or `SystemAdminDbContext`; bounded `DatabaseArtifactReplicaDescriptor` events
carry only the logical replica, state, safe destination reference, engine, artifact identity, and optional byte count.

The current `LocalBackupManifestStore` is a LocalWorkstation adapter and correctly rejects a non-local source. The AWS
adapter must implement the same shared schema-v2 invariants while requiring `AwsCloud`; it must not weaken the validator
or serialize the local-source restriction into the common contract.

## 9. IAM and separation of duties

### 9.1 Identity principles

- Workloads use temporary role credentials.
- Static AWS access keys are prohibited in source code, configuration files, actor events, manifests, logs, and UI.
- Role sessions include operation and environment context through approved session tags where supported.
- Role trust is limited to the expected workload identity and AWS organization.
- Permissions are scoped to exact bucket prefixes, KMS encryption context, Region, and operation category.
- Human administration uses federated identity, strong MFA, and separately approved break-glass access.
- The ability to create a backup does not imply the ability to delete, restore, release legal hold, or administer keys.

### 9.2 Service roles

| Role | Required capability | Explicit exclusions |
| --- | --- | --- |
| Vault upload role | Create multipart uploads, upload parts, complete or abort owned uploads, write unique approved prefixes, read required object metadata | No object-version deletion, retention bypass, legal-hold removal, bucket administration, or arbitrary reads |
| Verification role | Read exact artifact versions and checksum metadata for approved operations | No write, delete, retention, replication, or key administration |
| Manifest signing role | Sign approved manifest digests | No S3 artifact access or KMS decryption |
| Restore-read role | List catalog prefixes and read exact published versions for an approved restore | No write, delete, retention, or bucket administration |
| Retention-planning role | List versions, manifests, Object Lock state, legal holds, storage class, and dependency evidence | No delete or retention mutation |
| Retention-execution role | Delete only exact expired versions in an approved revision-matched plan | No governance bypass, policy change, wildcard sweep, or KMS administration |
| Legal-hold role | Apply or release legal hold under a separately audited workflow | No artifact read, delete, or key administration |
| Replication role | Replicate source versions, encryption, and Object Lock metadata to the destination | No human assumption, source deletion, destination read, or destination administration |
| Break-glass recovery role | Read catalog, manifests, exact artifact versions, and decrypt through the recovery key | No write, delete, retention modification, or routine application use |

Where one AWS API technically requires additional KMS permissions for multipart processing, those permissions are
constrained through KMS ViaService, encryption context, bucket ARN, caller identity, and resource policy. They do not
grant S3 GetObject.

### 9.3 Administrative roles

Bucket administration, KMS administration, security audit, and recovery operation are separate duties. Production
policy changes require peer review and deployment through approved infrastructure change control.

No routine role receives **s3:BypassGovernanceRetention**. Production Compliance mode does not provide a bypass even to
the account root user during the retention period.

AWS Organizations service-control policies should deny or tightly constrain:

- disabling or deleting production vault keys;
- suspending S3 Versioning;
- changing Object Lock configuration;
- disabling Block Public Access;
- making vault buckets public;
- weakening CloudTrail;
- changing replication outside approved infrastructure roles; and
- leaving the organization with a vault account.

### 9.4 Core Actor Host exclusion

The Core Actor Host has no IAM policy for S3 vault objects, KMS backup keys, replication, CloudTrail, or retention. Actor
messages contain logical destination IDs such as **AwsPrimary** and **AwsRecovery**, never bucket names, role ARNs,
credentials, presigned URLs, or KMS key IDs.

## 10. Encryption and key recovery

### 10.1 Data in transit

All AWS API traffic uses TLS. Bucket policies reject non-secure transport. Native database and service-agent traffic uses
authenticated encryption where the engine path supports it. Staging storage is encrypted independently from S3.

### 10.2 Data at rest

Every vault object is encrypted using SSE-KMS and the customer-managed regional key owned by that vault account.
S3 Bucket Keys are enabled where compatibility tests confirm correct encryption context and replication behavior, to
reduce KMS request volume and cost.

Cross-Region Replication decrypts with the primary key and encrypts the destination replica with the recovery account's
key. AWS managed KMS keys are not used for cross-account replicas.

### 10.3 Independent regional keys

The reference design chooses independent single-Region keys rather than a shared multi-Region key. This provides:

- independent key administration in each vault account;
- separate recovery blast radius;
- explicit proof that the recovery account can decrypt its replica without the primary key; and
- reduced risk that a synchronized key-policy error affects both replicas.

The cost is that replica encryption must be configured explicitly and manifests must record the key used by each
replica. This tradeoff is accepted.

### 10.4 Key policy

Key policies separate:

- key administrators, who cannot decrypt backup data;
- upload and replication users;
- verification and restore users;
- signing users, on a separate asymmetric key; and
- security auditors.

Cryptographic permissions are restricted to S3 use, the exact vault bucket encryption context, approved roles, and
approved accounts.

### 10.5 Rotation and deletion

Automatic key rotation is enabled where supported and validated. Rotation must preserve decryption of objects encrypted
under older key material.

Scheduling deletion of a production vault key is denied to ordinary administrators. If policy permits it at all, it
requires an exceptional security role, peer approval, the maximum practical waiting period, immediate alerting, and
proof that no retained object version depends on the key.

Disabling a key also triggers a critical recovery-readiness alert. A backup whose required key is disabled, pending
deletion, or inaccessible is not reported as recovery-ready.

### 10.6 Recovery trust bundle

An independently controlled recovery trust bundle contains:

- vault account IDs and Regions;
- bucket identities and expected ownership;
- KMS key ARNs and aliases;
- manifest signing public keys and trust history;
- supported manifest schemas;
- approved recovery-role identities;
- database engine and native-tool compatibility matrix;
- infrastructure templates for fresh targets;
- recovery tooling verification hashes; and
- escalation and authorization procedure.

The bundle contains no long-lived secret key. It is retained in at least one location outside the protected IFM
databases and is reviewed during every restore drill.

## 11. Network architecture

### 11.1 AWS-hosted Database Backup Service

When the Database Backup Service runs inside an AWS VPC, S3 traffic uses a controlled S3 gateway endpoint where
practical. STS, KMS, CloudWatch, and other required AWS service access uses approved private endpoints or controlled
egress.

Endpoint and bucket policies restrict access to expected roles and resources. A VPC-endpoint-only bucket policy must
include a tested break-glass exception; otherwise the same policy intended to improve security can prevent recovery when
the normal VPC is unavailable.

### 11.2 Service outside AWS

If IFM runs outside AWS:

- the service uses an approved workload-federation mechanism such as IAM Roles Anywhere or another reviewed identity
  provider to obtain temporary credentials;
- traffic uses TLS to regional S3 endpoints;
- network paths, bandwidth, retry behavior, and egress controls are tested under full backup and restore load; and
- long-lived IAM user access keys remain prohibited.

### 11.3 Database nodes

PostgreSQL and ScyllaDB nodes do not require network access to S3, KMS, or STS in the reference architecture. They
communicate only with the Database Backup Service or its service-controlled native agent path.

This constraint intentionally rejects the default Scylla Manager direct-to-S3 credential pattern for IFM production.
Changing to direct agent uploads would require revising the parent credential boundary, defining per-agent temporary
identity, and repeating the security review.

## 12. PostgreSQL AWS backup design

The AwsCloud processor consumes the shared typed PostgreSQL capability for preflight, physical backup, status,
verification, WAL recovery-range management, fresh-target restore, validation, cancellation, and reconciliation.
PostgreSQL has no general backup REST API: the adapter privately uses the PostgreSQL replication protocol and supported
native utilities such as `pg_basebackup` and `pg_verifybackup`. Actor messages cannot select tools, arguments,
credentials, or host paths.

### 12.1 Recovery model

PostgreSQL protection consists of:

- periodic physical full or incremental backups of the complete PostgreSQL cluster;
- continuous WAL archiving;
- immutable native and IFM manifests;
- checksum and native backup verification;
- cross-account and cross-Region replication; and
- recurring fresh-target PITR drills.

The shared request modes and implemented planner rules are binding:

- `Full` always begins a new chain;
- `Automatic` selects the newest verified, content-equivalent parent present on every required logical replica and
  falls back to full when no eligible common parent exists;
- explicit `Incremental` uses the same eligibility checks but fails rather than silently changing mode;
- chain depth and base age are bounded by source configuration; the initial AWS defaults match the validated local
  defaults of six incremental descendants and a seven-day maximum base age; and
- the requested and resolved mode are both retained in lineage, events, manifest v2, catalog interpretation, and
  projections.

PostgreSQL incremental capture requires source PostgreSQL 17 or later, matching PostgreSQL 17-or-later native tools,
`summarize_wal=on`, retained WAL summaries, a parent from the same database system identifier, the parent native
manifest, and `pg_combinebackup`. Failure of any precondition applies the `Automatic` fallback/explicit
`Incremental` failure rule above.

### 12.2 Full or incremental backup workflow

1. SystemAdmin commits **DatabaseBackupExecutionRequestedEvent** for **CorePostgresCluster**.
2. The Database Backup Service admits and journals the operation.
3. The service reads the requested mode carried in the execution event's `BackupLineage` (originating from command
   `RequestedBackupMode`), enumerates verified catalog entries on every required replica, and resolves one
   `DatabaseBackupLineage` before native capture.
4. The service validates the replication identity, database system identifier, PostgreSQL/tool versions, incremental
   prerequisites where selected, destination readiness, staging capacity, policy revision, active lease, and WAL
   archive health.
5. Full capture uses the approved physical base-backup command. Incremental capture invokes
   `pg_basebackup --incremental=<direct-parent-backup_manifest>` through the allowlisted native adapter.
6. Output is streamed through bounded encrypted staging or directly into bounded service-owned upload buffers.
7. The service preserves PostgreSQL's native backup manifest and calculates IFM artifact digests.
8. Artifacts are uploaded under unique immutable S3 keys.
9. Native verification validates the selected full or incremental backup.
10. The IFM engine manifest records cluster identity, system identifier, engine version, timeline, start and end LSN,
   start and end time, native tool version, tablespace mapping, artifact versions, checksums, and WAL dependency.
   It also stores the exact shared lineage and, for PostgreSQL incrementals, the direct parent in `Dependencies`.
11. The service publishes the engine commit and catalog evidence only after policy-required verification.
12. Replication state is tracked per object version and reported as replica lifecycle events.

The actor never starts or waits for the native utility. Process output remains service telemetry and a bounded artifact
log.

### 12.3 Continuous WAL workflow

The PostgreSQL archiver sends each completed WAL segment to an authenticated, allowlisted service ingress or
service-controlled agent. That component does not have AWS credentials.

The service:

- validates timeline and segment identity;
- rejects a conflicting second payload for the same immutable WAL identity;
- persists a transfer journal;
- uploads the segment under a unique deterministic WAL key;
- verifies length, S3 version, encryption, retention, and checksum;
- publishes bounded WAL archive health and watermark observations; and
- retains WAL according to every dependent base backup and PITR policy.

The normal archive acknowledgement is issued only after the segment is durably present and verified in the primary AWS
vault. A policy may introduce a redundant durable local spool, but acknowledging only local persistence weakens the AWS
RPO and must be represented explicitly rather than hidden.

An AWS outage therefore produces WAL backpressure. The service alerts on:

- last successfully archived WAL time;
- local queue depth and bytes;
- PostgreSQL WAL filesystem headroom;
- primary upload failures;
- replication lag to the recovery vault; and
- the earliest PITR gap.

The system must prefer a visible risk and controlled operational response over silently recycling WAL that has no
durable recovery copy.

### 12.4 PostgreSQL restore eligibility

A PostgreSQL restore point is eligible only when:

- its base or incremental chain is complete;
- the native backup manifest is valid;
- required WAL ranges are present for the selected target;
- every referenced object has an exact bucket, key, and version ID;
- checksums, encryption key, Object Lock, and signature evidence validate;
- PostgreSQL major-version and tool compatibility is supported;
- tablespace and filesystem requirements are declared;
- the chosen replica can meet the requested recovery class; and
- policy-required verification or restore testing is current.

### 12.5 PostgreSQL restore workflow

The service restores to a fresh encrypted volume and compatible PostgreSQL runtime:

1. Select an exact restore point, target time, target LSN, or named target.
2. Resolve the full base, incremental, and WAL dependency chain from signed manifests.
3. Select and validate a primary or recovery replica explicitly.
4. Retrieve or rehydrate archived S3 objects into controlled encrypted staging.
5. Verify every downloaded object's S3 and IFM checksums.
6. For an incremental target, stage the complete chain oldest-first, verify every signed manifest and native artifact,
   and use the matching `pg_combinebackup` to produce a synthetic full backup. Verify that combined backup before boot.
7. Restore the cluster files with correct ownership, permissions, and tablespace mapping.
8. Configure recovery to read the verified WAL staging path.
9. Start PostgreSQL in isolated recovery networking.
10. Confirm the intended recovery target and timeline.
11. Run native catalog, extension, role, event-store, sequence, and application checkpoint validation.
12. Publish **DatabaseRestoreValidationCompletedEvent**.
13. For production recovery, stop at **DatabaseRestoreReadyForCutoverEvent**.

No restore writes over an active production PostgreSQL data volume.

## 13. ScyllaDB AWS backup design

The AwsCloud processor consumes the shared typed ScyllaDB capability for preflight, start/status/cancel cluster backup,
schema-first restore, start/status/cancel data restore, node/token coverage validation, and reconciliation. The adapter
prefers the Scylla Manager REST/Swagger API. Any required `sctool` fallback stays behind the same allowlist and never
becomes an actor, UI, or Console contract.

### 13.1 Recovery model

ScyllaDB protection consists of:

- Scylla Manager coordination for multi-node production capture;
- complete declared-node and token-range coverage;
- schema capture;
- logically complete snapshot/SSTable artifact capture with Manager/object-store physical deduplication;
- preservation of Scylla native manifests;
- IFM cluster manifest and application checkpoint;
- immutable AWS replicas; and
- recurring fresh-cluster restore drills.

A cluster backup is incomplete when any required live node, keyspace, table, token range, schema artifact, or native
manifest required by the policy is missing.

Scylla uses the same `Full | Automatic | Incremental` request surface as PostgreSQL, but its restore graph is different.
`Full` resolves to `ScyllaManagerSnapshot`. An eligible `Automatic` or explicit `Incremental` resolves to
`ScyllaManagerDeduplicatedSnapshot`; base, parent, and depth are retained as lineage, while manifest `Dependencies`
remains empty. Scylla Manager restore points are logically complete and Manager/object storage owns their physical
deduplication. An AWS adapter must not manufacture a client-side SSTable dependency chain or copy unchanged SSTables
merely to make an IFM "full" directory.

### 13.2 Credential-preserving transfer model

Scylla Manager supports S3 directly, but its standard direct path places S3 credentials or equivalent access on
Manager agents. The approved IFM overview allows AWS destination credentials only in the Database Backup Service.

The reference architecture therefore uses:

1. Scylla Manager for native cluster coordination;
2. an encrypted service-controlled shared staging target exposed only to the allowlisted native backup path;
3. no AWS credential on Scylla nodes or Manager agents;
4. Database Backup Service upload from staging to immutable S3 keys; and
5. cleanup of staging only after AWS verification and journaled publication state permit it.

The staging mechanism must preserve Scylla Manager's expected directory layout and native manifests. It is bounded,
capacity-reserved, monitored, and not an authoritative long-term destination.

This choice adds staging I/O but preserves the approved trust boundary. A future direct-to-S3 optimization is a new
architecture decision, not an implementation detail.

### 13.3 Cluster-backup workflow

1. SystemAdmin commits **DatabaseBackupExecutionRequestedEvent** for **CoreScyllaCluster**.
2. The Database Backup Service admits and journals the operation.
3. The service validates Scylla Manager readiness, required nodes, token coverage, schema credentials, staging capacity,
   destination readiness, active repair conflicts, and policy revision.
4. Scylla Manager establishes coordinated snapshots and pauses conflicting topology movement as required.
5. Scylla Manager writes schema, SSTables, and native manifests to controlled staging.
6. The service checks every required node and protection-set component against the native manifest.
7. The service uploads artifacts with bounded multipart concurrency.
8. The service writes an IFM Scylla engine manifest containing cluster ID, topology, datacenters, nodes, token coverage,
   keyspaces, tables, schema identity, native snapshot tag, Scylla and Manager versions, artifact identities, checksums,
   application checkpoint, and the common lineage fields. Its `Dependencies` array is empty.
9. Verification validates the native and IFM manifests and object versions.
10. The service publishes the commit and append-only catalog entry.
11. Staging cleanup occurs only after the required AWS replica state and diagnostic policy allow it.

Scylla Manager automatic retention against the AWS source of truth is subordinated to the IFM approved retention plan.
The initial retention setting matches the validated LocalWorkstation default of 30 Manager snapshots. Native tooling
must not purge a restore point still protected by IFM retention, legal hold, active work, or replica policy.

### 13.4 Scylla restore eligibility

A Scylla restore point is eligible only when:

- schema and data manifests are complete;
- every required node and token range is accounted for;
- the selected Manager restore point is logically complete and all native objects referenced by that restore point
  exist on the selected replica;
- destination topology mapping is explicit;
- the target Scylla version is compatible;
- the selected backup set's application checkpoint is compatible with PostgreSQL where coordinated recovery is needed;
- checksums, signatures, encryption keys, retention, and exact S3 versions validate; and
- the chosen storage class can meet the requested recovery class.

### 13.5 Scylla restore workflow

The service restores to a fresh compatible cluster:

1. Resolve and validate the selected signed IFM manifest, its Scylla native manifest, and the logically complete Manager
   restore point. Lineage is audit/selection evidence, not an IFM artifact dependency chain.
2. Select the primary or recovery replica explicitly.
3. Rehydrate archived objects when needed.
4. Download exact S3 versions into fresh encrypted service-controlled staging.
5. Reconstruct the Scylla Manager-compatible layout and verify every artifact.
6. Validate fresh-cluster topology, capacity, version, and datacenter mapping.
7. Restore schema before table data.
8. Restore SSTables through the approved Scylla Manager method.
9. Run native cluster health, ownership, schema agreement, and data validation.
10. Rebuild excluded materialized views and secondary indexes from their base tables.
11. Reconcile event-derived and externally recoverable data according to classification.
12. Run application checkpoint and cross-engine validation.
13. Publish validation results and stop at **ReadyForCutover** for production.

No restore copies files blindly into active ScyllaDB node directories.

## 14. Coordinated PostgreSQL and ScyllaDB backup sets

The shared `DatabaseConsistencyMode` values are `EngineConsistent` and `CoordinatedProtectionSet`. AWS does not add an
`ApplicationCheckpoint` enum value. For a multi-engine set, the request uses `CoordinatedProtectionSet`; SystemAdmin
records an application checkpoint as the bounded consistency reference correlated with both engine operations without
pausing the entire application unless testing proves quiescence necessary.

A coordinated set manifest contains:

- BackupSetId and policy revision;
- environment and protection-set identities;
- application event or ingestion checkpoint;
- PostgreSQL engine-manifest identity and recovery boundary;
- Scylla engine-manifest identity and recovery boundary;
- consistency mode;
- known replay or reconciliation requirements;
- JetStream checkpoint relationship where applicable;
- required destinations and replica results;
- validation and restore-test history; and
- signature and publication record.

The coordinated set is not eligible until each required engine restore point is independently eligible and their
checkpoint compatibility has been validated.

## 15. Cross-Region and cross-account replication

### 15.1 Replication configuration

The primary vault replicates all published recovery prefixes to the recovery vault. Both buckets have Versioning and
Object Lock enabled. Replication includes:

- artifact object versions;
- native manifests;
- IFM manifests;
- Object Lock retention metadata;
- legal-hold state;
- publication commit records;
- catalog entries; and
- run-evidence, verification, and recovery records required by policy.

SSE-KMS replication explicitly selects the destination customer-managed key. The destination uses bucket-owner-enforced
ownership so the recovery account owns the replica.

The source replication role has only the source-key decrypt and destination-key encrypt permissions that S3 replication
requires. The recovery key policy grants that role through the S3 service and expected encryption context without
granting it general recovery-vault read access.

### 15.2 Replication completion

The service checks replication status for each required source object version. It records **Pending**, **Completed**, or
**Failed** at the replica level and verifies the destination object's ownership, encryption key, retention, version,
length, and checksum evidence.

S3 Inventory is used for broad reconciliation and audit, not real-time publication. Failed or previously unreplicated
versions use a controlled S3 Batch Replication repair plan.

### 15.3 S3 Replication Time Control

S3 Replication Time Control is enabled for policy classes whose cross-Region replica objective requires its supported
replication-time commitment and metrics. Large backup objects are still monitored individually because object size,
Region pair, KMS policy, and service conditions affect replication time.

RTC threshold events and replication metrics enter the Backup Service observability path. Missing the threshold changes
recovery-readiness state and alerts; it does not corrupt the valid primary replica.

### 15.4 Replica success policy

Each policy declares one of:

- **PrimaryRequired**: primary AWS vault is required for backup completion; recovery replica may complete asynchronously;
- **PrimaryAndRecoveryRequired**: both replicas must be complete before the backup set is policy-compliant; or
- **DualDestinationRequired**: AWS and an approved local replica must meet the shared policy.

For production disaster-recovery classes, **PrimaryAndRecoveryRequired** is the default compliance state. SystemAdmin may
record the engine backup as captured while still reporting the backup set as destination-incomplete and outside its
recovery objective.

## 16. AWS restore-source selection

Restore source selection is explicit and auditable:

1. discover candidate restore points from the destination-resident catalog;
2. validate catalog and manifest signatures;
3. validate exact object versions and dependencies;
4. verify the destination account, Region, ownership, KMS access, retention, storage class, and checksums;
5. compare measured or forecast retrieval time with the requested recovery class;
6. prefer the primary vault only when it is healthy and trusted;
7. select the recovery vault when the primary Region, account, key, or evidence is unavailable or suspect; and
8. record the selected replica and reason in the restore operation.

The service never silently combines arbitrary objects from two replicas. If a dependency chain is repaired from mixed
replicas, a new signed recovery plan identifies every source version and must pass full verification before use.

## 17. Break-glass disaster recovery

### 17.1 Trigger conditions

Break-glass recovery is permitted when normal SystemAdmin authorization cannot operate because Core, NATS, or the
protected databases are unavailable. It is not a shortcut for routine restore approval.

### 17.2 Independent prerequisites

The recovery team requires:

- independently federated AWS access with phishing-resistant MFA;
- access to the recovery account without the IFM application identity provider being the sole dependency;
- the recovery trust bundle;
- signed recovery tooling;
- fresh-target infrastructure templates;
- approved PostgreSQL and ScyllaDB native tools;
- adequate staging capacity and network access;
- audit logging outside IFM; and
- an incident or change record authorizing recovery.

No permanent AWS access key is stored as an emergency credential.

### 17.3 Break-glass sequence

1. Establish incident authority and two-person approval.
2. Assume the recovery-read role with a short session.
3. Verify account, Region, bucket, KMS key, CloudTrail, and recovery trust bundle.
4. Enumerate immutable catalog entries.
5. Validate signatures and reconstruct the catalog from manifests if the index is suspect.
6. Select an exact coordinated backup set and recovery target.
7. Resolve the PostgreSQL dependency/WAL closure and every native object referenced by the selected logically complete
   Scylla Manager restore point.
8. Retrieve or rehydrate exact object versions.
9. Validate checksums before native tools consume the artifacts.
10. Provision fresh isolated database targets.
11. Restore PostgreSQL and ScyllaDB using the normal native boundaries.
12. Run minimum native and application validation.
13. Start the recovered Core Actor Host only after validation.
14. Import a signed break-glass recovery record into SystemAdmin after it becomes available.
15. Perform a full post-recovery security and consistency review before production cutover.

### 17.4 Audit record

The signed break-glass record includes:

- recovery operation ID;
- incident/change authorization;
- human and role-session identities;
- selected catalog, manifest, bucket, key, and object-version identities;
- target infrastructure identity;
- timestamps and measured RPO/RTO;
- verification outcomes;
- deviations and errors;
- cutover authorization; and
- hashes of external audit evidence.

## 18. Lifecycle, retention, and legal hold

### 18.1 Retention source of truth

SystemAdmin owns the approved retention policy and operation intent. The Database Backup Service evaluates concrete AWS
dependencies and produces **DatabaseRetentionPlanCreatedEvent**. No S3 Lifecycle expiration rule independently decides
which PostgreSQL base, WAL, Scylla SSTable, manifest, or coordinated set is safe to delete.

### 18.2 Storage-class lifecycle

Lifecycle transitions may change storage class after publication because they do not change manifest identity. Policy
maps recovery classes to storage:

| Recovery class | Default storage direction |
| --- | --- |
| Active operational recovery | S3 Standard, Intelligent-Tiering without archive tiers, Standard-IA, or Glacier Instant Retrieval |
| Warm historical recovery | Glacier Instant Retrieval or Flexible Retrieval when measured retrieval time is acceptable |
| Long-term archive | Glacier Flexible Retrieval or Deep Archive only when delayed recovery is explicitly accepted |
| Catalog and current manifests | Immediately readable storage; never dependent on archive retrieval merely to discover restore points |

Archived objects are not immediately readable. A restore operation must represent archive retrieval as a visible phase,
track temporary restored-copy expiry, and include retrieval delay in RTO.

Small manifests and catalog entries remain in an immediately accessible storage class. Large native artifacts may
transition only after minimum-duration charges, retrieval cost, and restore objectives have been modeled.

No S3 Lifecycle expiration rule applies to published recovery prefixes. Lifecycle is limited to approved storage-class
transitions and cleanup of abandoned multipart uploads or explicitly non-published diagnostic prefixes.

### 18.3 Dependency-safe expiration

Retention planning protects:

- every base backup referenced by a retained incremental or PITR point;
- every required PostgreSQL WAL segment;
- every Scylla schema, snapshot, SSTable, and native object referenced by a retained logically complete Manager restore
  point; Scylla lineage alone does not create transitive IFM dependencies;
- every object referenced by an active backup, restore, verification, or drill;
- every legal-held object version;
- the latest successful restore-tested set;
- required primary and recovery replicas independently; and
- all publication, catalog, verification, and signature evidence needed to interpret retained artifacts.

### 18.4 Deletion execution

Deletion is a two-step operation:

1. create a signed, immutable plan containing exact bucket, key, version ID, dependency proof, retention expiry, legal
   hold state, and expected policy revision;
2. after actor approval, assume the separate deletion role and revalidate every entry immediately before deletion.

The executor stops on a policy-revision mismatch, active dependency, legal hold, unexpired Object Lock, unknown version,
replication uncertainty, or manifest inconsistency. It never performs recursive prefix deletion.

Source and recovery replicas are deleted under independent plans. Deleting a source object does not implicitly authorize
deleting the recovery replica.

### 18.5 Incomplete operations

Incomplete multipart uploads, uncommitted artifact versions, failed staging objects, and diagnostic logs follow a
separate cleanup policy. No cleanup rule may match published prefixes without exact object-state validation.

## 19. Verification and restore drills

### 19.1 Verification levels

AWS verification adds destination evidence to the shared levels:

1. **Upload verification**: S3 accepted the expected checksum and object length.
2. **Object identity verification**: exact version, ownership, encryption key, Object Lock, storage class, and metadata
   match the manifest.
3. **Replication verification**: recovery replica exists under the recovery owner and recovery KMS key.
4. **Cryptographic verification**: downloaded content digest matches the immutable manifest.
5. **Native verification**: PostgreSQL and Scylla native manifests and tools accept the artifact set.
6. **Engine restore verification**: a fresh database target restores and starts.
7. **Application verification**: event store, schemas, projections, sequence infrastructure, data classification, and
   application checkpoints pass.
8. **Coordinated-set verification**: PostgreSQL and Scylla state is compatible at the selected checkpoint.

Only levels explicitly completed are reported. Replicated does not mean verified; verified does not mean restore-tested.

### 19.2 Drill schedule

Production policy schedules:

- frequent metadata, checksum, KMS-access, and replication checks;
- periodic PostgreSQL PITR drills using randomly selected valid target times;
- periodic Scylla fresh-cluster restore drills;
- coordinated full-application restore drills;
- recovery-vault-only drills that deny access to the primary vault;
- key and identity failure drills;
- archived-object retrieval drills; and
- at least one exercise that assumes Core and NATS are unavailable.

### 19.3 Drill evidence

A drill records:

- requested and actual recovery point;
- capture age and data-loss window;
- catalog discovery time;
- archive retrieval time;
- artifact transfer time;
- native restore time;
- application validation time;
- total RTO;
- replica, Region, account, storage class, and KMS key used;
- throughput, cost estimate, and bottlenecks;
- validation failures and manual intervention; and
- corrective actions with owner and deadline.

Recovery objectives remain unproven until representative drills succeed.

## 20. Catalog and recovery discovery

### 20.1 Authoritative evidence

Immutable manifests and commit records are authoritative destination evidence. Catalog entries are append-only indexes
over that evidence. S3 Inventory is an audit and reconciliation aid. SystemAdmin read models are the normal application
view but are not required for disaster recovery.

### 20.2 Catalog reconstruction

Recovery tooling can:

1. list publication commit records under a versioned environment prefix;
2. validate their signatures;
3. resolve engine and backup-set manifests;
4. validate referenced exact object versions;
5. rebuild restore-point and dependency indexes; and
6. compare the rebuilt index with catalog entries and S3 Inventory.

No mutable global catalog file is required. An optional generated index may improve speed, but it is disposable and
cannot make an otherwise invalid restore point eligible.

### 20.3 Catalog privacy

Catalog and manifest contents are encrypted at rest. They use opaque identifiers in keys and omit secrets, raw
connection strings, credentials, trading logic, and unnecessary row-level information. Authorized recovery operators
can still determine engine version, topology, dependency chain, and recovery eligibility.

## 21. SystemAdmin and service integration

### 21.1 Actor contracts remain destination-neutral

The command, query, and event contracts from the overview are unchanged. A policy identifies a logical destination and
required replica class. AWS account IDs, bucket names, KMS keys, role ARNs, endpoints, and network settings remain typed
Database Backup Service configuration.

Every AWS DatabaseBackup domain event, execution-intent event, service event, and translated service-event command
carries BackupSource **AwsCloud**. The event type and payload schema are the same ones used by LocalWorkstation
processing. Logical replicas such as **AwsPrimary** and **AwsRecovery** do not replace BackupSource.

UI, Database Backup Console, and SystemAdmin ScheduledTask callers invoke the same source-scoped commands and queries.
RequestOrigin records which caller path initiated the contract; it does not select the AWS processor.
BackupSource **None** is allowed only for unselected/default state or an explicit all-sources query and is rejected for
accepted operations and source-bound events.

The implemented wire-schema baseline is normative for AwsCloud:

| Contract | Existing field/key used by AWS | Rule |
| --- | --- | --- |
| `DatabaseRequestEnvelope` | keys 0-9, contract version 1 | Reuse unchanged; caller identity, authorization, origin, correlation/causation, environment, and UTC creation time remain mandatory |
| `RequestDatabaseBackupCommand` through `DatabaseBackupCommand` | `RequestedBackupMode`, MessagePack key 31 | `None` is normalized to the legacy full request before execution; no AWS mode field is added |
| `DatabaseBackupExecutionRequestedEvent` through `DatabaseBackupEventContract` | `BackupLineage`, MessagePack key 31 | Initially carries requested mode; AWS host returns the resolved lineage on bounded service events |
| Service and domain events | the same `BackupLineage`, source envelope, replica descriptor, statistics, manifest revision, and restore-point fields | Event type names and MessagePack keys remain source-neutral |
| `DatabaseBackupOperationReadModel` | `BackupLineage`, key 12 | UI/Console see requested and resolved mode without AWS-specific models |
| `DatabaseRestorePointReadModel` | `BackupLineage`, key 11 | Catalog lineage is projected through domain events, never read directly from S3 by clients |

`DatabaseBackupLineage` itself retains the implemented key layout: requested mode 0, resolved mode 1, native kind 2,
base restore point 3, parent restore point 4, chain depth 5, and bounded native identity 6. Enum numeric values are
also stable: modes `Automatic=1`, `Full=2`, `Incremental=3`; native kinds `PostgreSqlBase=1`,
`PostgreSqlIncremental=2`, `ScyllaManagerSnapshot=3`, and `ScyllaManagerDeduplicatedSnapshot=4`.

The transport route and durable acknowledgement boundaries are identical to LocalWorkstation: public Core NATS
request/reply to actors; Command Actor execution intent through JetStream; host journal admission before acknowledgement;
host outbox service events through JetStream; Event Actor translation; Command Actor durable append; then public domain
events to projections and authorized UI/Console listeners. AWS multipart parts, S3 objects, WAL segments, and Scylla
components remain private journal/telemetry details rather than messages.

### 21.2 AWS-related service observations

The service uses existing event families to report AWS state:

| AWS observation | Service event |
| --- | --- |
| Primary object or manifest reached a meaningful lifecycle boundary | **DatabaseBackupArtifactReplicaUpdatedEvent** |
| Recovery replication completed, failed, or exceeded policy threshold | **DatabaseBackupArtifactReplicaUpdatedEvent** or bounded **DatabaseBackupServiceErrorEvent** |
| Object, checksum, signature, encryption, or lock verification completed | **DatabaseBackupVerificationCompletedEvent** |
| Required KMS key, vault, or replication capability changed | **DatabaseBackupServiceCapabilityChangedEvent** |
| Retention plan derived from exact AWS versions | **DatabaseRetentionPlanCreatedEvent** |
| Approved version deletions completed or stopped | **DatabaseRetentionExecutionCompletedEvent** or **DatabaseRetentionExecutionFailedEvent** |
| Reconciliation found journal, S3, or SystemAdmin divergence | **DatabaseBackupServiceReconciliationEvent** |
| Bounded phase or final run measurements captured | **DatabaseRecoveryRunStatisticsCapturedEvent** |

The SystemAdmin Event Actor maps each observation to the internal commands defined by the overview. It does not parse
CloudTrail records or call AWS.

### 21.3 Progress limits

One durable event per S3 part, object chunk, WAL segment, SSTable component, or CloudTrail record is prohibited.
Meaningful persisted checkpoints include:

- engine capture boundary;
- aggregate transfer thresholds;
- primary replica published;
- recovery replica completed;
- verification level changed;
- archive retrieval phase changed;
- policy-relevant replication lag;
- restore phase changed; and
- terminal outcome.

High-cardinality details stay in service metrics, traces, logs, journal records, and immutable operation evidence.

### 21.4 SystemAdmin projections and AWS evidence reconciliation

`SystemAdminDbContext` stores the shared operation, phase, restore-point, replica, structured error, health, and
`DatabaseRecoveryRunStatsReadModel` projections in `CorePostgresCluster`. The AwsCloud processor never receives its connection
and never writes it directly. Statistics enter Core only through the common service event, Event Actor translation,
Command Actor domain event, and idempotent projector path.

After Core recovery, restored event streams rebuild the projection tables. The reconciliation workflow then validates
newer S3 manifests, catalog entries, immutable run-evidence objects, verification records, and break-glass recovery
records. Accepted facts are submitted as authenticated reconciliation commands and recorded as new domain events before
projection. S3 objects, DynamoDB journal items, Inventory, and CloudTrail never update `SystemAdminDbContext` directly.

No AWS projection migration is required beyond the implemented shared schema. In particular,
`system_admin.database_recovery_operation.backup_lineage_json` and
`system_admin.database_restore_point.backup_lineage_json` store the common lineage JSON for either source. The
remaining shared tables are `database_recovery_phase`, `database_recovery_run_stats`, `database_artifact_replica`,
`database_recovery_error`, `database_backup_policy`, `database_backup_service_health`, `database_retention_state`,
`database_backup_projection_checkpoint`, and `database_backup_projection_receipt`. Their existing source, revision,
last-event, and last-source-event columns preserve source separation and replay/idempotency. AWS-specific object
metadata stays in immutable S3 evidence and the private host journal instead of widening these projections.

### 21.5 Operator clients

The UI continues to use SystemAdmin commands and projected query models and may react to the same authorized bounded
DatabaseBackup domain events as the Console. It never consumes execution-intent or raw service-response events.
AWS-specific read-model fields are limited to safe operational facts:

- logical replica identity;
- account trust boundary and Region alias;
- primary, replication, verification, and archive-retrieval state;
- retention class, retain-until time, and legal-hold indicator;
- encryption-key health without key material;
- latest verified and restore-tested age;
- projected and measured recovery time; and
- structured policy violations.

The normal UI does not expose credentials, role sessions, raw bucket policy, presigned URLs, full object keys, or
unbounded manifests. Production restore and cutover retain the separate approvals defined by the overview.

The Console uses RequestOrigin **Console**, the same commands and queries, and the same authorized bounded domain events
as the UI. It follows work by OperationId, may expose script-safe output and exit codes, and recovers a disconnected
session through queries. It cannot consume execution-intent or raw service-response events or call AWS, the Database
Backup Host, PostgreSQL tools, or Scylla Manager directly. The normal Console remains separate from the independently
secured break-glass recovery workflow.

## 22. Observability, audit, and alerting

### 22.1 AWS audit sources

The security audit design captures:

- CloudTrail management events for S3, KMS, IAM, STS, replication, and policy changes;
- CloudTrail S3 object data events for vault reads, writes, and deletes;
- KMS use and key-state events;
- S3 replication metrics and failure/threshold events;
- S3 Inventory including version, encryption, replication, and Object Lock fields;
- configuration compliance and drift findings;
- bucket public-access and policy findings; and
- role assumptions for upload, restore, retention, legal hold, and break-glass activity.

Audit delivery goes to an independently controlled log archive. The Database Backup Service cannot alter security audit
history.

### 22.2 Required metrics

Metrics include:

- bytes captured, staged, uploaded, replicated, downloaded, and rehydrated;
- multipart upload count, retries, aborts, throughput, and age;
- PostgreSQL latest archived WAL time, queue depth, and PITR gap;
- Scylla required and completed node/token coverage;
- primary and recovery object counts and bytes pending replication;
- oldest pending replication age and replication failures;
- KMS request failures and throttling;
- current key enabled state and pending-deletion state;
- Object Lock or legal-hold validation failures;
- latest primary-complete, recovery-complete, verified, and restore-tested age;
- archive retrieval duration;
- restore throughput and validation duration;
- actual RPO and RTO;
- lifecycle transition and retained-byte forecasts; and
- estimated storage, request, replication, retrieval, and data-transfer cost by protection set.

### 22.3 Critical alerts

Critical or policy-severity alerts include:

- PostgreSQL WAL archive age or filesystem headroom threatens RPO or availability;
- primary vault upload unavailable;
- recovery replication exceeds objective or fails;
- S3 Versioning, Object Lock, Block Public Access, encryption, replication, or CloudTrail drifts;
- required KMS key is disabled, inaccessible, or scheduled for deletion;
- destination bucket or replica ownership differs from policy;
- an unauthorized GetObject, DeleteObjectVersion, retention, legal-hold, or key-policy action occurs;
- no verified or restore-tested point exists within policy;
- Scylla cluster coverage is incomplete;
- retention cannot prove dependency safety;
- staging approaches its hard reserve; or
- a break-glass role is assumed.

### 22.4 Health endpoints

HTTP health endpoints report bounded capability:

- service journal writable;
- native tools available;
- staging capacity above reserve;
- primary vault reachable;
- required key usable;
- replication monitoring active;
- current policy revision supported; and
- recovery catalog readable where policy requires.

They expose no bucket inventory, manifest body, credentials, role session, or presigned object URL.

## 23. Reliability and failure behavior

### 23.1 Operation journal

The Database Backup Service journal is outside the protected application databases and records:

- accepted event and policy revision;
- lease owner and fencing token;
- native process identity and boundary;
- staging paths and capacity reservation;
- multipart upload state;
- exact S3 object and version identities;
- verification state;
- replication state;
- outbound service-event sequence and acknowledgement;
- bounded phase/final run statistics awaiting Core acknowledgement;
- journal schema revision and reconciliation checkpoint;
- retry state; and
- cleanup eligibility.

The journal belongs to the independent Database Backup Host and survives host restarts. Core and the service reconcile
after either side or NATS restarts without duplicating native work.

The AwsCloud journal must implement the same seven logical record families as the completed SQLite journal. DynamoDB
may use one physical table or reviewed companion tables, but it cannot collapse or omit these durability boundaries:

| Local logical schema | Required AwsCloud logical data and identity |
| --- | --- |
| `journal_operation` | operation ID; source; operation kind; protection set; immutable definition hash; intent event ID/type/payload; phase; terminal flag; lease host/expiry; fencing token; last service sequence; admitted/updated UTC |
| `journal_inbox` | source event ID as unique identity; operation ID; content hash; admitted UTC |
| `journal_checkpoint` | operation ID + fencing token + phase identity; terminal flag; safe diagnostic reference; observed UTC |
| `journal_artifact_replica` | operation ID + logical artifact-replica ID; state; safe destination reference; fencing token; updated UTC; AWS-private multipart and exact object-version state may be attached below this family |
| `journal_outbox` | event ID plus unique operation/service-sequence constraint; allowlisted event type; canonical payload and content hash; publish state/attempts; created/published UTC |
| `journal_run_stats` | operation ID + statistics revision; bounded statistics payload; publication state |
| `journal_reconciliation` | operation ID; acknowledged Core domain revision; acknowledged UTC |

The journal serializes the same allowlisted `DatabaseBackupEventContract` types and computes the same immutable
operation-definition identity from operation ID, source, operation kind, protection set, and backup-set identity. The
requested mode is therefore retained in the admitted execution event's `BackupLineage`; no separate AWS-only request
shape is needed. A duplicate event ID with a different content hash or an operation ID with a different definition is a
conflict, not a retry.

Conditional-write behavior must preserve the SQLite transaction semantics: inbox admission and operation creation are
atomic; lease mutation compares the current fencing token/revision; checkpoints and replica updates require the current
fence; service sequence is unique and monotonic per operation; an outbox item is durable before JetStream publish and
is marked published only after server acknowledgement; and terminal compaction cannot remove unacknowledged outbox or
statistics records.

Production AWS-hosted deployments use a DynamoDB execution-journal adapter in the workload account. The adapter uses
conditional writes for lease/fencing and revision checks, encrypted storage, strongly consistent reads where admission
or ownership requires them, and point-in-time recovery. Terminal-item expiry or compaction is allowed only after the
SystemAdmin outcome is acknowledged and required immutable S3 manifest/run evidence is durable. TTL is cleanup, never
the correctness boundary.

A single-host development or workstation deployment may use the same durable embedded journal profile as
LocalWorkstation on an encrypted persistent Docker/bind-mounted volume. It cannot claim the production multi-host
fencing profile. Both adapters implement the same private execution-journal capability and do not change actor
commands, events, queries, or BackupSource behavior.

Loss of the workload-account journal makes ambiguous incomplete work non-resumable but does not invalidate immutable
published S3 evidence. Recovery reconstructs published results from exact object versions, manifests, commits, catalog
entries, and run evidence, then uses the common reconciliation flow rather than inferring success.

### 23.2 Failure matrix

| Failure | Required behavior |
| --- | --- |
| Core unavailable before dispatch | No service operation begins; SystemAdmin retains intent and reports capability risk |
| Core unavailable after dispatch | Service continues only to the policy-approved safe boundary, journals events, and reconciles later |
| NATS unavailable | Same as Core communication loss; no duplicate operation on reconnect |
| Backup Service restart | Reconcile journal, multipart state, native process, S3 versions, and last service sequence |
| Duplicate execution event | Resolve to existing OperationId and policy revision |
| Reordered or missing service observation | Detect service-sequence gap, stop unsafe state advancement, and reconcile before acknowledgement |
| Native database unavailable | Fail preflight or stop at a native safe boundary; preserve structured diagnostics and do not publish |
| PostgreSQL base capture interrupted | Mark capture unusable unless the native tool proves resumability; retain bounded diagnostic evidence |
| PostgreSQL incremental parent missing or differs across required replicas | `Automatic` resolves full; explicit `Incremental` rejects before capture |
| PostgreSQL incremental chain/tool prerequisite fails | Apply the same fallback/failure rule before capture; never publish partial lineage |
| Scylla native task interrupted | Reconcile Scylla Manager state and required-node coverage; never infer cluster completeness |
| Credential expiry | Refresh temporary role session; pause safely if refresh fails |
| Primary S3 unavailable | Retry within budget, preserve staging, expose WAL/backpressure risk, and do not claim AWS completion |
| KMS unavailable | Stop affected upload or restore safely and alert; never fall back to unapproved encryption |
| Multipart interruption | Resume exact compatible upload or abort and create a new unique object identity |
| Checksum mismatch | Quarantine operation evidence, never publish, and recapture or retransmit |
| Primary complete, replication pending | Preserve primary; show recovery replica incomplete and policy compliance accordingly |
| Replication failed | Diagnose permissions/key/policy, repair with controlled Batch Replication, then reverify |
| Recovery Region unavailable | Preserve primary; alert on degraded regional recovery |
| Primary Region unavailable | Restore from independently validated recovery replica |
| Manifest or signature invalid | Restore point is ineligible regardless of object presence |
| PostgreSQL parent/base/WAL dependency or Scylla selected-snapshot object missing | Mark the affected restore point ineligible and prevent dependent retention changes; do not infer a Scylla lineage dependency |
| PostgreSQL WAL gap | PITR targets beyond the gap are ineligible; retain surrounding evidence for diagnosis |
| Scylla node/token omission | Cluster restore point is incomplete and unpublished |
| Staging exhausted | Reject new work or pause safely; protect WAL and active operation reserves |
| Archive retrieval delayed | Remain in explicit retrieval phase and revise forecast RTO |
| Restore target version or topology incompatible | Reject before target mutation and require an explicitly supported recovery plan |
| Native or application validation failed | Keep the fresh target isolated, report structured failure, and prohibit cutover |
| Cutover failed | Preserve old and restored targets, fence further mutation, and enter the separately approved rollback procedure |
| Retention race with restore | Restore lease and manifest references fence deletion |
| Key scheduled for deletion | Critical alert, recovery-readiness failure, and security response |
| Complete loss of Core and databases | Use recovery account, destination catalog, and break-glass workflow |

### 23.3 No silent downgrade

The service never silently:

- changes Compliance mode to Governance mode;
- uses SSE-S3 instead of the approved KMS key;
- publishes without a recovery replica when policy requires it;
- omits PostgreSQL parent/WAL dependencies or objects referenced by a selected Scylla Manager restore point;
- changes an explicit `Incremental` request to full;
- selects a warmer or colder storage class than policy allows;
- acknowledges an unprotected PostgreSQL WAL segment;
- uses an unsigned manifest;
- changes accounts or Regions; or
- treats a log message as an operation state.

## 24. Security and threat controls

### 24.1 Workload compromise

Normal workload roles can create only approved immutable objects through temporary sessions. They cannot alter existing
versions or recovery-vault policy. Compliance retention limits destructive action even if an upload role is compromised.
Unique operation IDs, policy revision, signatures, and SystemAdmin authorization prevent a compromised service from
silently replacing a known restore point.

### 24.2 Backup-account compromise

The recovery replica is owned by a separate account and key. One-way replication, independent administrators, immutable
versions, and independent audit reduce the chance that a single account compromise destroys both copies.

### 24.3 Ransomware and poisoned backups

Immutability alone can preserve encrypted or logically corrupted source data. The design also requires:

- historical retention;
- application checkpoints;
- malware and anomaly controls appropriate to database artifacts;
- native validation;
- isolated restore testing;
- deliberate selection of a recovery point before the incident; and
- prevention of automatic cutover.

### 24.4 Malicious deletion or policy weakening

Object Lock Compliance mode, separate deletion roles, organization guardrails, configuration drift alarms, CloudTrail
data events, independent audit ownership, and exact-version deletion plans provide layered control.

### 24.5 Credential exposure

Secrets are redacted from process arguments, error messages, manifests, object keys, tags, metrics, traces, actor events,
and UI. Temporary credentials live only in service memory and the supported AWS credential provider chain.

### 24.6 Supply-chain and recovery-tool risk

Native database tools, AWS SDKs, recovery binaries, and container images are version-pinned, vulnerability-scanned,
signed where supported, and recorded in operation manifests. Recovery drills prove that the retained toolchain can read
older formats.

## 25. Performance and cost architecture

### 25.1 Performance controls

The service configures separate bounded limits for:

- native database capture;
- local staging reads and writes;
- concurrent artifacts;
- multipart parts per artifact;
- aggregate upload bandwidth;
- aggregate download bandwidth;
- KMS request rate;
- checksum CPU;
- replication observations;
- archive retrieval jobs; and
- concurrent engine restores.

Backup I/O is subordinate to database latency policy. Restore drills may use higher limits on isolated infrastructure.
The dedicated Database Backup Host has independent CPU, memory, disk, and network budgets and does not share the
**Api.Server** process.

### 25.2 Object sizing

Very small objects increase S3, KMS, lifecycle, inventory, and retrieval request costs. Extremely large objects reduce
parallel recovery flexibility. Native artifact boundaries are preserved where meaningful, and packaging is allowed only
when it does not weaken native validation, deduplication, selective retry, or dependency interpretation.

Object-size and multipart-part-size choices are benchmarked using representative PostgreSQL and Scylla data.

### 25.3 Cost drivers

Cost reporting accounts for:

- active and archived S3 storage;
- noncurrent versions;
- minimum storage-duration charges;
- PUT, GET, LIST, HEAD, multipart, lifecycle, inventory, and Batch Operations requests;
- KMS requests;
- Cross-Region Replication and inter-Region transfer;
- S3 RTC;
- CloudTrail object data events;
- archive transition and retrieval;
- temporary restored copies;
- restore compute and staging volumes; and
- duplicate retention caused by failed or incomplete operations.

Cost pressure cannot silently shorten retention, remove a required replica, disable verification, or move an operational
recovery point into a storage class that violates RTO. Policy changes go through SystemAdmin authorization.

### 25.4 Capacity forecasting

Forecasting uses:

- PostgreSQL full/incremental backup size and WAL generation percentiles;
- Scylla snapshot growth and SSTable churn;
- retention-chain dependencies;
- replication lag;
- staging high-water marks;
- restore workspace requirements;
- lifecycle minimum durations; and
- at least one simultaneous recovery reserve where policy requires it.

## 26. Configuration ownership and deployment composition

### 26.1 Core-owned configuration

Core owns destination-neutral policy:

- the `SystemAdminDbContext` projection connection, schema version, migration policy, and projector checkpoint policy;
- protection sets and classifications;
- schedules;
- RPO and RTO classes;
- logical required destinations;
- retention classes;
- verification and drill frequency;
- legal-hold authorization policy;
- cutover approval policy; and
- expected Database Backup Service capability revision.

### 26.2 Service-owned AWS configuration

The Database Backup Service owns validated bootstrap configuration:

- logical destination-to-account/Region/bucket mapping;
- role identities and credential-provider mode;
- KMS key and signing-key references;
- required Object Lock mode and minimum retention;
- replication rule identity and RTC requirement;
- S3 endpoint and network controls;
- multipart, bandwidth, retry, and KMS limits;
- staging and journal locations;
- journal provider/table identity, KMS reference, consistency/fencing policy, PITR, schema revision, retention, and
  compaction settings;
- inventory and audit integration;
- archive retrieval policy;
- native database endpoint or agent configuration; and
- incremental enablement, maximum chain depth, maximum base age, and PostgreSQL/Scylla native prerequisites using the
  same fallback and failure semantics as the shared planner;
- break-glass capability configuration.

Startup fails closed when a mandatory production control is absent or weaker than policy.

### 26.3 Database Backup Host resource

The independently runnable **TomasAI.IFM.Api.DatabaseBackup.Host** receives:

- its own workload identity;
- its own network policy and endpoints;
- its own AWS configuration and temporary role path;
- independent CPU, memory, disk, and network constraints;
- independent deployment and restart lifecycle; and
- NATS event transport using the same contracts for UI, Console, and ScheduledTask callers.

Paper-trading development and functional tests run the Worker without Aspire. Ubuntu 24.04 Docker packaging is
qualified after the functional gates. A later full-system Linux production migration may use Aspire to compose the
Docker host, NATS, database dependencies, AWS resource references, and observability. Aspire does not hold backup state,
own credentials, execute backup behavior, or remain required after the service container starts.

## 27. Environment strategy

### 27.1 Development

Development defaults to disabled or dry-run production operations. It may use disposable buckets or an S3-compatible
emulator for contract tests, but emulation cannot prove AWS Object Lock, KMS, IAM, CRR, CloudTrail, archive retrieval, or
cross-account behavior.

### 27.2 AWS integration environment

An isolated AWS integration environment validates:

- temporary role assumption;
- bucket and key policies;
- multipart checksums;
- object-version identity;
- Object Lock Governance mode;
- signed manifest publication;
- shared `Full | Automatic | Incremental` selection, common-parent replica checks, explicit-incremental failure, and
  automatic full fallback;
- PostgreSQL 17 full-to-incremental capture, complete-chain `pg_combinebackup`, native verification, and fresh-target
  boot/query validation;
- Scylla Manager deduplicated-snapshot lineage with an empty IFM dependency array and fresh-target restore;
- manifest-v1 legacy-full reads and manifest-v2 lineage validation;
- DynamoDB conditional lease/fencing, duplicate admission, unacknowledged event/statistics replay, PITR configuration,
  schema compatibility, and safe terminal compaction;
- idempotent `SystemAdminDbContext` projections and projection rebuild from domain events;
- replication and destination ownership;
- KMS re-encryption;
- inventory and CloudTrail evidence;
- controlled retention cleanup; and
- fresh disposable database restore.

### 27.3 Production-readiness environment

Before production, a representative environment validates:

- the three-account boundary;
- Compliance-mode consequences;
- realistic backup and WAL volume;
- full Scylla cluster coverage;
- recovery-Region-only restore;
- primary key denial during recovery;
- archive retrieval;
- Core and NATS absence;
- actual RPO/RTO; and
- security and operations runbooks.

## 28. Architecture acceptance criteria

The AWS design is accepted only when evidence demonstrates:

1. The workload account cannot administer either vault.
2. Primary and recovery vaults use separate accounts and Regions.
3. Both vaults have Versioning, Object Lock, Block Public Access, bucket-owner enforcement, and SSE-KMS.
4. Published production objects use the approved Compliance retention.
5. A recovery replica is encrypted under a recovery-account key and owned by the recovery account.
6. No Core actor, PostgreSQL node, Scylla node, or Scylla Manager agent holds AWS destination credentials.
7. All application AWS access uses temporary role credentials.
8. PostgreSQL full/incremental physical backup and continuous WAL provide tested PITR.
9. PostgreSQL does not acknowledge AWS protection for a WAL segment before the declared durable boundary.
10. Scylla Manager capture proves complete required-node and token coverage.
11. Scylla staging preserves native manifests and is not treated as a durable recovery destination.
12. Every artifact is addressed by exact S3 version and validated checksum.
13. Manifest, commit, and catalog publication cannot expose a partial restore point.
14. Catalog reconstruction works without Core, NATS, PostgreSQL, or ScyllaDB.
15. Replication status and destination encryption are verified per required object version.
16. Catalog and current manifest discovery do not depend on archive retrieval.
17. Retention cannot delete a referenced dependency, legal-held version, active restore input, or latest restore-tested set.
18. Deletion uses an exact approved version list and a separate role.
19. KMS key disablement or pending deletion creates an immediate recovery-readiness failure.
20. CloudTrail object data events and security audit records are stored outside service control.
21. A recovery-account-only break-glass drill restores fresh PostgreSQL and ScyllaDB targets.
22. Production restore stops at **ReadyForCutover** until separately approved.
23. SystemAdmin authoritative state changes only through Command Actor domain events.
24. AWS observations enter Core only through service events, Event Actor translation, and durable Command Actor
    application.
25. The independent Database Backup Host and Core restart and reconcile separately using the same operation and
    manifest semantics without duplicating native work.
26. Actual RPO and RTO are measured using representative data.
27. Cost forecasts include storage, replication, KMS, audit, lifecycle, retrieval, and restore compute.
28. No acceptance criterion depends on the Aspire AppHost remaining online.
29. Every AWS source-bound event carries BackupSource **AwsCloud** while retaining the shared source-independent event
    type; BackupSource **None** is rejected for operations and source-bound events.
30. UI, Console, and ScheduledTask callers use the same DatabaseBackup command and query surface without consuming raw
    AWS service events.
31. The service runs as a standalone Worker from the first implementation and never executes inside Api.Server or an
    actor; Docker packaging and Aspire orchestration are introduced only at their approved qualification/migration gates.
32. PostgreSQL and ScyllaDB execution is available only through the shared typed, allowlisted backup/restore
    capabilities.
33. The Console follows the same actor API and bounded events as the UI and cannot bypass SystemAdmin.
34. UI and Console observe only authorized bounded DatabaseBackup domain events, never execution-intent or raw service
    events.
35. `SystemAdminDbContext` contains only rebuildable event projections and bounded run statistics and is never written
    directly by the AwsCloud processor.
36. The production AWS execution journal provides durable conditional lease/fencing and revision updates outside the
    protected databases, and retains unacknowledged outbound events/statistics for replay.
37. Immutable S3 manifests/run evidence retain bounded final statistics required to interpret and reconcile completed
    recovery points without Core or the workload-account journal.
38. After restore, newer AWS evidence enters SystemAdmin only through authenticated reconciliation commands and domain
    events before projection.
39. AwsCloud uses the implemented MessagePack key layout, including command key 31 for requested mode and event key 31
    for lineage, without introducing AWS-specific command/event/read-model types.
40. Every new AWS engine manifest is shared schema version 2 with `Source=AwsCloud`; legacy schema version 1 is read only
    as a normalized full backup.
41. `Full`, `Automatic`, and explicit `Incremental` pass the same planner semantics as LocalWorkstation, including a
    verified equivalent parent on every required replica, bounded depth/base age, automatic fallback, and explicit
    failure.
42. A PostgreSQL 17 full-to-incremental chain is reconstructed oldest-first with `pg_combinebackup`, natively verified,
    and restored to a fresh queried target from each required AWS replica class.
43. A Scylla Manager deduplicated snapshot records base/parent/depth lineage but no IFM manifest dependency; fresh
    restore proves the selected Manager restore point is logically complete.
44. AwsCloud reuses the existing SystemAdmin projection tables and `backup_lineage_json` columns with source
    discrimination; no direct AWS projection tables or S3-to-projection writes exist.
45. The DynamoDB journal preserves all seven implemented logical journal record families, admission/content-conflict
    behavior, fenced checkpoints, monotonic service sequence, and durable outbox/statistics replay.

## 29. Approved architecture decisions and proposed amendments

The existing production directions were approved on 2026-08-10. Rows explicitly labeled **proposal** are additions
for this architecture review. Later changes require an explicit architecture revision rather than an
implementation-level substitution:

| Decision | Proposed production direction |
| --- | --- |
| AWS trust topology | Workload, primary backup, and recovery accounts |
| Region topology | Primary backup Region near workload; distinct approved recovery Region |
| Immutable storage | S3 Versioning and Object Lock on both vaults |
| Lock mode | Compliance mode for published production recovery objects |
| Encryption | Independent customer-managed regional KMS keys |
| Replication | One-way cross-account CRR; RTC for recovery classes that require it |
| Recovery replica | Required for production policy compliance |
| Object publication | Artifact versions, signed manifests, commit record, append-only catalog |
| Scylla AWS movement | Service-controlled staging and upload; no AWS credentials on Scylla agents |
| Native capability API — v0.3 proposal | Shared typed PostgreSQL and Scylla backup/restore ports; PostgreSQL protocol/utilities and Scylla Manager REST or allowlisted CLI fallback remain adapter details |
| PostgreSQL WAL acknowledgement | After verified primary-vault durability |
| Deep archive | Allowed only for explicitly delayed recovery classes |
| AWS Backup | Optional tertiary control, not the native backup or catalog authority |
| Retention deletion | Separate approved exact-version plan per vault |
| Break-glass identity | Independent federation, strong MFA, short role sessions, no static keys |
| Cutover | Separate approval after fresh-target validation |
| Operator clients — v0.3 proposal | UI and Console use the same DatabaseBackup actor commands, queries, and authorized bounded domain events; no direct execution path and no raw service/execution event subscription |
| Deployment — v0.5 | Standalone Database Backup Host for functional paper-trading development; Ubuntu 24.04 Docker qualification later; Aspire deferred to the full-system Linux production migration |
| SystemAdmin persistence — v0.4 proposal | Rebuildable `SystemAdminDbContext` projections and `DatabaseRecoveryRunStatsReadModel` in Core PostgreSQL; no direct AwsCloud writes |
| Production execution journal — v0.4 proposal | Encrypted workload-account DynamoDB adapter using conditional lease/revision writes and PITR; development/single-host profile may use the encrypted embedded journal |
| Recovery evidence — v0.4 proposal | Immutable S3 manifest/run-evidence objects carry the bounded final summary and remain usable without Core or the execution journal |
| Post-restore reconciliation — v0.4 proposal | Replay restored events, validate newer AWS evidence, append accepted reconciliation events, then update projections |
| Shared contract/storage baseline — v0.6 | Reuse implemented MessagePack keys, manifest schema v2, SystemAdmin lineage projections, seven logical journal record families, and source-neutral incremental semantics |
| Incremental defaults — v0.6 | Initial AWS depth/base-age defaults match validated local values (six descendants/seven days) and remain policy-configurable |
| Scylla incremental meaning — v0.6 | Manager restore points are logically complete and physically deduplicated; record lineage but no IFM SSTable dependency chain |

## 30. Alignment with the completed local implementation

The LocalWorkstation implementation is now the executable baseline for shared behavior. AwsCloud reuses without schema
forks:

- operation, backup-set, artifact, and replica identities;
- `BackupSource`, `DatabaseBackupMode`, `DatabaseNativeBackupKind`, command/event/read-model MessagePack keys, and NATS
  routes;
- `DatabaseBackupLineage` selection, validation, propagation, and legacy normalization;
- signed `DatabaseBackupManifest` schema version 2 and schema-version-1 legacy-full reads;
- signed manifest and commit semantics;
- checksum requirements;
- catalog reconstruction;
- SystemAdmin and Database Backup Service event flow;
- existing SystemAdmin projection tables and `backup_lineage_json` fields;
- the seven logical journal record families, content-conflict rules, fencing, outbox, statistics, and reconciliation;
- verification and restore-test terminology;
- PostgreSQL direct-parent dependency closure and Scylla logically complete deduplicated-snapshot semantics;
- `Full | Automatic | Incremental` fallback/failure behavior and bounded chain planning;
- fresh-target restore and separate cutover; and
- break-glass recovery evidence.

AwsCloud supplies only the destination-specific implementation delta: temporary-role identity, DynamoDB persistence,
S3 exact object versions, KMS/signing, Object Lock, cross-account/cross-Region replication, archive retrieval, AWS
audit, and AWS-specific break-glass access. Those details are bound by signed publication/catalog evidence and private
journal state; they do not widen public actor messages or create a second SystemAdmin schema.

Implementation must therefore start by reusing the existing shared/domain/application projects and extracting only
source-neutral validators currently housed in the LocalWorkstation adapter where necessary. It then adds an AwsCloud
processor, S3 publication/catalog/restore-source adapters, a DynamoDB execution-journal adapter, AWS signing and
identity adapters, and AWS integration tests. Copying the LocalWorkstation project and changing paths is prohibited
because it would fork contract and lineage behavior.

## 31. References

### Shared IFM architecture

- [IFM database backup and restore architecture overview](Database-Backup-Architecture-Overview.md)
- [AWS cloud code implementation specification](AWS-Cloud-Backup-Restore-Code-Implementation-Specification.md)
- [Local workstation code implementation specification](Local-Workstation-Backup-Restore-Code-Implementation-Specification.md)
- [Local workstation incremental validation report](Local-Workstation-Backup-Restore-Incremental-Validation-Report.md)
- [IFM Aspire migration overview](../../Documents/system/Aspire%20migration%20overview.md)

### Amazon S3

- [Amazon S3 Object Lock](https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock.html)
- [S3 Object Lock considerations and replication](https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock-managing.html)
- [Replicating encrypted S3 objects](https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication-config-for-kms-objects.html)
- [Replicating objects within and across Regions](https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication.html)
- [S3 Replication Time Control](https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication-time-control.html)
- [S3 object replication status](https://docs.aws.amazon.com/AmazonS3/latest/userguide/replication-status.html)
- [S3 object integrity and checksums](https://docs.aws.amazon.com/AmazonS3/latest/userguide/checking-object-integrity-upload.html)
- [S3 multipart upload](https://docs.aws.amazon.com/AmazonS3/latest/userguide/mpuoverview.html)
- [S3 data consistency model](https://docs.aws.amazon.com/AmazonS3/latest/userguide/Welcome.html#ConsistencyModel)
- [Working with archived S3 objects](https://docs.aws.amazon.com/AmazonS3/latest/userguide/archived-objects.html)
- [S3 Lifecycle transition considerations](https://docs.aws.amazon.com/AmazonS3/latest/userguide/lifecycle-transition-general-considerations.html)
- [S3 Inventory](https://docs.aws.amazon.com/AmazonS3/latest/userguide/configure-inventory.html)

### AWS identity, encryption, network, and audit

- [IAM roles](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles.html)
- [IAM security best practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)
- [AWS KMS key deletion](https://docs.aws.amazon.com/kms/latest/developerguide/deleting-keys.html)
- [S3 gateway VPC endpoints](https://docs.aws.amazon.com/vpc/latest/privatelink/vpc-endpoints-s3.html)
- [CloudTrail data events](https://docs.aws.amazon.com/awscloudtrail/latest/userguide/cloudtrail-events.html#data-events)
- [AWS backup and recovery approaches](https://docs.aws.amazon.com/prescriptive-guidance/latest/backup-recovery/introduction.html)
- [DynamoDB condition expressions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Expressions.ConditionExpressions.html)
- [DynamoDB read consistency](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html)
- [DynamoDB encryption at rest](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/EncryptionAtRest.html)
- [DynamoDB point-in-time recovery](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Point-in-time-recovery.html)
- [DynamoDB time to live](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html)

### Database-native recovery

- [PostgreSQL pg_basebackup](https://www.postgresql.org/docs/current/app-pgbasebackup.html)
- [PostgreSQL replication protocol](https://www.postgresql.org/docs/current/protocol-replication.html)
- [PostgreSQL pg_verifybackup](https://www.postgresql.org/docs/current/app-pgverifybackup.html)
- [PostgreSQL continuous archiving and PITR](https://www.postgresql.org/docs/current/continuous-archiving.html)
- [Scylla Manager backup](https://manager.docs.scylladb.com/stable/backup/)
- [Scylla Manager backup format](https://manager.docs.scylladb.com/stable/backup/specification.html)
- [Scylla Manager restore](https://manager.docs.scylladb.com/stable/restore/)
- [Scylla Manager REST API](https://manager.docs.scylladb.com/stable/swagger/index.html)

## 32. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-10 | Created the AWS reference architecture covering three-account isolation, immutable S3 vaults, KMS, IAM, one-way cross-Region replication, native PostgreSQL and Scylla workflows, signed publication, restore, retention, break-glass recovery, audit, cost, Aspire extraction, and acceptance criteria. |
| 0.2 | 2026-08-10 | Recorded approval of Section 29 and aligned with overview version 0.6 by assigning BackupSource AwsCloud to shared DatabaseBackup events, preserving primary/recovery replica identities, and confirming the shared UI and ScheduledTask command/query surface. |
| 0.3 | 2026-08-11 | Proposed alignment with overview 0.7: direct Docker/Aspire host deployment, shared three-value BackupSource semantics, the common UI/Console/ScheduledTask actor API, and typed PostgreSQL/Scylla native capability adapters. |
| 0.4 | 2026-08-11 | Proposed the shared four-store persistence model for AwsCloud: `SystemAdminDbContext` projections/run statistics, a conditional durable production execution journal, immutable S3 run evidence, and event-gated post-restore reconciliation. |
| 0.5 | 2026-08-12 | Aligned deployment sequencing with paper trading: standalone Worker development, later Ubuntu 24.04 Docker qualification, and Aspire deferred to the full-system Linux production migration. |
| 0.6 | 2026-08-21 | Aligned AwsCloud with the completed LocalWorkstation message and storage baseline: stable MessagePack keys, manifest schema v2, shared SystemAdmin lineage projections, seven logical journal families, Full/Automatic/Incremental planning, PostgreSQL 17 chain reconstruction, and Scylla Manager logically complete deduplicated-snapshot semantics. AWS adapters remain unimplemented. |
