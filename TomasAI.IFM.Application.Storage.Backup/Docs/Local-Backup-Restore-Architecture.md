# IFM Local Workstation Database Backup and Restore Architecture

Status: Approved architecture with implemented incremental-backup amendment
Version: 0.5
Date: 2026-08-18
Scope: Local workstation protection for PostgreSQL and ScyllaDB using encrypted online and rotated offline storage
Parent architecture: Database-Backup-Architecture-Overview.md version 0.9
Reference architecture: AWS-Cloud-Backup-Restore-Architecture.md version 0.4
BackupSource: LocalWorkstation

## 1. Purpose

This document defines the local workstation reference architecture for backing up and restoring the IFM PostgreSQL and
ScyllaDB clusters. It applies the approved common architecture and follows the AWS design's logical operation,
manifest, catalog, verification, retention, and recovery model while replacing AWS mechanisms with explicit local
storage controls.

The design resolves the local questions deferred by the overview:

- online and offline local vault topology;
- Windows and Linux volume, filesystem, and encryption requirements;
- stable media identity independent of drive letter or mount path;
- durable file publication without S3 object versions;
- local access-control and retention-deletion separation;
- PostgreSQL WAL and base-backup handling;
- Scylla Manager capture and local artifact handling;
- removable-media rotation and custody;
- local catalog reconstruction and break-glass recovery;
- capacity, device health, corruption, ransomware, and workstation-failure behavior;
- restore testing and evidence; and
- the capability limits that prevent ordinary workstation storage from being described as equivalent to AWS.

This is an architecture and design document. It does not prescribe C# classes, command scripts, specific drive models,
drive letters, mount paths, volume sizes, purchasing choices, or an implementation task breakdown.

## 2. Inherited architecture

This design inherits these non-negotiable rules:

1. The SystemAdmin DatabaseBackup feature has Command, Event, and Query actors with the responsibilities defined by the
   overview.
2. The UI, Database Backup Console, and separate SystemAdmin ScheduledTask feature invoke the same DatabaseBackup
   commands and queries using different RequestOrigin and authenticated identities.
3. ScheduledTask does not consume raw Database Backup Service events or call backup executors directly.
4. The shared BackupSource enum is **None**, **LocalWorkstation**, and **AwsCloud**. This processor accepts only
   **LocalWorkstation** operations; **None** is limited to unselected/default state or explicit all-sources queries.
5. Every source-bound DatabaseBackup event uses the common source-independent type and carries BackupSource
   **LocalWorkstation** for this processor.
6. The DatabaseBackup Command Actor owns authoritative event-sourced control state and committed execution intent.
7. The DatabaseBackup Event Actor translates service observations into commands and acknowledges only after durable
   Command Actor application or idempotent prior application.
8. The DatabaseBackup Query Actor reads projected models and never queries a vault or execution journal as a hidden
   source of truth.
9. The Database Backup Service executes native capture, transfer, verification, retention, restore, and local-media
   behavior outside actor threads.
10. The service runs from the first implementation as the independently runnable
    **TomasAI.IFM.Api.DatabaseBackup.Host** .NET 10 Worker and communicates with Core over NATS. Ubuntu 24.04 Docker
    packaging is a later paper-trading qualification gate; Aspire is deferred to the full-system production migration.
11. The actors live under **TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup** and retain exactly the shared Command,
    Event, and Query roles; LocalWorkstation does not introduce source-specific actor types.
12. PostgreSQL and ScyllaDB are protected at physical cluster or declared protection-set boundaries.
13. Native artifacts never travel through actor mailboxes, NATS, or HTTP management endpoints.
14. Restore uses fresh targets and requires separate production cutover approval.
15. Destination manifests and catalogs remain usable without Core, NATS, or the protected databases.
16. Break-glass recovery is mandatory.
17. `SystemAdminDbContext` stores rebuildable event projections and bounded run statistics in Core PostgreSQL; the
    LocalWorkstation processor never writes it directly.
18. The local Database Backup Host stores its private execution journal on an encrypted persistent volume separate
    from the protected databases, removable backup media, and disposable container filesystem.
19. Backup callers choose `Full`, `Automatic`, or `Incremental` through the common actor contract. `Automatic` may
    fall back to full; explicit `Incremental` must fail when a valid common parent or native eligibility is absent.
20. PostgreSQL incremental restore points form an explicit signed dependency chain. Scylla Manager restore points are
    logically complete and may be physically deduplicated without exposing an artificial SSTable chain to callers.

If this document conflicts with the approved overview, the overview controls. Where this document offers weaker
protection than the AWS reference, the limitation must be visible in policy, read models, UI, alerts, and recovery
qualification.

## 3. AWS-to-local architecture mapping

The local design preserves AWS semantics through the following mapping:

| AWS reference mechanism | Local workstation mechanism | Important difference |
| --- | --- | --- |
| Primary immutable S3 vault | Dedicated encrypted online vault volume | Usually shares workstation, power, and administrative fault domains |
| Recovery-account S3 vault | Encrypted rotated offline media set | Requires correct human rotation and physical custody |
| S3 object key and version ID | Unique generation-relative path and immutable ArtifactVersionId | Ordinary filesystems do not provide retained object versions automatically |
| S3 Object Lock Compliance | No-overwrite publication, restrictive ACLs, separate deletion identity, and offline disconnection | Local administrator or physical attacker may bypass online controls; not Compliance-mode equivalent |
| Cross-Region Replication | Verified copy to a different rotated medium stored separately | Not continuous and has a human-dependent RPO |
| SSE-KMS | BitLocker on Windows or LUKS2/dm-crypt on Linux | Recovery material must be independently escrowed and platform-compatible |
| IAM workload roles | Dedicated operating-system service identities and explicit elevation boundaries | Local administrators remain a powerful shared trust boundary |
| S3 checksum and metadata | Cryptographic file digest, length, durable-write record, and read-back verification | Storage hardware may acknowledge writes incorrectly; restore tests remain essential |
| S3 commit object | Durable signed publication commit file written last | Publication durability depends on filesystem and flush behavior |
| S3 append-only catalog entry | Unique signed catalog entry file | Local ACLs cannot provide AWS account isolation |
| S3 Inventory | Scheduled vault scan and independently generated inventory snapshot | Inventory is workstation-generated and must be cross-checked |
| Glacier storage class | Physically offline media | Media connection and unlock time contributes to RTO |
| CloudTrail and account audit | Service journal, OS security log, signed operation records, and offline custody record | Audit can share the workstation failure domain unless exported |
| AWS recovery role | Independently controlled recovery identity and encryption recovery material | Operator procedures and physical access are part of the boundary |

The mapping preserves meaning, not equal durability. **LocalWorkstation** is a valid BackupSource but cannot claim the
same account, Region, or managed immutability guarantees as **AwsCloud**.

## 4. Architectural decisions

The local reference design adopts:

| Concern | Decision |
| --- | --- |
| BackupSource | The shared enum is `None`, `LocalWorkstation`, and `AwsCloud`; this processor admits only `LocalWorkstation` operations |
| Fast recovery copy | Dedicated encrypted online vault on a different physical storage device from active databases |
| Offline recovery copy | At least two encrypted removable media devices in rotation |
| Production local readiness | Requires a current verified offline replica stored separately; online-only is degraded |
| Windows encryption | BitLocker for fixed and removable vault volumes |
| Linux encryption | LUKS2 over dm-crypt |
| Windows filesystem | NTFS baseline; ReFS allowed for supported fixed volumes after compatibility validation |
| Linux filesystem | XFS or ext4 after durability and recovery validation |
| Unsupported filesystems | FAT, FAT32, and exFAT are not production vault filesystems |
| Volume identity | Stable volume GUID or filesystem/LUKS UUID plus signed MediaId; never drive letter alone |
| Publication | Unique no-overwrite paths, durable flush, verification, signed manifest, commit record, then catalog entry |
| Online immutability | Best-effort ACL and identity separation; never represented as S3 Object Lock equivalent |
| Offline isolation | Media is unlocked and attached only for bounded synchronization, verification, or restore windows |
| Recovery keys | Escrowed independently from Core, the protected database, and the protected workstation |
| PostgreSQL | Physical base backup plus continuous WAL into the online vault |
| ScyllaDB | Scylla Manager cluster coordination through service-controlled local staging or vault ingress |
| Offline chain | Every advertised offline restore point is self-contained on one medium |
| Retention | Exact-path, dependency-safe plan; offline deletion waits for the identified medium |
| Restore | Copy verified artifacts to a separate encrypted restore workspace, then restore fresh targets |
| Media disposal | Cryptographic erase or approved physical destruction; file deletion alone is not secure erasure |

## 5. Goals and non-goals

### 5.1 Goals

The architecture must:

- provide low-latency recovery from an online local copy;
- provide a disconnected copy capable of surviving workstation ransomware and loss of the online vault;
- protect PostgreSQL PITR and complete ScyllaDB cluster restore points;
- identify every device and artifact without trusting drive letters or mutable paths;
- detect partial writes, wrong media, missing dependencies, and filesystem corruption;
- keep recovery keys available if the workstation and Core are lost;
- prevent routine service identity from deleting published history;
- preserve the AWS manifest, catalog, verification, and restore-test meanings;
- make media age and physical isolation visible;
- support Windows first without preventing a validated Linux host;
- bound local disk, USB, CPU, and memory pressure; and
- measure real local and offline-media RPO/RTO.

### 5.2 Non-goals

This design does not:

- claim that an attached workstation disk is a disaster-recovery copy;
- claim that NTFS, ReFS, XFS, ext4, BitLocker, or LUKS alone provides S3 Object Lock semantics;
- back up to the operating-system volume or active database volume;
- treat a different partition on the same physical device as fault isolation;
- use a user-selected arbitrary path as a vault;
- support production backup on FAT or exFAT removable media;
- require Windows media to be directly readable on Linux or Linux media on Windows;
- define network-attached storage, immutable NAS, tape, optical WORM, or third-party backup products;
- stream database artifacts through NATS;
- automatically cut over production after restore;
- securely erase individual files from SSD or flash media by ordinary deletion; or
- promise RPO/RTO before representative restore drills.

## 6. System context

The normal local path is:

    UI, Console, or SystemAdmin ScheduledTask actor
                  |
                  | common source-scoped command
                  v
    SystemAdmin DatabaseBackup actors
                  |
                  | committed event
                  | BackupSource = LocalWorkstation
                  v
    Docker/Aspire Database Backup Service
       LocalWorkstation processor
          |-- native PostgreSQL/Scylla coordination
          |-- checksum and publication
          |-- online/offline replica management
                  |
          +-------+-------------------+
          |                           |
          v                           v
    Encrypted online vault      Rotated encrypted offline media
    fast recovery copy          disconnected recovery copy

The online and offline copies are ArtifactReplica identities inside one LocalWorkstation operation. A physical
destination is not the BackupSource.

## 7. Local protection topology

### 7.1 Online vault

The online vault is a dedicated encrypted volume intended for continuous WAL, scheduled backup publication, frequent
verification, and fast restore staging.

It must:

- reside on a different physical device from active PostgreSQL, ScyllaDB, and operating-system storage;
- use a dedicated stable mount root;
- remain inaccessible to normal Core and user identities;
- reserve capacity for active publication, retention rollover, and restore operations;
- reject publication if volume identity, encryption, filesystem, ACLs, or root layout differs from policy;
- support durable writes, large files, no-overwrite creation, and reliable enumeration;
- report device, filesystem, encryption, capacity, and health state; and
- never be treated as the only production recovery copy.

A separate partition on the same disk does not satisfy the physical-device requirement.

### 7.2 Offline media set

Production local protection uses at least two dedicated encrypted removable devices, normally identified as rotation
slots A and B. Three devices are preferred when one medium may be in transit, undergoing verification, or unavailable.

Each medium:

- has a unique MediaId unrelated to a drive letter;
- has its own encryption protector and recovery material;
- is normally powered off and disconnected;
- contains self-describing volume metadata, manifests, catalog entries, and complete retained dependency chains;
- is mounted only for a bounded operation;
- is verified before safe removal;
- is stored separately from the workstation and other active rotation medium; and
- has a recorded last verified, last restore-tested, and last disconnected time.

Two media stored beside the workstation do not provide meaningful site separation. The UI must distinguish
**Disconnected** from **StoredSeparately**; software can observe disconnection but cannot independently prove physical
custody.

### 7.3 Restore workspace

Restore uses a separate encrypted workspace with enough capacity for:

- archive or media hydration;
- complete PostgreSQL base and WAL chains;
- Scylla schema and SSTable sets;
- checksum read-back;
- native combination or reconstruction;
- diagnostic evidence; and
- one failed restore retained for analysis when policy requires it.

The restore workspace is temporary and never becomes a cataloged replica. Native restore tools do not mutate vault
files.

### 7.4 Optional additional copies

Additional local devices may be registered as replicas, but each must have a declared fault-boundary grade:

- **SameHostOnline**;
- **DetachedOffline**;
- **StoredSeparately**; or
- **ExternallyManaged**, which requires a separate future architecture.

Replica count does not substitute for fault-boundary grade.

## 8. Platform and filesystem architecture

### 8.1 Common filesystem requirements

A production vault filesystem must support:

- files larger than the maximum expected native artifact;
- atomic no-overwrite rename within one volume;
- explicit file-data and metadata durability operations;
- restrictive identities and ACLs;
- stable volume identity;
- filesystem consistency checking and recovery tooling;
- long path and Unicode-safe behavior;
- detection or rejection of links that escape the vault root;
- predictable free-space reporting; and
- safe read-only mounting for recovery.

Every supported platform has a durability adapter that converts a successful write into evidence only after the file
and required directory metadata are flushed, reopened, and verified. An operating-system copy-complete result alone is
not a publication boundary.

### 8.2 Windows baseline

Windows vaults use:

- BitLocker for fixed and removable data volumes;
- a stable volume GUID path as the primary operating-system identity;
- NTFS as the compatibility baseline;
- ReFS on supported fixed volumes only after native tools, integrity-stream behavior, removable-media workflow, and
  recovery-host compatibility are tested;
- dedicated service and retention identities; and
- safe removal or dismount before physical disconnection.

Drive letters are optional operator conveniences and are never authoritative. A device appearing as a previously used
drive letter does not make it an approved vault.

If ReFS is selected, file-data integrity streams and scrubber policy are explicitly configured and monitored. ReFS
metadata protection or integrity streams supplement, but do not replace, IFM cryptographic checksums and restore tests.

### 8.3 Linux baseline

Linux vaults use:

- LUKS2 over dm-crypt;
- XFS or ext4 after platform durability tests;
- filesystem and LUKS UUIDs for stable identity;
- restrictive ownership, mode, and optional ACL policy;
- explicit file and directory synchronization;
- read-only recovery mounts where possible; and
- independently protected LUKS header backups and recovery material.

LUKS header damage can make an otherwise intact volume inaccessible. Header backup identity and verification are part
of media readiness, but a header backup is never stored only on the volume it protects.

### 8.4 Platform portability

The manifest records operating system, filesystem type and version, encryption technology, native tool requirements,
and recovery compatibility. A Windows vault is restored through a compatible Windows recovery environment; a Linux
vault is restored through a compatible Linux recovery environment unless a cross-platform test has explicitly
qualified another path.

Portable meaning applies to IFM manifest semantics and artifact checksums, not automatic cross-platform volume access.

## 9. Volume and media identity

### 9.1 Identity components

A LocalVaultIdentity includes:

- EnvironmentId;
- BackupSource LocalWorkstation;
- logical replica identity;
- MediaId;
- rotation slot where applicable;
- operating-system volume GUID or filesystem UUID;
- encryption volume/protector identity;
- physical-device identity evidence available from the platform;
- filesystem type and format revision;
- vault schema version;
- expected capacity class;
- creation and enrollment time;
- signing identity; and
- lifecycle state.

No single hardware serial is trusted alone because USB bridges, enclosures, cloning, and device replacement can make it
missing or misleading.

### 9.2 Enrollment

Media enrollment is an explicit administrative ceremony:

1. verify the intended physical device and capacity;
2. erase or initialize it under approved media-handling procedure;
3. create the approved encrypted volume and filesystem;
4. create a unique MediaId and vault root;
5. apply access-control policy;
6. write a signed immutable enrollment record;
7. back up encryption recovery material independently;
8. run full write, flush, read-back, disconnect, reconnect, unlock, and identity tests;
9. run a disposable catalog publication and restore test; and
10. authorize the medium for a declared rotation slot and environment.

Cloning an enrolled volume does not create a second approved medium. A clone with duplicate MediaId is quarantined.

### 9.3 Mount validation

Before any operation, the processor:

- resolves the canonical volume identity;
- confirms encryption is active;
- verifies the signed enrollment record;
- validates MediaId, environment, replica role, filesystem, and mount options;
- confirms the configured vault root is on the expected volume;
- rejects symbolic links, junctions, reparse points, bind mounts, or path traversal that escape the root;
- validates access-control policy;
- checks free-space reserve and filesystem health; and
- acquires a fenced exclusive vault lease.

A wrong, unknown, duplicate, read-only, unhealthy, or policy-incompatible volume fails closed.

## 10. Local namespace and publication

### 10.1 Logical layout

Every vault is self-describing:

    ifm-vault/
      vault/
        enrollment/
        schema-v1/
          environments/{EnvironmentId}/
            protection-sets/{ProtectionSetId}/
              operations/{OperationId}/
                engines/{EngineId}/
                  artifacts/{ArtifactId}/{ArtifactVersionId}/{PartName}
                  native-manifest/{NativeManifestId}
                  ifm-engine-manifest/{ManifestId}
                  verification/{VerificationId}
                backup-set/{BackupSetId}/manifest/{ManifestId}
                publication/{PublicationId}/commit
              catalog/entries/{UtcDate}/{BackupSetId}/{CatalogEntryId}
              recovery-records/{RecoveryOperationId}/{RecordId}
      incoming/{OperationId}/
      diagnostics/{OperationId}/

Actual platform separators differ, but manifest paths use a normalized relative representation. Absolute workstation
paths never enter actor events or portable manifests.

### 10.2 No-overwrite identity

Local filesystems do not supply S3 version IDs. The architecture therefore requires:

- globally unique ArtifactVersionId and ManifestId values;
- creation that fails if the final relative path already exists;
- no mutable **latest** artifact;
- no content replacement under a published identity;
- exact length and cryptographic digest in the manifest; and
- a signed publication record referencing every exact relative path.

An existing path with different content is a security and integrity incident, not an overwrite opportunity.

### 10.3 Durable publication sequence

Publication follows:

1. Validate volume identity, lease, capacity, encryption, ACLs, and policy revision.
2. Create an operation-specific incoming directory using no-follow path handling.
3. Write each artifact under a temporary unique name.
4. Flush file data and metadata using the supported platform adapter.
5. Reopen and verify length and cryptographic digest.
6. Move each verified file to a unique final path on the same volume without replacement.
7. Flush affected directory metadata.
8. Write and verify the native manifest.
9. Write and verify the signed IFM engine and backup-set manifests.
10. Write the signed publication commit file last.
11. Flush, reopen, and verify the commit.
12. Write a unique signed append-only catalog entry.
13. Report publication only after a post-publication scan resolves every referenced file.

A restore point is visible only when its valid commit and catalog entry resolve a complete manifest chain. Directory
existence, rename success, or catalog text alone is insufficient.

### 10.4 Crash recovery

After restart, the service scans:

- operation journals;
- incoming directories;
- final files without a commit;
- commits without catalog entries;
- catalog entries with invalid references; and
- stale vault leases.

Uncommitted final files remain ineligible and follow diagnostic cleanup policy. The service may safely finish
publication only when the journal, policy revision, file digests, and complete manifest graph prove that doing so is
idempotent. It never guesses after an interrupted native capture.

## 11. Local access control

### 11.1 Identities

The local design separates:

| Identity | Capability | Explicit exclusions |
| --- | --- | --- |
| Backup service writer | Create and verify files only inside an accepted operation's incoming root | No write, deletion, or ownership authority in published roots |
| Publication identity | Move verified files into unique no-replace final paths, harden ownership/ACLs, and write signed manifests, commits, and catalog entries | No overwrite, published-version deletion, retention planning, or vault administration |
| Verification reader | Read exact published files and metadata | No publish, delete, ACL, encryption, or media enrollment |
| Retention planner | Enumerate manifests, dependency graph, capacity, and retention state | No deletion |
| Retention executor | Delete exact expired paths from an approved plan | No wildcard sweep, media enrollment, or encryption administration |
| Media operator | Attach, unlock, verify, safely dismount, and store the expected medium | No policy or backup-state mutation outside approved commands |
| Recovery operator | Read catalog and artifacts and unlock approved media during restore | No routine backup execution or retention deletion |
| Vault administrator | Enroll media and maintain filesystem/ACL policy | No ordinary application role |

On a single-user workstation these may ultimately map to the same human administrator, but separate service tokens,
process identities, elevation steps, and audit records remain required.

### 11.2 ACL limitation

ACLs protect against ordinary application bugs and least-privilege compromise. A local administrator, root user,
physical attacker, kernel compromise, or storage firmware can bypass them. Therefore:

- the online vault is never described as immutable against workstation administration;
- detached media is required for stronger isolation;
- encryption protects powered-off media, not an unlocked mounted vault;
- critical audit evidence should be exported or copied offline; and
- AWS remains the stronger reference durability boundary.

### 11.3 Path safety

All filesystem operations use allowlisted roots and normalized relative manifest paths. The service rejects:

- parent traversal;
- alternate data streams where not explicitly supported;
- symbolic links, hard-link surprises, junctions, reparse points, or bind mounts that violate policy;
- device paths outside the enrolled volume;
- user-controlled command arguments;
- case-folding collisions;
- reserved names;
- duplicate normalized paths; and
- changes in volume identity during an operation.

## 12. Encryption and recovery keys

### 12.1 Volume encryption

All online and offline vault volumes are encrypted at rest:

- Windows uses BitLocker or BitLocker To Go as appropriate;
- Linux uses LUKS2 over dm-crypt; and
- restore workspace volumes use the same platform baseline.

Unencrypted local media is not an eligible LocalWorkstation replica.

### 12.2 Unlock policy

The online vault may use controlled automatic unlock only when:

- the operating-system volume is itself protected;
- an independently escrowed recovery method exists;
- service identity and mount policy remain restrictive; and
- threat review accepts that workstation compromise can access the mounted vault.

Offline media does not use unattended automatic unlock. It is unlocked only for the bounded rotation or recovery
window, then safely dismounted and disconnected.

### 12.3 Recovery material

Recovery passwords, recovery keys, LUKS key material, LUKS header backups, and administrator secrets:

- are never stored only on the protected workstation;
- are never stored on the media they unlock as the sole copy;
- never enter actor messages, manifests, paths, logs, metrics, traces, or UI models;
- are accessible through an independently controlled recovery procedure;
- have at least two protected copies where policy requires;
- are tested during drills; and
- have access and rotation audit.

For a personal workstation, an approved password manager plus a separately stored sealed recovery copy may satisfy the
independence requirement only after explicit review. The design does not assume enterprise directory services exist.

### 12.4 Manifest signing

Local engine manifests, backup-set manifests, publication commits, catalog entries, media seals, and recovery records
are signed by an asymmetric key controlled by the Database Backup Service security boundary.

The signing design requires:

- private-key storage in an approved operating-system protected or hardware-backed key provider;
- no private signing key on backup media;
- a public-key trust bundle on every vault and in the independent recovery kit;
- key identity, algorithm, validity, and rotation history in signed metadata;
- continued verification of artifacts signed by retired but trusted keys;
- separate authorization for signing and retention deletion; and
- an audited key-replacement procedure after workstation loss.

Signature verification must work during break-glass recovery without Core, NATS, or access to the original workstation.
A signature proves publisher identity and manifest integrity; it does not replace artifact checksums or native restore
testing.

### 12.5 Artifact encryption

Volume encryption is the baseline. Portable per-artifact envelope encryption may be introduced later if a requirement
exists to move artifacts outside approved encrypted volumes. It must use a reviewed cryptographic format, independent
key recovery, streaming operation, authenticated integrity, and manifest compatibility.

The service never invents custom cryptography merely to imitate SSE-KMS.

### 12.6 Media retirement

Ordinary file deletion does not prove data erasure on SSD or flash devices. Retirement uses one of:

- verified destruction of all encryption key protectors followed by approved device sanitization;
- device-supported secure erase under a documented procedure; or
- physical destruction for failed or high-sensitivity media.

The retirement record preserves MediaId, authorization, method, date, and operator identity without retaining secret
key material.

## 13. Native database boundary

The same native formats and consistency boundaries used by AWS apply locally:

- PostgreSQL physical base backup and continuous WAL;
- Scylla Manager coordinated cluster backup;
- engine-native manifests and version evidence;
- application checkpoint coordination; and
- native plus application validation.

The LocalWorkstation processor owns local destination behavior. It does not change database consistency merely because
the destination is faster or physically nearby.

The processor consumes the same high-level, allowlisted engine capability interfaces as the AwsCloud processor:

| Capability | Local implementation boundary |
| --- | --- |
| PostgreSQL backup/restore | Preflight, start base backup, observe status, verify manifest, manage WAL recovery range, restore to a fresh target, validate, and cancel through a typed capability backed by the PostgreSQL replication protocol and supported native utilities such as `pg_basebackup` and `pg_verifybackup` |
| ScyllaDB backup/restore | Preflight, start/observe/cancel cluster backup, restore schema before data, start/observe/cancel data restore, and validate node/token coverage through a typed capability backed primarily by the Scylla Manager REST/Swagger API |

PostgreSQL does not expose a general backup REST API, so its native protocol and utilities remain private implementation
details of the capability adapter. Scylla Manager's structured REST API is preferred; if `sctool` is required for a
supported operation, it is hidden behind the same interface and allowlist. Actor messages cannot carry executable
names, arbitrary arguments, raw SQL/CQL, database credentials, or host filesystem paths.

## 14. PostgreSQL local backup design

### 14.1 Base backup

The base-backup workflow:

1. consumes the common execution event with BackupSource **LocalWorkstation**;
2. validates the PostgreSQL replication identity, versions, vault, lease, capacity, and WAL health;
3. captures one physical cluster backup through the native backup interface;
4. writes through bounded encrypted staging or directly into the online vault incoming area;
5. preserves the PostgreSQL native manifest;
6. calculates IFM cryptographic digests;
7. durably publishes artifacts and manifests;
8. performs native verification;
9. publishes the commit and catalog entry;
10. emits the common source-independent service events with BackupSource **LocalWorkstation**; and
11. queues policy-required offline replication when the expected medium is available.

Incremental PostgreSQL base backups are permitted only under the same measured compatibility and dependency rules as
AWS.

### 14.2 Continuous WAL

The PostgreSQL archiver sends completed WAL to authenticated local service ingress without local-vault path authority.
The service:

- validates segment and timeline identity;
- writes a unique incoming file;
- flushes, reopens, and verifies it;
- publishes it to the online vault;
- updates the durable WAL watermark; and
- emits bounded health and RPO observations.

Normal archive acknowledgement occurs only after the WAL segment is durable and verified in the online vault. Offline
media is not required for each archive acknowledgement because it is intentionally disconnected. The UI therefore
shows two separate ages:

- **OnlineLocalRpoAge**; and
- **OfflineLocalRpoAge**.

An online vault outage creates visible WAL backpressure and database-capacity risk. The service never silently
acknowledges an unprotected segment.

### 14.3 Offline PostgreSQL set

An offline PostgreSQL restore point includes on one medium:

- a complete eligible base or incremental chain;
- the complete WAL range needed for its advertised PITR window;
- native and IFM manifests;
- commit and catalog records;
- verification evidence; and
- compatible recovery-tool metadata.

The catalog must not advertise PITR beyond the WAL actually sealed on that medium.

### 14.4 PostgreSQL restore

Restore:

1. selects an exact online or offline replica;
2. mounts the source read-only where supported;
3. validates media, catalog, signatures, and dependency chain;
4. copies exact files to the encrypted restore workspace;
5. verifies every digest;
6. combines incremental backups where applicable;
7. restores to a fresh PostgreSQL volume;
8. applies WAL to the selected target;
9. validates system identifier, timeline, schemas, extensions, event store, sequences, and application checkpoint; and
10. stops at **ReadyForCutover** for production.

## 15. ScyllaDB local backup design

### 15.1 Capture model

Scylla Manager coordinates multi-node production capture. The LocalWorkstation processor provides an allowlisted
service-controlled local target or staging boundary and verifies:

- required live nodes;
- token-range coverage;
- schema capture;
- expected keyspaces and tables;
- snapshot tag;
- Scylla and Manager versions;
- native manifest completeness; and
- application checkpoint.

### 15.2 Local publication

Scylla Manager output is never cataloged in place merely because its native task completed. The processor:

1. reconciles the complete native layout;
2. computes IFM artifact identities and digests;
3. copies or moves data through the durable publication sequence;
4. writes the IFM Scylla engine manifest;
5. verifies the published online replica;
6. writes commit and catalog evidence; and
7. cleans native staging only after policy permits.

Scylla Manager retention cannot independently delete IFM-cataloged dependencies.

### 15.3 Offline Scylla set

Every advertised offline Scylla restore point contains:

- schema artifacts;
- complete required SSTable and incremental dependencies;
- native manifests;
- topology and datacenter mapping evidence;
- all IFM manifests, commits, and catalog records; and
- verification evidence.

Required data is not split across rotation media. A medium lacking any dependency may retain diagnostic data but cannot
advertise the restore point as eligible.

### 15.4 Scylla restore

Restore:

1. validates media and complete manifest graph;
2. copies exact artifacts into the encrypted restore workspace;
3. reconstructs the Scylla Manager-compatible layout;
4. verifies every digest;
5. provisions a fresh compatible cluster;
6. restores schema first;
7. restores table data using the approved Manager method;
8. validates topology, ownership, schema agreement, and data;
9. rebuilds excluded views and indexes;
10. performs application and coordinated-checkpoint validation; and
11. stops at **ReadyForCutover** for production.

## 16. Coordinated backup sets

A coordinated PostgreSQL and Scylla backup set retains the same ApplicationCheckpoint model as AWS. For
LocalWorkstation:

- each engine operation carries BackupSource **LocalWorkstation**;
- one BackupSetId links the engine operations;
- the online vault must contain complete eligible engine points;
- an offline replica is eligible only when the same medium contains every required engine dependency;
- JetStream checkpoint relationships remain separately declared; and
- validation proves cross-store compatibility before whole-application recovery is advertised.

Copying PostgreSQL to one offline disk and ScyllaDB to another does not create one eligible coordinated offline set.

## 17. Offline rotation architecture

### 17.1 Rotation states

Each medium moves through:

    Enrolled -> Expected -> Attached -> IdentityValidated -> Unlocked
             -> Synchronizing -> Verifying -> Sealed -> Dismounting
             -> Disconnected -> StoredSeparately

Failure states include **WrongMedia**, **IdentityConflict**, **EncryptionUnavailable**, **InsufficientCapacity**,
**FilesystemUnhealthy**, **CopyFailed**, **VerificationFailed**, and **UnsafeRemoval**.

Software can prove successful dismount and subsequent absence. **StoredSeparately** requires an authorized custody
attestation or operational record; it is not inferred from device absence.

### 17.2 Rotation workflow

1. ScheduledTask, an authorized UI caller, or an authorized Console caller submits a common DatabaseBackup command. A
   backup command may require the offline replica, while verification or reconciliation may repair an already captured
   eligible artifact set.
2. The LocalWorkstation processor identifies the expected rotation slot and waits without blocking actor threads.
3. The UI asks the operator to attach the specific MediaId.
4. The service validates physical, volume, encryption, filesystem, enrollment, and environment identity.
5. The operator unlocks the medium through the approved mechanism.
6. The service obtains a fenced exclusive lease.
7. It calculates a capacity- and dependency-safe copy plan.
8. It copies complete restore sets using incoming paths and durable publication.
9. It verifies all copied files, manifests, commits, and catalogs.
10. It produces a signed media-seal record and bounded common service event.
11. It flushes, dismounts, and reports when safe physical removal is permitted.
12. The operator disconnects and stores the medium separately.

The service never prompts an operator to remove media while writes, filesystem metadata, encryption updates, or
verification reads remain outstanding.

### 17.3 Self-contained media

An offline medium is self-contained for every restore point it advertises. Retention and copy planning may use
incremental transfer to avoid recopying unchanged files, but the resulting medium must physically contain the complete
dependency graph.

No eligible recovery chain requires simultaneous access to A and B media.

### 17.4 Rotation lateness

Policy defines maximum:

- time since successful offline synchronization;
- time since full media verification;
- time since separate-storage attestation; and
- time since an offline-only restore drill.

Exceeding any threshold changes LocalWorkstation recovery readiness and alerts independently from online backup success.

## 18. Restore-source selection

The service selects a local replica explicitly:

1. enumerate destination-resident catalog entries;
2. validate media enrollment and identity;
3. validate manifest and commit signatures;
4. resolve exact dependency paths;
5. confirm encryption recovery access;
6. verify files, lengths, and digests;
7. compare replica age and measured retrieval time with recovery policy;
8. prefer online only when the workstation and online vault are trusted;
9. prefer offline media when ransomware, accidental deletion, online corruption, or host compromise is suspected; and
10. record the selected MediaId, replica grade, and reason.

The service never silently mixes files from online and offline replicas. A deliberate repair plan may do so only by
creating and verifying a new signed recovery plan that names every source artifact.

## 19. Break-glass recovery

### 19.1 Independence

Break-glass local recovery works without:

- Core;
- NATS;
- PostgreSQL;
- ScyllaDB;
- the SystemAdmin read models;
- the online vault; or
- the original workstation.

### 19.2 Recovery kit

The independently stored recovery kit contains:

- signed recovery tooling and hashes;
- supported manifest schemas;
- signing public-key trust history;
- media inventory and rotation-slot records;
- BitLocker or LUKS recovery procedure;
- protected access to required recovery material;
- compatible recovery operating-system requirements;
- PostgreSQL and Scylla tool compatibility matrix;
- fresh-target infrastructure templates;
- minimum validation runbook; and
- escalation and authorization contacts.

The kit does not contain an unprotected encryption secret.

### 19.3 Break-glass sequence

1. Establish recovery authorization and external audit record.
2. Prepare a trusted compatible recovery workstation.
3. Attach one selected offline medium without automatic execution.
4. verify device and encryption identity before unlock.
5. Unlock and mount read-only where supported.
6. Validate catalog, manifests, signatures, commits, files, and digests.
7. Copy the selected complete set to an encrypted restore workspace.
8. Provision fresh PostgreSQL and ScyllaDB targets.
9. Restore through the normal native workflows.
10. Run native and minimum application validation.
11. Start recovered Core only after validation.
12. Import a signed recovery record into SystemAdmin after availability returns.
13. Require separate production cutover approval.

### 19.4 Recovery record

The signed record includes RecoveryOperationId, MediaId, rotation slot, vault schema, artifact versions, manifest IDs,
operator and authorization identity, recovery host, tool versions, measured RPO/RTO, validation results, deviations,
and cutover decision.

## 20. Local manifest and catalog

### 20.1 Shared schema

Local manifests contain every common field plus:

- BackupSource LocalWorkstation;
- logical replica and MediaId;
- normalized relative paths;
- ArtifactVersionId;
- operating system and filesystem;
- encryption technology and non-secret protector reference;
- volume identity;
- durable-publication evidence;
- media rotation and seal revision;
- replica fault-boundary grade;
- online or offline availability;
- last full media verification;
- bounded final `DatabaseRecoveryRunStats` summary and statistics schema revision;
- storage and retrieval compatibility; and
- local capability limitations.

They do not contain absolute paths, drive letters as identity, unlock secrets, recovery passwords, raw device commands,
or arbitrary process arguments.

### 20.2 Catalog

Every vault has append-only unique catalog entries derived from immutable manifests. A generated current index may
accelerate UI and recovery listing, but it is disposable.

Catalog reconstruction:

1. scans signed publication commits;
2. resolves manifests;
3. validates exact relative paths and digests;
4. reconstructs dependency and restore-point indexes;
5. compares them with append-only catalog entries;
6. identifies orphan, incomplete, or conflicting files; and
7. never promotes an incomplete operation.

### 20.3 Offline seal

A media-seal record summarizes:

- MediaId and rotation revision;
- catalog root or digest set;
- included eligible restore points;
- dependency completeness;
- bytes and file count;
- filesystem and encryption health;
- verification result;
- start and completion time;
- safe-dismount result; and
- signing identity.

The seal does not freeze future append-only generations. A later synchronization creates a new seal revision.

## 21. Retention and capacity

### 21.1 Retention planning

SystemAdmin authorizes retention through the common source-scoped commands. The LocalWorkstation processor creates an
exact plan that protects:

- PostgreSQL base, incremental, and WAL chains;
- Scylla schema, snapshot, SSTable, and incremental dependencies;
- active backup, restore, drill, and verification inputs;
- coordinated backup sets;
- legal hold;
- the latest restore-tested set;
- required online and offline replicas independently;
- media-seal and catalog evidence; and
- minimum rollback reserve.

### 21.2 Online deletion

Online deletion:

- requires an approved revision-matched plan;
- uses the separate retention identity;
- revalidates every exact path immediately;
- rejects links and root escapes;
- never performs an unbounded recursive sweep;
- stops on dependency, hold, lease, identity, or policy mismatch; and
- emits the common retention result event with BackupSource **LocalWorkstation**.

### 21.3 Offline deletion

An offline plan remains pending until the exact MediaId is attached and validated. The service does not mark retention
complete merely because a catalog says a file should expire.

Offline cleanup preserves self-contained restore sets. If removing shared files would break a retained set, the files
remain until all dependents expire or the retained set is republished self-contained.

### 21.4 Capacity policy

Each vault reserves space for:

- the largest expected active capture;
- publication rollover;
- WAL surge;
- verification workspace;
- retention lag;
- filesystem overhead;
- diagnostic evidence;
- one complete protected recovery set; and
- restore workspace where the volume is explicitly assigned that role.

Thresholds produce forecast, warning, critical, and admission-stop states. Retention pressure cannot silently delete
protected history.

### 21.5 Media replacement

Replacement creates a new MediaId. Required restore sets are copied and fully verified before the old medium leaves
rotation. Identity is never transferred by copying the enrollment file.

## 22. Verification and restore drills

### 22.1 Verification levels

Local verification includes:

1. **Write verification**: durable platform writes completed without error.
2. **File verification**: exact length and cryptographic digest match.
3. **Publication verification**: manifests, commit, and catalog resolve after reopen.
4. **Volume verification**: MediaId, encryption, filesystem, ACL, mount, and health match policy.
5. **Dependency verification**: complete base/WAL or schema/SSTable graph exists.
6. **Offline verification**: medium reconnects, unlocks, and verifies after safe removal.
7. **Native verification**: database-native tools accept the artifact set.
8. **Engine restore verification**: a fresh engine restores and starts.
9. **Application verification**: event store, sequences, schemas, projections, and checkpoint pass.
10. **Coordinated-set verification**: PostgreSQL and Scylla state is compatible.

### 22.2 Filesystem integrity

Filesystem checks, ReFS integrity streams, scrubbers, SMART or device-health data, and operating-system diagnostics are
supporting signals. They never replace cryptographic digest verification.

An unreadable sector, repaired corruption, checksum mismatch, unexpected remount, or device-health critical condition
degrades the affected replica until full revalidation.

### 22.3 Drill schedule

Policy schedules:

- online-vault metadata and digest sampling;
- periodic full online verification;
- media reconnect and unlock tests;
- full offline-media digest verification;
- PostgreSQL random-target PITR;
- Scylla fresh-cluster restore;
- coordinated application restore;
- recovery using only one offline medium;
- recovery on a different compatible workstation;
- loss-of-key and wrong-media exercises; and
- power-loss or removal fault injection on disposable test media.

### 22.4 Drill evidence

Drills record catalog discovery, media retrieval, unlock, copy, checksum, native restore, application validation, total
RTO, achieved RPO, throughput, device health, operator steps, failures, and corrective actions.

## 23. SystemAdmin and service integration

### 23.1 Common contracts

Local processing uses the exact DatabaseBackup command, query, domain-event, execution-event, service-event, and
translated-command types defined by the overview. Every source-bound event carries BackupSource
**LocalWorkstation**.

No LocalDatabaseBackupCompletedEvent, LocalRestoreProgressEvent, or similar local-only type is introduced.

### 23.2 Replica observations

The processor reports local behavior through common events:

| Local observation | Common service event |
| --- | --- |
| Operation admitted or rejected | DatabaseBackupServiceAcceptedEvent or DatabaseBackupServiceRejectedEvent |
| Online or offline replica changed lifecycle state | DatabaseBackupArtifactReplicaUpdatedEvent |
| Mount, encryption, filesystem, capacity, or media problem | DatabaseBackupServiceErrorEvent |
| Verification completed | DatabaseBackupVerificationCompletedEvent |
| Restore validation completed | DatabaseRestoreValidationCompletedEvent |
| Fresh production target is valid | DatabaseRestoreReadyForCutoverEvent |
| Retention plan or execution changed | Common retention plan/completion/failure events |
| Source capability or expected medium changed | DatabaseBackupServiceCapabilityChangedEvent |
| Journal and vault state reconciled | DatabaseBackupServiceReconciliationEvent |
| Bounded phase or final run measurements captured | DatabaseRecoveryRunStatisticsCapturedEvent |

The service response echoes LocalWorkstation, OperationId, sequence, MediaId or logical replica where applicable, and
the producing host identity. The DatabaseBackup Event Actor rejects a source mismatch.

### 23.3 Progress limits

Durable progress is emitted for phase, aggregate byte threshold, replica transition, media transition, verification
level, restore milestone, or policy heartbeat. It is not emitted per file, WAL segment, SSTable, buffer, or filesystem
operation.

### 23.4 SystemAdmin projections and local evidence reconciliation

`SystemAdminDbContext` stores the shared operation, phase, restore-point, replica, structured error, health, and
`DatabaseRecoveryRunStats` projections in `CorePostgresCluster`. The LocalWorkstation processor has no connection to
that context. A bounded statistics summary reaches it only through the shared service event, Event Actor translation,
Command Actor domain event, and idempotent projector path.

After a local restore, SystemAdmin projection tables are replayed from restored domain events. The reconciliation
workflow then validates any newer online-vault or offline-media manifests, catalog entries, media seals, final run
summaries, and signed recovery records. Accepted evidence becomes a reconciliation command and new domain event before
it appears in `SystemAdminDbContext`; local files never update projection tables directly.

## 24. UI, Console, and ScheduledTask integration

### 24.1 UI

The UI uses common DatabaseBackup commands and queries with RequestOrigin **Ui** and reacts to the authorized bounded
DatabaseBackup domain events shared with the Console. Local views show:

- LocalWorkstation source health;
- online vault identity alias and fault-boundary grade;
- expected and attached MediaId aliases;
- encryption and filesystem readiness without secrets;
- capacity and forecast;
- online and offline RPO age;
- last safe dismount and separately stored attestation;
- latest verified and restore-tested set;
- rotation overdue state;
- restore-source choice and expected media;
- structured capability limitations; and
- approval and audit state.

The UI never accepts a raw arbitrary backup path, displays recovery secrets, or consumes execution-intent or raw
service-response events.

### 24.2 Database Backup Console

The Console uses the same commands and queries with RequestOrigin **Console**, listens to the same authorized bounded
domain events as the UI, and uses OperationId to follow or cancel work. It may support interactive and script-friendly
output, deterministic exit codes, and query-based recovery after a disconnected session. It does not consume
execution-intent or raw service-response events and does not invoke PostgreSQL tools, Scylla Manager, vault adapters,
or the Database Backup Host directly. The normal Console is distinct from the independently secured break-glass
recovery kit.

### 24.3 ScheduledTask

ScheduledTask actors use the same queries and commands with RequestOrigin **ScheduledTask**. They may trigger:

- PostgreSQL base backup;
- backup verification;
- Scylla backup;
- coordinated backup set;
- retention evaluation;
- restore drill;
- a source-scoped backup whose policy requires an offline replica;
- verification or reconciliation that repairs an incomplete offline replica; and
- media-health or reconciliation operation.

These are behaviors authorized through the common DatabaseBackup contracts; no local-only replication command or event
type is introduced.

An unavailable offline medium produces a bounded waiting, skipped, or policy-violation outcome rather than blocking an
actor thread. ScheduledTask does not consume raw local service events; future feature-level completion events remain
outside this document.

## 25. Reliability and failure behavior

### 25.1 Operation journal

The restart-safe journal records:

- accepted event and LocalWorkstation source;
- operation, policy, correlation, and fencing identity;
- expected vault and MediaId;
- native process and consistency boundary;
- incoming and final relative paths;
- file lengths and digests;
- durable-write and read-back state;
- manifest, commit, and catalog state;
- copy and verification checkpoints;
- media lease and rotation phase;
- outbound service-event sequence and acknowledgement state;
- bounded phase/final run statistics awaiting Core acknowledgement;
- journal schema revision and last reconciliation state; and
- cleanup eligibility.

The journal is outside the protected databases. It must not be stored only on the removable medium currently being
written or in the container writable layer. The initial LocalWorkstation implementation uses a transactional embedded
journal database, preferably SQLite configured for durable commits, on an encrypted persistent Docker volume or
validated encrypted bind-mounted volume. The volume is separate from active PostgreSQL/Scylla data and removable
backup media. The single Database Backup Host writer owns journal migrations and locking; actors and clients never open
the file.

Loss of this journal makes incomplete work non-resumable but does not invalidate a committed vault manifest. On journal
loss, the processor reconstructs published evidence from manifests and catalogs, fails or quarantines ambiguous
incomplete work, and reconciles through the common actor flow. It never infers completion from files alone.

### 25.2 Failure matrix

| Failure | Required behavior |
| --- | --- |
| Wrong BackupSource | Reject before journal admission |
| Wrong or unknown media | Quarantine and request expected MediaId |
| Duplicate MediaId | Reject both until an administrator resolves enrollment integrity |
| Drive-letter or mount change | Resolve stable identity; never redirect by convenience path |
| Volume identity changes mid-operation | Fence operation and fail safely |
| Encryption locked | Wait or reject with structured operator action; never request secret through actor event |
| Recovery key unavailable | Mark replica non-recoverable and alert critically |
| Filesystem read-only | Stop mutation; preserve readable evidence |
| Filesystem corruption | Quarantine replica, run approved diagnostics, and recover from another verified copy |
| Device health critical | Stop new writes when safe and prioritize verified replacement |
| Capacity reserve breached | Reject new capture or pause safely; preserve WAL risk visibility |
| Power loss during write | Reconcile journal, incoming files, flush evidence, commit, and catalog |
| Media removed during write | Mark unsafe removal, invalidate incomplete publication, and require full verification |
| Partial file or checksum mismatch | Never publish; recopy from a verified source |
| Commit missing | Operation remains invisible and ineligible |
| Catalog missing | Reconstruct from valid commits and manifests |
| Catalog points to missing file | Restore point is ineligible and replica degraded |
| Online vault device fails | Restore or rebuild from current offline or AWS copy |
| Workstation lost | Use offline break-glass recovery on another compatible workstation |
| Ransomware suspected | Do not attach clean offline media to the suspect host; recover in an isolated environment |
| Offline rotation overdue | Keep online success but mark offline RPO violation |
| Expected medium unavailable | Leave operation pending/skipped according to policy and alert |
| Service restart | Reconcile journal, process, vault lease, paths, and last service sequence |
| Duplicate or reordered event | Apply idempotency and sequence reconciliation |
| Native database unavailable | Fail preflight or stop at native safe boundary |
| PostgreSQL WAL gap | Limit eligible PITR range and alert |
| Incomplete Scylla node/token coverage | Do not publish an eligible cluster restore point |
| Restore target incompatible | Reject before mutation |
| Validation failure | Keep target isolated and prohibit cutover |
| Cutover failure | Preserve old and restored targets and enter approved rollback |
| Retention race with restore | Restore lease and manifest references fence deletion |
| Core unavailable after dispatch | Continue only to approved safe boundary and reconcile later |
| Complete local loss | Recover from AWS if available; local-only policy declares unrecoverable site risk |

### 25.3 Ransomware procedure

When compromise is suspected:

- stop automatic media rotation;
- do not unlock or attach known-clean offline media to the affected host;
- preserve logs and online evidence without trusting it;
- prepare a separate clean recovery workstation;
- select a pre-incident offline restore point;
- verify signatures and checksums before restore; and
- require security review before cutover.

## 26. Security architecture

### 26.1 Threat boundaries

The design addresses accidental deletion, ordinary service compromise, filesystem corruption, stolen powered-off media,
online ransomware, wrong-media use, and workstation loss.

It cannot fully resist a malicious local administrator with access to all recovery keys, a compromised kernel while
media is unlocked, destructive firmware, fire affecting all co-located media, or an operator who never performs
rotation. AWS or separately managed immutable infrastructure remains necessary for stronger protection.

### 26.2 Secret handling

Secrets never appear in:

- DatabaseBackup events or queries;
- manifests or catalog entries;
- filenames or volume labels;
- command-line arguments where avoidable;
- service logs, traces, metrics, or UI;
- media custody records; or
- diagnostic bundles.

### 26.3 Malware and autorun

Recovery and rotation workstations disable automatic execution from removable media. The service treats vault contents
as untrusted input until path, manifest, signature, schema, and digest validation succeeds.

Native tools consume only validated files from the restore workspace, not arbitrary removable-media paths.

### 26.4 Audit integrity

Signed manifests, commits, catalog entries, seals, and recovery records provide portable evidence. OS security and
service logs are periodically exported to another approved destination because logs stored only on the workstation do
not survive its loss.

## 27. Observability

### 27.1 Metrics

Required metrics include:

- online vault availability and write readiness;
- attached and expected MediaId;
- encryption lock state;
- filesystem type, error state, and read-only transitions;
- free, reserved, incoming, published, and diagnostic bytes;
- online and offline copy throughput;
- file and checksum failures;
- incomplete incoming age;
- latest online WAL and backup boundary;
- OnlineLocalRpoAge and OfflineLocalRpoAge;
- last media attach, verification, seal, dismount, and separate-storage attestation;
- rotation overdue duration;
- device-health signals where available;
- latest verified and restore-tested ages;
- restore copy and native recovery throughput;
- measured RPO/RTO; and
- retention and replacement forecast.

High-cardinality path and file details remain logs or journal data, not metric labels.

### 27.2 Alerts

Alerts cover:

- online vault unavailable or wrong;
- offline rotation overdue;
- expected medium not attached;
- wrong or duplicate media;
- encryption disabled or recovery material unverified;
- filesystem or checksum corruption;
- unsafe removal;
- capacity reserve threatened;
- PostgreSQL WAL backpressure;
- Scylla cluster incompleteness;
- no current self-contained offline set;
- no restore-tested local set;
- ACL or vault-root drift;
- unexpected administrator mutation;
- device-health failure;
- key or header-backup loss; and
- break-glass recovery activation.

### 27.3 Health endpoints

HTTP health exposes bounded capability only:

- LocalWorkstation processor ready;
- online vault identity valid;
- expected offline slot state;
- encryption and filesystem ready;
- capacity above hard reserve;
- journal writable;
- native tools available; and
- effective policy revision.

It exposes no absolute paths, media secrets, recovery keys, usernames, or unrestricted inventory.

## 28. Performance and capacity

### 28.1 Resource isolation

Local backup can contend with databases for CPU, memory, PCIe lanes, USB controllers, storage caches, and power. The
service has separate limits for:

- native capture;
- concurrent file copies;
- read and write buffers;
- checksum workers;
- online and removable-device bandwidth;
- WAL ingress;
- verification scans;
- restore hydration; and
- filesystem operations.

The independently constrained Database Backup Host protects actor/API responsiveness and allows backup CPU, memory,
I/O, and lifecycle limits to be applied without sharing the Api.Server process.

### 28.2 Physical topology

The online vault should use a different physical device and, where practical, a different controller path from active
database storage. A removable medium's measured sustained write and read rates, not its advertised interface speed,
determine rotation and recovery time.

USB hubs, thermal throttling, write-cache behavior, power management, and enclosure bridges are part of qualification.

### 28.3 Benchmark evidence

Representative benchmarks measure:

- PostgreSQL base capture while the application is active;
- WAL burst ingestion;
- Scylla snapshot publication;
- concurrent checksum cost;
- online copy throughput;
- offline rotation throughput;
- full-media verification;
- reconnect and unlock time;
- restore workspace hydration;
- native restore; and
- application validation.

Tests report database latency impact, actor/API responsiveness, CPU, memory, I/O queue, throughput, and power-loss
recovery behavior.

### 28.4 Capacity forecasting

Forecasts include database growth, WAL volatility, SSTable churn, complete dependency chains, incoming duplication,
rotation delay, diagnostic retention, filesystem overhead, offline self-contained sets, and restore workspace.

## 29. Configuration and deployment composition

### 29.1 Core-owned configuration

Core owns:

- the `SystemAdminDbContext` projection connection, schema version, migration policy, and projector checkpoint policy;
- LocalWorkstation enablement;
- protection sets and classifications;
- recovery objectives;
- online and offline replica requirements;
- schedule bindings;
- verification and drill policy;
- retention classes;
- acceptable capability grade; and
- authorization policy.

### 29.2 Service-owned configuration

The service owns validated non-domain configuration:

- supported platform;
- expected online volume and MediaId;
- rotation slots and expected MediaIds;
- allowlisted vault roots;
- filesystem and encryption requirements;
- stable identity providers;
- journal and restore-workspace location;
- journal provider, schema revision, encryption/durability settings, and compaction policy;
- buffer, bandwidth, checksum, and concurrency limits;
- capacity reserves;
- native database endpoint or agent path;
- health and audit integration; and
- break-glass tooling configuration.

Configuration contains secret references, not recovery secrets.

### 29.3 Database Backup Host resource

From the first implementation, standalone **TomasAI.IFM.Api.DatabaseBackup.Host** receives:

- its own process and service identity;
- exclusive local vault permissions;
- independent CPU, memory, and I/O limits;
- direct removable-media observation;
- source-independent NATS contracts used identically by UI, Console, and ScheduledTask callers;
- independent deployment and restart;
- local health and telemetry; and
- no normal application database credentials.

Development and functional paper-trading tests start the Worker and disposable dependencies explicitly without Aspire.
After functional gates pass, the same executable is packaged in an Ubuntu 24.04/.NET 10 container and qualified with
persistent journal, vault, media, and restore-workspace mounts. Aspire composition, shared Service Defaults, and the
full production OpenTelemetry deployment are deferred to a separate full-system Linux production migration plan.
Neither Docker nor Aspire owns authoritative operation state.

## 30. Environment and test strategy

### 30.1 Unit and contract tests

Tests cover:

- BackupSource LocalWorkstation routing;
- rejection of AwsCloud events by the local processor;
- shared event schema compatibility;
- idempotent `SystemAdminDbContext` operation, phase, error, replica, and run-stat projection;
- projection rebuild from SystemAdmin domain events;
- durable local journal restart, duplicate event, unacknowledged statistics replay, and schema migration;
- path normalization and traversal;
- volume/media identity;
- no-overwrite publication;
- manifest and catalog reconstruction;
- dependency-safe retention;
- idempotency and sequence gaps;
- wrong-media behavior; and
- bounded progress.

### 30.2 Filesystem integration tests

Disposable encrypted volumes test:

- enrollment;
- reconnect with changed drive letter or mount path;
- flush and read-back;
- interrupted publication;
- duplicate files;
- ACL drift;
- read-only remount;
- capacity exhaustion;
- safe dismount;
- catalog reconstruction; and
- exact-path retention.

Emulated filesystems cannot qualify production hardware durability.

### 30.3 Native integration tests

Disposable PostgreSQL and Scylla clusters test online backup, WAL, coordinated capture, offline copy, fresh restore,
validation, cancellation, restart, and cleanup.

### 30.4 Destructive qualification tests

Only disposable test media is used for:

- cable removal during copy;
- power interruption;
- filesystem corruption;
- encryption-header recovery;
- wrong-format insertion;
- duplicate enrollment;
- secure retirement; and
- recovery on a different workstation.

### 30.5 Production-readiness drill

Production local readiness requires:

- online vault failure;
- complete recovery using one offline medium;
- original workstation unavailable;
- Core and NATS unavailable;
- independently retrieved encryption recovery material;
- PostgreSQL PITR;
- Scylla fresh-cluster restore;
- coordinated application validation;
- measured RPO/RTO; and
- separate cutover approval.

## 31. Architecture acceptance criteria

The local design is accepted only when:

1. Every source-bound event carries BackupSource **LocalWorkstation** and uses the common event type;
   BackupSource **None** is rejected for operations and source-bound events.
2. UI, Console, and ScheduledTask actors use the common DatabaseBackup command and query surface.
3. ScheduledTask does not consume raw local service events.
4. The online vault is on a different physical device from active databases and the operating system.
5. At least two encrypted offline media devices are enrolled for production local protection.
6. A current verified offline medium is stored separately.
7. Drive letter or mount path alone can never identify a vault.
8. Wrong, duplicate, or changed MediaId fails closed.
9. Windows media uses BitLocker and Linux media uses LUKS2.
10. Encryption recovery material exists outside the protected workstation and is drill-tested.
11. Production vault filesystems meet the durability and access-control baseline.
12. FAT and exFAT are rejected for production vaults.
13. Published paths are unique and cannot be overwritten by the service writer.
14. File and directory durability plus read-back precede commit publication.
15. A restore point is invisible until signed manifests, commit, and catalog validate.
16. Local manifests retain the same logical meaning as AWS manifests.
17. Every offline-advertised restore point is self-contained on one medium.
18. PostgreSQL base backup and continuous WAL support tested PITR.
19. WAL acknowledgement occurs only after verified online-vault durability.
20. Scylla Manager capture accounts for required nodes, token ranges, schema, and data.
21. Online and offline RPO ages are reported separately.
22. Offline rotation lateness creates a policy violation even when online backup succeeds.
23. Retention uses an exact approved path list and separate execution identity.
24. Retention cannot break a PostgreSQL, Scylla, coordinated, active, held, or restore-tested chain.
25. Offline retention remains pending until the exact medium is validated.
26. Restore reads from a validated source into a separate encrypted workspace.
27. Production restore uses fresh targets and stops at ReadyForCutover.
28. Break-glass recovery works without Core, NATS, databases, online vault, or original workstation.
29. Recovery from one offline medium succeeds on another compatible workstation.
30. Local ACLs and mounted encryption are not described as AWS-equivalent immutability.
31. Ransomware procedure prevents known-clean media from being attached to a suspect host.
32. Media retirement does not rely on ordinary file deletion as secure erasure.
33. The independent Database Backup Host and Core can restart separately and reconcile through the external journal
    without duplicating native work.
34. The service runs as a standalone .NET 10 Worker from the first implementation, never executes inside Api.Server or
    an actor, and passes an Ubuntu 24.04 Docker qualification gate before paper-trading deployment; Aspire is deferred.
35. Representative benchmarks and drills demonstrate acceptable application impact, RPO, and RTO.
36. PostgreSQL and ScyllaDB execution is available only through the shared high-level allowlisted capabilities; no
    actor or client can supply native commands, arbitrary arguments, credentials, or paths.
37. The Console observes the same actor API and events as the UI and cannot bypass the DatabaseBackup actors.
38. UI and Console observe only authorized bounded DatabaseBackup domain events, never execution-intent or raw service
    events.
39. `SystemAdminDbContext` contains only rebuildable event projections and bounded run statistics and is never written
    directly by the LocalWorkstation processor.
40. The local execution journal uses a transactional store on an encrypted persistent volume outside protected
    databases, removable media, and the container writable layer.
41. Successful, failed, and cancelled local operations retain bounded structured run statistics; immutable completed
    manifests retain the final summary needed for disaster reconciliation.
42. After restore, newer vault/media evidence enters SystemAdmin only through authenticated reconciliation commands and
    domain events before projection.

## 32. Decisions requested during review

| Decision | Proposed direction |
| --- | --- |
| BackupSource | Shared enum is `None`, `LocalWorkstation`, and `AwsCloud`; this processor accepts `LocalWorkstation`, while `None` is only unselected/default or an all-sources query filter |
| Minimum topology | One dedicated encrypted online vault plus two encrypted rotated offline media devices |
| Production readiness | Requires a current verified offline medium stored separately |
| Windows encryption | BitLocker for fixed and removable volumes |
| Linux encryption | LUKS2 over dm-crypt |
| Windows filesystem | NTFS baseline; ReFS permitted for supported qualified fixed volumes |
| Linux filesystem | XFS or ext4 after qualification |
| Removable filesystem | NTFS on Windows; supported XFS/ext4 inside LUKS2 on Linux |
| Stable identity | Signed MediaId plus volume GUID or filesystem/LUKS UUID; no drive-letter identity |
| Publication | Unique no-overwrite files, durable flush/read-back, signed manifest, commit, then catalog |
| Online immutability claim | Best-effort ACL protection only; explicitly weaker than S3 Object Lock |
| Offline isolation | Media attached only for bounded copy, verification, or restore |
| Offline dependency rule | Every advertised restore point self-contained on one medium |
| PostgreSQL WAL acknowledgement | After verified online-vault durability |
| Offline RPO | Separate metric based on last verified sealed medium |
| Scylla capture | Scylla Manager with service-controlled local target/staging |
| Native capability API | Shared typed PostgreSQL and Scylla backup/restore ports; PostgreSQL protocol/utilities and Scylla Manager REST or allowlisted CLI fallback remain adapter details |
| Restore workspace | Separate encrypted workspace; vault files never restored in place |
| Retention | Exact-path approved plan; offline work waits for exact MediaId |
| Secure disposal | Cryptographic erase, approved secure erase, or physical destruction |
| ScheduledTask | Same DatabaseBackup commands and queries; raw service events remain isolated |
| Console | Same actor commands, queries, and bounded events as UI; no direct native or host execution path |
| Deployment | Dedicated standalone Database Backup Host Worker during functional development; Ubuntu 24.04 Docker qualification before paper-trading deployment; Aspire deferred |
| SystemAdmin persistence | Rebuildable `SystemAdminDbContext` projections and `DatabaseRecoveryRunStats` in Core PostgreSQL; no direct processor writes |
| Local execution journal | Transactional embedded database, preferably durable SQLite, on an encrypted persistent Docker/bind-mounted volume separate from protected databases and backup media |
| Recovery reconciliation | Replay restored events, validate newer signed local evidence, append accepted reconciliation events, then update projections |
| Cutover | Separate approval after fresh-target validation |

## 33. References

### IFM architecture

- [Database backup and restore architecture overview](Database-Backup-Architecture-Overview.md)
- [AWS cloud backup and restore reference architecture](AWS-Cloud-Backup-Restore-Architecture.md)
- [Aspire migration overview](../../Documents/system/Aspire%20migration%20overview.md)

### Windows storage and encryption

- [BitLocker overview](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/)
- [BitLocker recovery overview](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/recovery-overview)
- [BitLocker recovery process](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/recovery-process)
- [ReFS overview](https://learn.microsoft.com/en-us/windows-server/storage/refs/refs-overview)
- [ReFS integrity streams](https://learn.microsoft.com/en-us/windows-server/storage/refs/integrity-streams)
- [Windows volume GUID paths](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getvolumenameforvolumemountpointa)

### Linux storage and encryption

- [Linux dm-crypt documentation](https://docs.kernel.org/admin-guide/device-mapper/dm-crypt.html)
- [Cryptsetup and LUKS recovery guidance](https://gitlab.com/cryptsetup/cryptsetup/-/blob/main/FAQ.md)
- [Linux fsync](https://man7.org/linux/man-pages/man2/fsync.2.html)
- [Linux rename](https://man7.org/linux/man-pages/man2/rename.2.html)

### Database-native recovery

- [PostgreSQL pg_basebackup](https://www.postgresql.org/docs/current/app-pgbasebackup.html)
- [PostgreSQL replication protocol](https://www.postgresql.org/docs/current/protocol-replication.html)
- [PostgreSQL pg_verifybackup](https://www.postgresql.org/docs/current/app-pgverifybackup.html)
- [PostgreSQL continuous archiving and PITR](https://www.postgresql.org/docs/current/continuous-archiving.html)
- [Scylla Manager backup](https://manager.docs.scylladb.com/stable/backup/)
- [Scylla Manager restore](https://manager.docs.scylladb.com/stable/restore/)
- [Scylla Manager REST API](https://manager.docs.scylladb.com/stable/swagger/index.html)

## 34. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-10 | Created the LocalWorkstation reference architecture covering encrypted online and rotated offline vaults, stable media identity, durable publication, Windows/Linux storage, PostgreSQL and Scylla workflows, common source-independent events, retention, restore, break-glass recovery, security, observability, testing, and decisions for review. |
| 0.2 | 2026-08-11 | Aligned local backup with overview 0.7: direct Docker/Aspire host deployment, shared three-value BackupSource semantics, the common UI/Console/ScheduledTask actor API, the Domain.SystemAdmin DatabaseBackup feature boundary, and typed PostgreSQL/Scylla native capability adapters. |
| 0.3 | 2026-08-11 | Proposed the shared four-store persistence model for LocalWorkstation: `SystemAdminDbContext` projections/run statistics, a durable embedded execution journal on an encrypted persistent volume, immutable local manifest run evidence, and event-gated post-restore reconciliation. |
| 0.4 | 2026-08-12 | Approved standalone Worker development for paper trading, Ubuntu 24.04/.NET 10 Docker qualification after functional gates, and deferral of Aspire to the later full-system Linux production migration. |
| 0.5 | 2026-08-18 | Added the implemented Full/Automatic/Incremental contract, common-replica chain planning and fallback rules, PostgreSQL 17 manifest-chain capture and `pg_combinebackup` restore, Scylla Manager deduplicated-snapshot semantics, lineage persistence, dependency-safe retention, and common UI/Console/ScheduledTask selection. |
