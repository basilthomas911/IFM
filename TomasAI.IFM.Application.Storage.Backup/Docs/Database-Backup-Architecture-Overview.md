# IFM Database Backup and Restore Architecture Overview

Status: Approved architecture
Version: 0.6
Date: 2026-08-10
Scope: Shared architecture for PostgreSQL and ScyllaDB backup and restore using AWS cloud and local destinations

## 1. Purpose

This document defines the common database backup and restore architecture for IFM. It is the authoritative shared
design for the later AWS cloud and local reference architectures.

The architecture protects database clusters rather than treating every logical database or keyspace as an independent
physical backup target. It retains the SystemAdmin actor as the authoritative application coordinator, redesigns all
backup commands, events, and queries, and makes restore capability as important as backup creation.

The following documents will elaborate this overview after it is approved:

- `AWS-Cloud-Backup-Restore-Architecture.md`, the reference cloud architecture;
- `Local-Backup-Restore-Architecture.md`, the local architecture conforming to the AWS logical model; and
- the existing `BackupArchitecture.md`, which remains temporarily for comparison and will be deleted after all three
  replacement documents have completed their consistency review.

This is an architecture document. It defines responsibilities, boundaries, invariants, workflows, and acceptance
criteria. It does not prescribe C# classes, library packages, deployment scripts, infrastructure-as-code resources, or
an implementation task breakdown.

## 2. Architectural direction

### 2.1 Decisions established by this overview

The target architecture is based on these decisions:

1. The existing backup execution actor and its event flow are deprecated and will not constrain the redesign.
2. The SystemAdmin bounded context contains separate **DatabaseBackup** and **ScheduledTask** features. The
   DatabaseBackup feature contains exactly three actor roles: Command Actor, Event Actor, and Query Actor. ScheduledTask
   owns a separate actor set; its internal design is outside this document.
3. PostgreSQL and ScyllaDB are protected as physical clusters or explicitly defined cluster protection sets, not as a
   sequence of unrelated logical-database backup commands.
4. Backup and restore control uses durable, versioned events. SystemAdmin publishes execution-intent events; the service
   publishes progress and outcome events that Core converts into SystemAdmin commands. Native database artifacts never
   travel through the event transport or NATS.
5. AWS and local backup use the same logical contracts, operation states, manifests, verification rules, and restore
   qualification model.
6. AWS is the reference durability architecture. The local design follows the same semantics while documenting its
   lower geographic and infrastructure fault tolerance.
7. A backup is not considered operationally proven merely because files were created. It becomes restore-tested only
   after an isolated restore and application validation succeed.
8. Restore defaults to fresh database volumes or clusters. In-place replacement is an exceptional, separately approved
   cutover action.
9. The backup catalog and manifests must remain discoverable when the Core Actor Host and application databases are
   unavailable.
10. Normal backup operations require the Core Actor Host. Disaster recovery must also provide a tightly controlled
    break-glass path that does not depend on Core or NATS.
11. Database backup and restore behavior initially runs in a dedicated Database Backup Service hosted by `Api.Server`,
    outside actor handlers and actor threads.
12. The SystemAdmin Command Actor controls authoritative state and intent; the SystemAdmin Event Actor processes service
    events into commands; the SystemAdmin Query Actor serves projected read models; and the Database Backup Service
    controls long-running execution behavior and recoverable operational journals.
13. The service boundary is extraction-ready from the first implementation. It later moves without behavioral redesign
    into `TomasAI.IFM.Api.DatabaseBackup.Host` as an Aspire-managed project resource.
14. Aspire extraction creates the final process, resource, credential, and deployment boundary. Correctness cannot
    depend on the Aspire AppHost remaining online.
15. Database backup event names and schemas are source-independent. Every DatabaseBackup domain event,
    execution-intent event, service event, and translated service-event command carries one concrete **BackupSource**
    value.
16. **BackupSource** selects the cloud or local execution capability. Initial values are **AwsCloud** and
    **LocalWorkstation**. It is not the same as the UI or ScheduledTask request origin.
17. The UI and SystemAdmin ScheduledTask actors exercise the same DatabaseBackup command and query contracts.
    ScheduledTask-to-DatabaseBackup event integration is deferred until the ScheduledTask architecture is designed.

### 2.2 Non-goals

This overview does not:

- preserve obsolete per-database `.bak` file behavior;
- use the deprecated backup event actor as an execution engine;
- stream database rows, snapshots, SSTables, WAL, or backup archives through NATS;
- make Redis cache state an authoritative backup target;
- define NATS JetStream disaster recovery beyond its relationship to database recovery;
- define AWS resource names, accounts, regions, bucket policies, or exact service configurations;
- define local drive letters, mount points, filesystem products, or hardware;
- define ScheduledTask aggregates, persistence, actor roles, internal commands, internal events, or scheduling
  algorithms;
- authorize automatic production cutover after a restore; or
- claim a recovery objective until it has been measured by a successful restore drill.

## 3. Relationship to the IFM host architecture

This design conforms to the [Aspire migration overview](../../Documents/system/Aspire%20migration%20overview.md):

- the Core Actor Host remains the owner of business-domain actors and normal application-database clients;
- the future Database Backup Host is a satellite capability host;
- cross-host application control uses NATS;
- observability uses HTTP and OpenTelemetry-compatible export;
- backup destination credentials belong only to the Database Backup Host; and
- satellites do not become alternate application query or mutation paths.

The Database Backup Service is initially an isolated hosted-service capability inside `Api.Server`. It is not an actor,
does not execute work on a SystemAdmin actor thread, and does not expose arbitrary reads from PostgreSQL or ScyllaDB. It
accepts only allowlisted backup and restore operations over versioned capability contracts.

The target project boundary is `TomasAI.IFM.Api.DatabaseBackup.Host`. Aspire will later declare it as a separate project
resource beside the Core Actor Host, NATS, PostgreSQL, ScyllaDB, and observability resources. The initial architecture
must preserve an extraction seam so that moving the service changes hosting and transport, not backup policy, operation
state, manifests, verification, or restore behavior.

## 4. System context

### 4.1 Normal operating path

```text
Operator / UI              SystemAdmin ScheduledTask feature
      |                                 |
      | same DatabaseBackup commands    | same DatabaseBackup commands
      | and queries                     | and queries
      +----------------+----------------+
                       |
                       v
      SystemAdmin DatabaseBackup actors in Core
      - Command: policy, state, source-bound intent
      - Event: source-bound service-event ingestion
      - Query: source-filtered projected read models
              |
              | committed events
              | in-process now / NATS after extraction
              v
       Database Backup Service
      - hosted in Api.Server initially
      - separate Aspire host later
      - native backup coordination
      - artifact movement
      - verification and retention
      - restore preparation
          /                 \
         v                   v
 Local backup target     AWS backup target
```

Native database traffic and artifact data use database-native and destination-native channels. NATS carries operation
intent, bounded progress, manifest summaries, artifact references, outcomes, and audit correlation only.

### 4.2 Restore path

```text
Restore request
     |
     v
SystemAdmin actor authorization
     |
     v
Database Backup Host discovers and verifies restore point
     |
     v
Fresh PostgreSQL volume/cluster and/or fresh ScyllaDB volume/cluster
     |
     v
Native recovery -> engine validation -> application validation
     |
     v
ReadyForCutover
     |
     v
Separate operator approval -> controlled cutover
```

The restore operation stops at `ReadyForCutover` unless an authorized operator submits a separate cutover approval.

### 4.3 Disaster-recovery path

When Core or NATS is unavailable, an authorized recovery operator must be able to use the destination catalog and
immutable manifests to restore fresh PostgreSQL and ScyllaDB infrastructure. The restored Core Actor Host is started
only after native and minimum application validation pass.

Break-glass recovery is intentionally separate from the normal UI path. It must be authenticated, audited outside the
application database, documented in a recovery runbook, and exercised regularly.

### 4.4 Database Backup Service deployment evolution

The Database Backup Service has two planned deployment stages.

#### Stage 1: isolated service inside `Api.Server`

The first implementation runs as a dedicated hosted service within the current `Api.Server` process. It has:

- a dedicated service boundary and dependency-registration module;
- versioned execution-intent and service-outcome event contracts;
- a bounded work admission path separate from actor mailboxes;
- its own recoverable operation journal outside actor state;
- service-specific configuration, validation, telemetry, and health state;
- explicit CPU, memory, native-process, disk, and network concurrency limits; and
- no references from backup behavior to business-domain actor implementations.

SystemAdmin actors authorize operations, record authoritative state, and publish execution-intent events. The Database
Backup Service event listener receives those events after their durable actor-state commit. Actors never invoke native
utilities directly, wait synchronously for completion, or hold long-running backup resources.

Stage 1 is a logical and behavioral separation, not a strong process or secret-isolation boundary. An `Api.Server`
process failure can interrupt both actors and backup behavior, and service credentials loaded into the process share its
operating-system trust boundary. These limitations are accepted temporarily and remain visible as migration risks.

#### Stage 2: separate Aspire project resource

The service moves into `TomasAI.IFM.Api.DatabaseBackup.Host`, with its own process, dependency-injection composition
root, configuration, credentials, resource limits, health endpoints, metrics, logs, traces, deployment identity, and
release lifecycle.

The target host boundary provides:

- native backup utilities and large artifact transfers must not consume the Core actor thread pool;
- backup CPU, memory, disk, and network budgets must be independently constrained;
- AWS and local destination credentials must not enter the Core process;
- native database administration capabilities must not be available to ordinary business actors;
- backup failures, process crashes, or dependency upgrades must not stop business-domain actors;
- the backup service can be restarted and upgraded independently;
- backup behavior has a substantially different security and operational profile from business-domain processing; and
- the future Aspire topology can orchestrate and observe the capability as a distinct project resource.

Stage 2 turns the Stage 1 logical boundary into a process and trust boundary. No shared static state, direct service
reference, or `Api.Server` configuration dependency may prevent extraction.

### 4.5 Control state versus execution behavior

The architecture deliberately separates two kinds of state:

| State owner | State | Examples |
| --- | --- | --- |
| SystemAdmin Command Actor in Core | Authoritative event-sourced application control state | Requested operation, policy revision, authorization, approvals, expected protection set, high-level lifecycle, audit correlation, terminal outcome |
| Database Backup Service | Recoverable execution state | Native process phase, staging path, transfer checkpoint, per-artifact checksum status, destination retry state, lease ownership, restart journal |
| Backup destination | Independent recovery evidence | Immutable artifacts, manifests, catalog entries, verification records, retention and legal-hold metadata |

The SystemAdmin Command Actor's event stream remains the source of truth for what IFM authorized and what outcome the
application recognizes.
The Database Backup Service remains the source of truth for how an accepted operation is currently being executed. The
destination remains the source of truth for what recoverable evidence physically exists.

These records are correlated by `OperationId`, `BackupSetId`, policy revision, and monotonic state revision. No layer is
expected to persist every detail owned by another layer.

### 4.6 Service communication boundary

Normal communication follows this pattern:

```text
SystemAdmin Command Actor                 Database Backup Service
      |                                             |
      | durable execution-intent event              |
      |-------------------------------------------->|
      |                                             |
      | service progress/outcome event              |
      |<--------------------------------------------|
      |                                             |
SystemAdmin Event Actor                             |
      |                                             |
      | translate service event into command        |
      v                                             |
SystemAdmin Command Actor                           |
      |                                             |
      | persist domain event -> update read model   |
      v                                             |
SystemAdmin Query Actor / UI                        |
```

In Stage 1, an in-process event publisher delivers committed SystemAdmin execution-intent events to the Database Backup
Service event listener. The service publishes its own progress and outcome events to the SystemAdmin Event Actor.
Delivery is asynchronous and must not execute backup behavior on the actor call stack.

In Stage 2, the two listeners retain the same logical contracts while NATS becomes the cross-host event transport.
Contracts are bounded, idempotent, versioned, authorized, and safe for at-least-once delivery. Actor commands, domain
events, read models, and operation state do not change when transport changes.

The service never updates SystemAdmin event-sourced state directly. A host-to-Core event is an observation from the
execution service. The SystemAdmin Event Actor converts it into a command, and the SystemAdmin Command Actor decides
whether the observation is valid for the current operation revision before emitting a new durable domain event.

In both stages, HTTP is limited to health, readiness, metrics, diagnostic management, and explicitly secured break-glass
recovery. A normal HTTP endpoint must not bypass SystemAdmin authorization to start a production backup or restore.

Native database traffic flows between the Database Backup Service or approved native agent and the database backup
interface. Artifact traffic flows directly between the service and AWS or local storage. Neither path is proxied through
Core or NATS.

### 4.7 Lifecycle and availability

In Stage 1, the service shares the `Api.Server` process lifecycle but has an independent readiness state. Core reports the
backup capability as unavailable, retains scheduled intent according to policy, and alerts when an RPO or schedule is at
risk if the hosted service cannot accept work.

In Stage 2, the Database Backup Host may be offline without stopping Core and gains an independent deployment and
restart lifecycle.

The service normally requires Core to authorize new operations. After accepting an operation, a temporary Core or NATS
outage does not require the service to abandon a safe native capture or artifact transfer. It journals execution,
continues only to the policy-approved safe boundary, and reconciles state when communication returns.

The service must not invent new scheduled or production restore operations while Core is unavailable. The only
exception is the separately authenticated break-glass disaster-recovery path.

After an `Api.Server` restart in Stage 1, or reconnect in Stage 2, Core and the service reconcile by `OperationId` and
state revision. Core does not blindly create a new operation, and the service does not overwrite a newer authoritative
actor decision.

## 5. Responsibilities

### 5.1 SystemAdmin DatabaseBackup actor group

The SystemAdmin DatabaseBackup feature uses exactly three actors with non-overlapping responsibilities.

Unless explicitly qualified as ScheduledTask, references below to the SystemAdmin Command Actor, Event Actor, or Query
Actor mean the corresponding actor in the DatabaseBackup feature.

#### 5.1.1 SystemAdmin Command Actor

The Command Actor owns:

- validation of UI, API, ScheduledTask, policy, and internally translated service commands;
- accepted backup and restore policy revisions;
- backup requests from authorized callers and references to accepted ScheduledTask identities;
- authorization and approval state;
- operation IDs, correlation, causation, and idempotency;
- application checkpoints and any required maintenance coordination;
- reconstruction and transition of authoritative event-sourced aggregates;
- append-only domain events and the authoritative application audit record;
- publication of committed execution-intent events for the Database Backup Service listener;
- validation of progress, completion, verification, error, failure, and reconciliation commands from the Event Actor;
  and
- reconciliation of break-glass recovery records after Core returns.

It is the only SystemAdmin actor permitted to change authoritative backup state. It does not execute database utilities,
copy large artifacts, hold AWS or local destination secrets, or block an actor thread while backup behavior is active.

#### 5.1.2 SystemAdmin Event Actor

The Event Actor owns:

- listening for allowlisted Database Backup Service events;
- validating contract version, subject, producing service identity, envelope integrity, and required identifiers;
- validating that the producing service identity is authorized for the declared BackupSource;
- deserializing each supported service event;
- mapping the service event to the corresponding internal SystemAdmin command;
- preserving source event ID, BackupSource, service sequence, correlation, causation, operation, host, and observation
  metadata;
- sending the translated command to the Command Actor using `OperationId`, `BackupSetId`, policy identity, or service
  identity as the appropriate entity key;
- acknowledging the inbound service event only after the Command Actor reports durable success or idempotent prior
  application;
- requesting reconciliation when it detects a service-sequence gap; and
- routing malformed, unauthorized, unmappable, or repeatedly rejected events to the approved diagnostic/dead-letter
  path without mutating backup state.

The Event Actor is an inbound adapter. It does not apply domain transitions, save aggregate state, update read models,
execute backup behavior, or treat receipt of a service event as proof that the observation is valid.

#### 5.1.3 SystemAdmin Query Actor

The Query Actor owns:

- the database-backup query contract;
- validation and routing of query requests;
- reading event-projected backup, restore, policy, retention, service-health, and recovery-readiness models;
- returning bounded query results to UI, API, and authorized ScheduledTask actor clients; and
- reporting projection availability or staleness without falling back to the execution service as a hidden data source.

The Query Actor is read-only. It does not load command aggregates, issue execution intent, or query PostgreSQL/Scylla
backup utilities and destinations directly.

#### 5.1.4 Actor-group invariants

- Command Actor domain events drive both service execution intent and read-model projection.
- Event Actor service-event ingestion always passes through a Command Actor command before state changes.
- Query Actor results come only from projected domain events and declared static protection metadata.
- All three actors share contract identities and correlation but not mutable in-memory state.
- Actor mailboxes remain bounded and never carry native backup artifacts or unbounded logs.
- A failure in event ingestion or projection cannot silently advance command state.

#### 5.1.5 SystemAdmin feature boundary

This architecture defines two cooperating features inside the SystemAdmin bounded context:

| SystemAdmin feature | Responsibility in this architecture |
| --- | --- |
| DatabaseBackup | Owns backup and restore policy, authorization, source-bound operations, service integration, event-sourced backup state, projections, and the commands and queries defined in Section 9 |
| ScheduledTask | Owns time-based triggering through its own actor set and calls the public DatabaseBackup command and query contracts as an authorized actor client |

The DatabaseBackup and ScheduledTask features do not share mutable actor state. The ScheduledTask feature does not call
the Database Backup Service, publish DatabaseBackup execution-intent events, or consume raw service response events. It
submits the same DatabaseBackup commands available to the UI and may use the same bounded DatabaseBackup queries to
decide whether a scheduled invocation is appropriate.

ScheduledTask command, event, query, aggregate, persistence, retry, and actor-role design is deliberately out of scope.
No ScheduledTask-specific event integration is required by this version. If later scheduling workflows need completion
notifications, that design must define an explicit feature-to-feature contract rather than coupling ScheduledTask actors
to Database Backup Service events.

### 5.2 Database Backup Service and future host

The Database Backup Service is the behavior boundary. It initially runs inside `Api.Server`; the future Database Backup
Host becomes its executable and process boundary under Aspire.

The Database Backup Service owns:

- listening for authorized SystemAdmin execution-intent events;
- routing each event to the processor registered for its concrete BackupSource;
- execution of the requested backup and restore behavior;
- database-native backup coordination within its approved credential model;
- bounded concurrency, resource throttling, and cancellation;
- local staging where required;
- artifact checksum generation and native verification;
- destination adapters for AWS and local storage;
- immutable operation manifests and destination catalogs;
- retention evaluation and safe chain-aware pruning;
- restore-point discovery and restore preparation;
- publication of bounded progress and outcome events to Core;
- independent operational logs and metrics; and
- the break-glass recovery entry point.

One service process may support more than one BackupSource, but each registered processor declares its supported source
and accepts only matching events. A deployment may instead run source-specific service instances. Neither choice
changes event names, payload schemas, actor commands, queries, or operation state.

It must not expose general SQL, CQL, arbitrary paths, arbitrary process execution, or a generic object-storage API.

The service must not reference or instantiate business-domain actors. It consumes only shared SystemAdmin backup
contracts and infrastructure abstractions needed to perform authorized behavior. This rule applies even while both
components share the `Api.Server` process.

### 5.3 Native database backup capability

The native database backup capability is the trusted boundary that communicates with PostgreSQL and ScyllaDB backup
interfaces. It belongs to the Database Backup Service or a service-controlled engine-local agent as defined in Section
24. It never belongs to a SystemAdmin or business-domain actor.

Regardless of placement, it must:

- use only engine-native, consistent backup mechanisms;
- use allowlisted cluster identities and operation options;
- prevent user-provided shell or path injection;
- report a precise native consistency boundary;
- write only to operation-specific staging locations;
- never modify active application data as part of backup creation; and
- never treat file copy completion as proof of recoverability.

### 5.4 UI

The UI owns operator interaction only. It displays policies, operations, restore points, health, approvals, and audit
results by using SystemAdmin actor commands and queries. It never connects to a database, AWS, or a local backup drive.

### 5.5 Backup destinations

AWS and local destinations own durable artifact storage, but not application operation state. Every destination must be
self-describing through immutable manifests and catalog entries so that recovery does not require the application
database.

## 6. Protection model

### 6.1 Protection sets

The primary protection units are:

| Protection set | Scope | Primary recovery role |
| --- | --- | --- |
| `CorePostgresCluster` | The complete PostgreSQL physical cluster, including its application databases | Authoritative actor/event state, operational records, and sequence infrastructure stored in PostgreSQL |
| `CoreScyllaCluster` | The configured ScyllaDB cluster and allowlisted application keyspaces | Domain projections, market data, and other Scylla-resident state according to data classification |
| `CoordinatedCoreBackupSet` | One PostgreSQL restore point plus one ScyllaDB restore point associated with a shared application checkpoint | Whole-application recovery and restore drills |

Names are logical architecture identifiers. Environment-specific physical cluster identities are configuration, not
part of public command subjects.

### 6.2 Logical database and keyspace presentation

The UI may show the logical databases and keyspaces contained in a protection set, but selecting an individual logical
database must not accidentally trigger another copy of the same physical PostgreSQL cluster.

Selective logical export may be introduced later as a separately named operation. It must not be represented as a
physical cluster backup or be used as the primary disaster-recovery mechanism.

### 6.3 Data classification

Each protected data group must be classified as one of:

- **Authoritative**: cannot be reconstructed from another retained source and must meet the strictest recovery target.
- **Rebuildable**: can be recreated deterministically from authoritative events or data.
- **Externally recoverable**: can be re-downloaded from an approved external source under a defined retention agreement.
- **Ephemeral**: cache or transient processing state that is intentionally not restored.

Classification determines backup frequency, retention, restore priority, and validation. A projection being rebuildable
does not automatically mean rebuilding it is operationally acceptable; rebuild duration must also satisfy the recovery
time objective.

Redis is excluded when it contains only ephemeral or rebuildable cache state. If authoritative state is ever introduced
into Redis, that architectural decision must reopen the protection inventory.

### 6.4 JetStream relationship

NATS JetStream recovery is a separate infrastructure concern. Database restore planning must nevertheless declare
whether durable messages or consumer positions are required to resume consistently from the selected application
checkpoint. A complete production disaster-recovery runbook must cover both the database restore points and any
required JetStream snapshot or replay position.

## 7. Shared architecture model

### 7.1 Backup policy

A `BackupPolicy` is a versioned, non-secret definition containing:

- protected environment and protection sets;
- enabled BackupSource values and source-specific policy bindings;
- enabled destinations;
- which destinations are required for success;
- ScheduledTask identities and cadence requirements for base, incremental, log-archive, and restore-drill work;
- retention and immutability rules;
- maximum concurrency and resource budgets;
- recovery point and recovery time objectives;
- verification levels;
- notification and escalation rules; and
- the effective date and approving identity.

Policies are owned by Core and distributed to the Database Backup Host as accepted revisions. The host must acknowledge
the revision it is enforcing. A host may reject an unsafe or unsupported policy, but it may not silently reinterpret it.

### 7.2 Backup operation

A `BackupOperation` represents one requested execution against one protection set. It includes:

- `OperationId`;
- one concrete BackupSource;
- `BackupSetId` when coordinated with other protection sets;
- policy revision;
- operation type;
- requested and required destinations;
- request, correlation, and causation identities;
- current state and state revision;
- timestamps and native consistency boundaries;
- bounded progress;
- terminal outcome; and
- references to immutable manifests.

`OperationId` is independent of a database name and is never reused.

### 7.3 Backup set

A `BackupSet` associates one or more engine-specific operations with a shared recovery purpose. It records:

- participating protection sets;
- participating source-bound operations;
- application checkpoint or sequence boundary;
- engine-specific restore points;
- destination replica status;
- compatibility and completeness status; and
- whether the set is verified or restore-tested.

A backup set may be partially complete without being eligible for whole-application recovery.

### 7.4 Artifact and replica

A `BackupArtifact` is a native backup file, directory tree, WAL segment group, SSTable set, schema export, log, or
manifest. An `ArtifactReplica` is one stored copy in AWS or local storage and records the BackupSource that owns its
execution and catalog lifecycle.

The architecture distinguishes:

- native artifact validity;
- transfer completion;
- destination durability;
- checksum verification;
- retention eligibility; and
- restore-test status.

Copying one artifact to two destinations does not create two independent native consistency boundaries. Both replicas
refer to the same immutable artifact identity and checksum set.

### 7.5 Restore point

A `RestorePoint` is a cataloged, verified selection that can be presented to an operator. It includes:

- protection set and cluster identity;
- recoverable time or checkpoint range;
- required base and incremental chain;
- required PostgreSQL WAL range or Scylla recovery components;
- available destination replicas;
- engine and format compatibility;
- verification and restore-drill history; and
- current retention or legal-hold status.

An artifact directory is not a restore point until its manifest and required dependency chain have been validated.

### 7.6 Restore operation

A `RestoreOperation` records:

- selected restore point or coordinated backup set;
- one concrete BackupSource;
- source destination and fallback replicas;
- fresh target identity;
- authorization and approval identities;
- native restore progress;
- validation results;
- cutover eligibility and separate cutover approval; and
- cleanup disposition for failed or test restores.

### 7.7 Backup source

**BackupSource** is a required logical discriminator that selects which backup execution capability owns an operation.
The initial contract values are:

| BackupSource | Processing capability |
| --- | --- |
| AwsCloud | The AWS cloud backup/restore processor defined by the AWS reference architecture |
| LocalWorkstation | The local workstation backup/restore processor to be defined by the local reference architecture |

BackupSource is not a bucket, directory, drive, Region, account, endpoint, credential, or replica identifier. Those
details remain service configuration and destination metadata. BackupSource is also not the request origin: UI and
ScheduledTask identify who initiated a command, while BackupSource identifies which execution capability processes it.

Every DatabaseBackup event carries exactly one recognized BackupSource. Values such as **Any**, **Both**, null, or an
unknown future value are rejected. A policy requiring AWS and local processing creates distinct source-bound operations
with distinct OperationId values under one BackupSetId. This prevents two processors from accepting the same operation
while preserving coordinated recovery reporting.

BackupSource is immutable after an operation is created. It is copied from accepted command intent into every domain
event, execution-intent event, service response event, translated command, projection, manifest summary, and terminal
outcome for that operation. It is never inferred only from a NATS subject, host name, bucket, or filesystem path.

## 8. Operation state machines

### 8.1 Backup states

```text
Requested
   |
   v
Authorized -> Queued -> Preparing -> Capturing -> Transferring
                                              -> Verifying -> Publishing
                                                              |
                                                              v
                                                          Completed

Any active state -> Failed
Any cancellable active state -> Cancelling -> Cancelled
```

State meanings:

- `Requested`: Core accepted intent but has not authorized execution.
- `Authorized`: policy, permissions, target, and destination requirements passed.
- `Queued`: the capability host durably accepted the operation.
- `Preparing`: preflight, locking, capacity, credentials, and compatibility checks are running.
- `Capturing`: the engine-native consistency boundary and native artifacts are being created.
- `Transferring`: immutable artifacts are being copied to required destinations.
- `Verifying`: checksums and engine-native verification are running.
- `Publishing`: manifests and catalog entries are being atomically exposed.
- `Completed`: every destination required by policy is verified and cataloged.
- `Failed`: a terminal failure prevents policy-defined success.
- `Cancelled`: cancellation completed without publishing an eligible restore point.

An optional destination failure may produce a completed operation with a degraded-replica warning only when the signed
policy explicitly permits it.

### 8.2 Restore states

```text
Requested -> AwaitingApproval -> Authorized -> Preparing -> Retrieving
           -> Restoring -> Recovering -> Validating -> ReadyForCutover
                                                   -> DrillCompleted

ReadyForCutover -> AwaitingCutoverApproval -> CuttingOver -> Completed

Any active state -> Failed
Any cancellable active state -> Cancelling -> Cancelled
```

Production restore, production cutover, and cleanup are separate decisions. A successful restore drill ends at
`DrillCompleted` and never changes the active production target.

### 8.3 State invariants

- State revisions increase monotonically.
- Terminal states cannot return to active states.
- Duplicate messages cannot repeat a destructive action.
- Progress is advisory; state and manifest revisions are authoritative.
- Cancellation is best effort during a native atomic step and must report when the operation becomes safely cancellable.
- No restore point becomes visible until its immutable manifest is published successfully.

## 9. SystemAdmin DatabaseBackup actor contracts

Contract names in this section are conceptual architecture names. Their serialized shape and language-specific types
belong to later implementation design.

The DatabaseBackup command, query, domain-event, and service-event names are identical for AWS and local execution.
Contracts never add source-specific variants such as AwsDatabaseBackupCompletedEvent or
LocalDatabaseBackupCompletedEvent. BackupSource is data in the common contract, not part of its type name.

### 9.1 DatabaseBackup commands

- `RequestDatabaseBackupCommand`
- `CancelDatabaseBackupCommand`
- `RequestDatabaseRestoreCommand`
- `ApproveDatabaseRestoreCommand`
- `CancelDatabaseRestoreCommand`
- `ApproveDatabaseCutoverCommand`
- `RequestDatabaseRestoreDrillCommand`
- `UpdateDatabaseBackupPolicyCommand`
- `PlaceBackupLegalHoldCommand`
- `ReleaseBackupLegalHoldCommand`
- `RequestBackupRetentionEvaluationCommand`
- `ExecuteBackupRetentionPlanCommand`

Every command includes an idempotency identity, actor subject, requesting identity, expected policy revision where
applicable, and audit context. Secrets and arbitrary executable arguments are prohibited.

Every source-scoped command includes one concrete BackupSource. A policy command may describe more than one enabled
source, but the Command Actor emits a separate source-bound domain and execution event for each resulting operation.
The command audit envelope distinguishes a UI caller from a ScheduledTask actor through RequestOrigin and requesting
identity; RequestOrigin is never used to route execution.

A UI- or ScheduledTask-originated command never directly invokes a native database utility. It is handled by the
DatabaseBackup Command Actor, which validates current event-sourced state and emits one or more durable domain events.

### 9.2 SystemAdmin-to-service execution events

Selected SystemAdmin domain events are published after their event-store commit as service-facing execution-intent
events. The Database Backup Service event listener consumes these events.

Every event in the following table carries one concrete BackupSource. The same event type is published for AwsCloud and
LocalWorkstation. A processor must reject rather than ignore an event with an unsupported source.

| SystemAdmin event | Service behavior authorized by the event |
| --- | --- |
| `DatabaseBackupExecutionRequestedEvent` | Preflight and execute one backup operation for the specified protection set and required destinations |
| `DatabaseBackupCancellationRequestedEvent` | Attempt safe cancellation of the identified backup operation |
| `DatabaseBackupVerificationRequestedEvent` | Re-verify a cataloged backup or destination replica without creating a new capture |
| `DatabaseRestoreExecutionRequestedEvent` | Prepare and execute an approved restore into the specified fresh target |
| `DatabaseRestoreCancellationRequestedEvent` | Attempt safe cancellation of the identified restore operation |
| `DatabaseRestoreDrillRequestedEvent` | Restore and validate an isolated disposable target without production cutover |
| `DatabaseCutoverExecutionRequestedEvent` | Execute only the separately approved cutover step for a validated restored target |
| `DatabaseRetentionEvaluationRequestedEvent` | Calculate a dependency-safe retention plan without deleting artifacts |
| `DatabaseRetentionExecutionRequestedEvent` | Execute a specifically approved and revision-matched retention plan |
| `DatabaseBackupPolicyActivatedEvent` | Adopt the indicated non-secret backup policy revision and report whether the service can enforce it |
| `DatabaseBackupReconciliationRequestedEvent` | Report the service journal state for an operation or all non-terminal operations after restart or reconnect |

Every execution event contains the complete bounded intent required by the service. The listener does not call back into
SystemAdmin to fill missing policy or target fields during a native operation. It may reject an unsupported, stale,
unsafe, or incomplete event and report that rejection through a service event.

The event store commit precedes service publication. A transactional outbox or equivalent durable dispatcher must
prevent these invalid states:

- the service begins behavior for an event that was not committed to SystemAdmin state; or
- SystemAdmin commits an execution event permanently but publication is silently lost.

Publication is at least once. The service deduplicates by event ID, `OperationId`, and BackupSource and never starts a
second native operation merely because the same execution event is redelivered. An existing OperationId can never be
rebound to another BackupSource.

### 9.3 Database Backup Service event listener

The service event listener owns the execution-event subscription and admission boundary.

In Stage 1:

- it subscribes to the committed in-process SystemAdmin event publication stream;
- it routes only to a processor registered for the event's BackupSource;
- it copies the bounded event envelope into the recoverable service journal before acknowledging acceptance;
- it schedules work outside the actor call stack; and
- it publishes service events through the in-process service-event publisher.

In Stage 2:

- it subscribes to the corresponding authorized NATS subjects, which may include BackupSource as a routing token;
- durable delivery and acknowledgement replace the in-process transport;
- the service journal remains the execution restart boundary; and
- the event names and semantics remain unchanged.

The listener is not a domain actor. It does not own SystemAdmin state, create operator approvals, or infer authority from
the ability to receive an event.

NATS subject or in-process subscription filtering is an optimization and authorization boundary, not the source of
truth. The listener validates the BackupSource inside the event envelope, the processor capability, and the producing
host or service identity before admission.

### 9.4 Service-to-SystemAdmin events

The Database Backup Service publishes observations about accepted behavior. These are integration events from the
execution boundary; they are not applied directly to the SystemAdmin aggregate.

Every service event listed below echoes the exact BackupSource from the accepted execution intent. AWS and local
processors use the same event names and payload schemas. The service cannot relabel a response, and the SystemAdmin
Event Actor rejects a response whose BackupSource differs from the operation or whose producing host is not authorized
for that source.

#### Backup execution events

| Service event | Meaning |
| --- | --- |
| `DatabaseBackupServiceAcceptedEvent` | The service durably admitted the backup operation and owns its execution lease |
| `DatabaseBackupServiceRejectedEvent` | The service rejected the operation before execution, with a structured reason |
| `DatabaseBackupServiceStartedEvent` | Preflight succeeded and execution entered an active phase |
| `DatabaseBackupServiceProgressEvent` | A bounded, monotonic progress checkpoint was reached |
| `DatabaseBackupBoundaryEstablishedEvent` | The database-native consistency boundary was established and identified |
| `DatabaseBackupArtifactReplicaUpdatedEvent` | One AWS or local artifact replica reached a meaningful lifecycle state |
| `DatabaseBackupVerificationCompletedEvent` | Checksum and native verification completed with structured results |
| `DatabaseBackupServiceErrorEvent` | A retryable or operator-actionable error occurred without yet declaring terminal failure |
| `DatabaseBackupServiceCompletedEvent` | All policy-required backup work and manifest publication completed |
| `DatabaseBackupServiceFailedEvent` | The operation reached a terminal failure |
| `DatabaseBackupServiceCancelledEvent` | Cancellation reached a safe terminal boundary and no incomplete data is eligible for restore |

#### Restore and drill events

| Service event | Meaning |
| --- | --- |
| `DatabaseRestoreServiceAcceptedEvent` | The service durably admitted the restore or drill operation |
| `DatabaseRestoreServiceRejectedEvent` | Restore admission failed before target mutation |
| `DatabaseRestoreServiceStartedEvent` | Restore preflight passed and retrieval or target preparation began |
| `DatabaseRestoreServiceProgressEvent` | A bounded, monotonic restore checkpoint was reached |
| `DatabaseRestoreValidationCompletedEvent` | Native and application validation completed with structured results |
| `DatabaseRestoreReadyForCutoverEvent` | A production recovery target passed required validation and awaits separate approval |
| `DatabaseRestoreDrillCompletedEvent` | An isolated restore drill completed, including measured RPO and RTO |
| `DatabaseRestoreServiceErrorEvent` | A retryable or operator-actionable restore error occurred without yet declaring terminal failure |
| `DatabaseRestoreServiceCompletedEvent` | The approved restore or cutover workflow reached its terminal success state |
| `DatabaseRestoreServiceFailedEvent` | The restore, validation, drill, or cutover reached a terminal failure |
| `DatabaseRestoreServiceCancelledEvent` | Restore cancellation reached a safe terminal boundary |

#### Policy, retention, and reconciliation events

| Service event | Meaning |
| --- | --- |
| `DatabaseBackupPolicyAppliedEvent` | The service accepted and is enforcing a policy revision |
| `DatabaseBackupPolicyRejectedEvent` | The service cannot safely enforce the requested policy revision |
| `DatabaseRetentionPlanCreatedEvent` | A dependency-safe, non-executed retention plan is available for actor review |
| `DatabaseRetentionExecutionCompletedEvent` | The approved retention plan completed with per-destination results |
| `DatabaseRetentionExecutionFailedEvent` | Retention stopped safely because an invariant or destination operation failed |
| `DatabaseBackupServiceReconciliationEvent` | The service reports its journaled state and last sequence for reconciliation |
| `DatabaseBackupServiceCapabilityChangedEvent` | A meaningful service capability or readiness state changed |

Service events contain bounded structured state, not raw process output. Detailed logs, per-file transfer messages, and
stack traces remain in service telemetry or artifact logs and are referenced by a safe diagnostic identifier.

The service records an outbound event in its recoverable journal before publication and retains enough delivery state to
retry until Core acknowledges ingestion or reconciliation proves that the corresponding SystemAdmin domain event already
exists. A service crash between native progress and event publication must not permanently hide a terminal result.

### 9.5 SystemAdmin Event Actor translation into commands

The SystemAdmin Event Actor consumes every supported service event and translates it into a SystemAdmin command. This is
the only normal path by which host observations enter the authoritative backup-state workflow.

| Service event group | SystemAdmin command produced |
| --- | --- |
| Accepted or rejected | `RecordDatabaseOperationAdmissionCommand` |
| Started | `RecordDatabaseOperationStartedCommand` |
| Progress | `RecordDatabaseOperationProgressCommand` |
| Native boundary | `RecordDatabaseBackupBoundaryCommand` |
| Artifact replica state | `RecordDatabaseArtifactReplicaCommand` |
| Verification or validation | `RecordDatabaseOperationVerificationCommand` |
| Retryable or actionable error | `RecordDatabaseOperationErrorCommand` |
| Ready for cutover | `RecordDatabaseRestoreReadyForCutoverCommand` |
| Completed | `CompleteDatabaseOperationCommand` |
| Failed | `FailDatabaseOperationCommand` |
| Cancelled | `RecordDatabaseOperationCancelledCommand` |
| Policy applied or rejected | `RecordDatabaseBackupPolicyStatusCommand` |
| Retention plan or outcome | `RecordDatabaseRetentionResultCommand` |
| Reconciliation | `ReconcileDatabaseBackupServiceStateCommand` |
| Capability change | `RecordDatabaseBackupServiceCapabilityCommand` |

The translated command preserves the source service event ID, BackupSource, service sequence, operation ID,
correlation ID, host identity, and observed time. The SystemAdmin Event Actor sends that command to the Command Actor,
which then:

1. loads the aggregate identified by `OperationId`, `BackupSetId`, or policy identity;
2. rejects an unknown operation, BackupSource mismatch, host not authorized for the source, stale policy, duplicate
   source event, illegal transition, or stale service sequence;
3. applies the accepted state transition;
4. appends a durable SystemAdmin domain event;
5. publishes the domain event for projection; and
6. returns durable success or an idempotent-already-applied result to the Event Actor.

The Event Actor acknowledges the service event only after that result. Translation does not mean blind acceptance. The
SystemAdmin Command Actor remains the transition authority.

### 9.6 Event-sourced SystemAdmin state

All authoritative database backup state, including accepted progress and structured errors, is reconstructed from
SystemAdmin domain events. No mutable backup-status table is updated as an alternate source of truth. The event-sourced
boundaries are:

| Aggregate identity | State owned by the aggregate |
| --- | --- |
| `OperationId` for backup | Immutable BackupSource, request, authorization, service admission, phase, persisted progress, boundary, replicas, verification, terminal outcome, errors |
| `OperationId` for restore | Immutable BackupSource, request, approvals, target, phase, persisted progress, validation, cutover readiness, terminal outcome, errors |
| `BackupSetId` | Participating source-bound engine operations, application checkpoint, completeness, compatibility, restore-test status |
| Environment/policy identity | Policy revisions, enabled BackupSources, ScheduledTask bindings, destination requirements, retention, RPO/RTO, service enforcement status |
| Service host identity and BackupSource | Capability revision, supported sources, effective policy revision, readiness transitions, last reconciliation time |

Using `OperationId` as the actor entity and mailbox key ensures that accepted service updates for one operation are
processed sequentially. Backup-set coordination is handled by its own aggregate rather than placing unrelated operation
progress into one global SystemAdmin state object.

Representative SystemAdmin domain events include:

- `DatabaseBackupRequestedEvent`;
- `DatabaseBackupAuthorizedEvent`;
- `DatabaseBackupExecutionRequestedEvent`;
- `DatabaseBackupServiceAdmissionRecordedEvent`;
- `DatabaseBackupStartedEvent`;
- `DatabaseBackupProgressRecordedEvent`;
- `DatabaseBackupBoundaryRecordedEvent`;
- `DatabaseBackupArtifactReplicaRecordedEvent`;
- `DatabaseBackupVerifiedEvent`;
- `DatabaseBackupCompletedEvent`, `DatabaseBackupFailedEvent`, or `DatabaseBackupCancelledEvent`;
- corresponding restore, validation, cutover-readiness, completion, failure, and cancellation events;
- backup-set checkpoint and completeness events;
- policy revision and enforcement-status events; and
- service reconciliation and capability-status events.

Every representative event above and every corresponding restore, retention, policy, verification, and reconciliation
event carries BackupSource. A policy revision affecting both initial sources produces source-specific domain events so
that no event has ambiguous execution ownership.

The current deprecated backup events are not repurposed. New event names, entity identities, schemas, and versioning are
introduced for this architecture.

The Database Backup Service execution journal is operational recovery state, not an alternative application aggregate.
It may also use an append-only event journal, but SystemAdmin event sourcing remains authoritative for actor queries,
operator decisions, audit, and recognized terminal outcomes.

### 9.7 Progress and error persistence

Progress is event sourced, but it is deliberately bounded. The service emits a durable progress event only for a
meaningful checkpoint such as:

- operation phase change;
- policy-configured percentage or byte threshold;
- database-native boundary creation;
- destination replica state change;
- verification stage change;
- restore recovery milestone; or
- heartbeat interval needed to distinguish active work from a stalled operation.

The service does not emit one durable event per file, WAL segment, SSTable component, output line, or transferred block.
High-frequency details remain in metrics, traces, structured service logs, and the operation artifact log. This prevents
progress event sourcing from becoming the dominant write load during large backups.

A persisted error contains:

- stable error code and category;
- operation phase and affected engine or destination;
- retryable, terminal, or operator-actionable classification;
- safe public message;
- service sequence and occurrence time;
- whether native or destination state may require cleanup;
- diagnostic log or manifest reference; and
- no credential, raw connection string, sensitive process argument, or uncontrolled stack-trace data.

Repeated identical transient errors may be coalesced while retaining first occurrence, last occurrence, and count.

### 9.8 Read models

SystemAdmin domain-event projectors maintain database backup read models. The UI and query APIs read these models; they
do not query the Database Backup Service directly for authoritative application state.

| Read model | Primary contents |
| --- | --- |
| `DatabaseProtectionSetReadModel` | PostgreSQL and Scylla protection sets, logical contents, classification, enabled policy |
| `DatabaseBackupOperationReadModel` | BackupSource, current phase, bounded progress, engine boundary, destination replicas, verification, terminal outcome, summarized errors |
| `DatabaseBackupSetReadModel` | Coordinated source-bound operations, application checkpoint, completeness, compatibility, restore-test status |
| `DatabaseRestorePointReadModel` | BackupSource, eligible restore points, dependency chains, destinations, verification and drill history |
| `DatabaseRestoreOperationReadModel` | BackupSource, restore target, approvals, phase, progress, validation, cutover readiness, terminal outcome |
| `DatabaseBackupPolicyReadModel` | Enabled BackupSources, effective policy, ScheduledTask bindings, retention, required destinations, RPO/RTO, service enforcement revision |
| `DatabaseBackupHealthReadModel` | Per-source service readiness, latest completed/verified/restore-tested ages, policy violations and alerts |
| `DatabaseRetentionReadModel` | Forecast, protected chains, legal holds, proposed and executed retention plans |

Projectors are idempotent and checkpointed. They can rebuild from the event store. A projection failure cannot authorize
service behavior or change the underlying operation outcome.

### 9.9 Queries

The redesigned SystemAdmin query surface includes:

- `GetDatabaseProtectionSetsQuery`;
- `GetDatabaseBackupPolicyQuery`;
- `GetDatabaseBackupOperationQuery`;
- `ListDatabaseBackupOperationsQuery`;
- `GetDatabaseBackupSetQuery`;
- `ListDatabaseRestorePointsQuery`;
- `GetDatabaseRestorePointQuery`;
- `GetLatestVerifiedDatabaseBackupQuery`;
- `GetLatestRestoreTestedDatabaseBackupQuery`;
- `GetDatabaseRecoveryObjectiveComplianceQuery`;
- `GetDatabaseRestoreOperationQuery`;
- `ListDatabaseRestoreDrillsQuery`;
- `GetDatabaseRetentionForecastQuery`; and
- `GetDatabaseBackupServiceHealthQuery`.

Queries return bounded read models. Large logs and manifests are accessed through authorized management endpoints or
destination tools, not embedded into actor query replies.

The UI and authorized ScheduledTask actors use these same query types. Source-specific queries require BackupSource;
list and compliance queries may use an optional BackupSource filter but return the source on every source-bound item.
The query envelope identifies and authorizes the caller independently from BackupSource.

### 9.10 Event and command envelope invariants

Every service-facing event and translated command carries:

- contract version;
- immutable source event ID;
- operation ID and optional backup-set ID;
- one concrete BackupSource;
- correlation and causation IDs;
- environment and protection-set identity;
- policy revision;
- message creation and observation times;
- producing host identity;
- monotonic service sequence or progress revision where applicable;
- operation kind and phase; and
- authenticated subject authorization in the cross-host stage.

Every translated service-event command also carries BackupSource. Every UI- or ScheduledTask-originated command and
query carries caller identity and RequestOrigin. These fields answer different questions and must not be substituted:
BackupSource selects the execution capability, while RequestOrigin identifies who invoked the DatabaseBackup contract.

At-least-once delivery is assumed in both directions. Listeners and actors must be idempotent and reject stale revisions,
sequence regressions, conflicting operation definitions, unauthorized producers, and illegal state transitions.

A detected service-sequence gap triggers reconciliation. It is not silently ignored, and later progress does not imply
that missing state was safely persisted.

## 10. UI architecture

### 10.1 Backup dashboard

The backup dashboard presents:

- protection sets rather than misleading independent physical database names;
- logical databases and keyspaces contained in each set;
- BackupSource and source-specific capability health;
- effective AWS and local destination policy;
- last completed, last verified, and last restore-tested recovery points;
- measured recovery-point age;
- current operations and bounded progress;
- capacity, retention, and replica-health warnings; and
- Database Backup Host availability and policy revision.

### 10.2 Manual backup workflow

The operator:

1. selects a protection set or coordinated backup set;
2. selects one concrete BackupSource and only policy-allowed operation and destination options;
3. sees the expected consistency and resource impact;
4. submits a SystemAdmin command;
5. receives an `OperationId` immediately; and
6. follows asynchronous progress through queries and events.

The UI never waits on one HTTP or NATS request for the duration of a backup.

### 10.3 Restore workflow

The restore UI must make destructive boundaries unmistakable. It provides:

- eligible restore-point selection;
- verification and restore-test history;
- complete dependency-chain visibility;
- target environment and fresh target identity;
- estimated size and recovery duration where known;
- approval state;
- validation results;
- explicit distinction between restore, drill, and cutover; and
- separate confirmation for cleanup of restored volumes.

Production restore and cutover require strong authorization and re-authentication appropriate to the deployment
environment. A restore request does not imply cutover approval.

### 10.4 UI concurrency

Backup UI models consume asynchronous actor updates through the existing UI-safe dispatch abstraction. They must not
block the WinForms or future WPF UI thread, poll aggressively, or mutate view state from a NATS callback thread.

## 11. Backup orchestration

### 11.1 Normal sequence

1. An authorized UI caller or ScheduledTask actor submits the same `RequestDatabaseBackupCommand` with one concrete
   BackupSource and its own RequestOrigin.
2. The DatabaseBackup Command Actor validates policy, caller identity, BackupSource, concurrency, and environment.
3. Core allocates a source-bound `OperationId` and, when required, `BackupSetId`.
4. Core establishes the required application checkpoint or maintenance mode.
5. The actor appends `DatabaseBackupRequestedEvent` and `DatabaseBackupExecutionRequestedEvent`, including
   BackupSource and the accepted checkpoint, to event-sourced state.
6. The committed execution event is published to the Database Backup Service listener and admitted only by the
   processor registered for its BackupSource.
7. The service journals and deduplicates the event, then publishes accepted or rejected service events.
8. The SystemAdmin Event Actor converts the observation into a SystemAdmin command; the Command Actor persists the
   admission result and its domain event is projected into the read model.
9. The service performs capacity, compatibility, destination, credential, and lock preflight.
10. The native database capability establishes the engine-specific backup boundary.
11. The service publishes bounded progress and boundary events; the Event Actor translates them into commands and the
    Command Actor persists accepted SystemAdmin domain events.
12. Artifacts are written to operation-specific staging or directly to an approved destination.
13. Required destination replicas are completed.
14. Artifact checksums and native verification complete.
15. The immutable manifest and catalog entry are published atomically.
16. The service publishes its terminal completion or failure event.
17. The Event Actor translates that event into a SystemAdmin command; the Command Actor validates and persists the
    terminal domain event.
18. Read-model projectors update the query models, and Core releases any application coordination state.

Failure to publish, duplicate delivery, a service-sequence gap, or loss of an acknowledgement is reconciled by source
event ID, BackupSource, `OperationId`, and service sequence. It does not create a second backup or blindly advance
actor state.

### 11.2 Consistency modes

The architecture supports three explicitly named consistency modes:

- **EngineConsistent**: each engine creates an independently valid native restore point.
- **ApplicationCheckpoint**: engine restore points are associated with a shared application event or ingestion checkpoint.
- **QuiescedApplication**: selected writers are paused, in-flight work drains, and both engines establish boundaries
  while the application is in a controlled maintenance state.

`ApplicationCheckpoint` is the preferred coordinated mode for an event-sourced system. A global ingestion pause is not
the default merely because two engines exist. `QuiescedApplication` is used only when classification and restore tests
show that checkpoint-based reconciliation cannot meet consistency requirements.

### 11.3 Concurrency

- Only one capture or restore may mutate backup state for the same physical protection set at a time.
- PostgreSQL and Scylla operations may run concurrently only when policy and measured resource budgets permit.
- Restore takes exclusive ownership of its fresh target.
- Retention cannot delete any artifact referenced by an active backup, restore, verification, or legal hold.
- A distributed lease must be fenced so that two hosts cannot both believe they own one operation.

## 12. PostgreSQL protection architecture

### 12.1 Recovery model

PostgreSQL is protected at physical cluster scope. The production recovery model combines:

- periodic verified base backups;
- continuous WAL archiving for point-in-time recovery;
- optional native incremental base backups when database size and measured change rate justify their chain complexity;
- immutable manifests connecting base backups, incrementals, and required WAL ranges; and
- recurring isolated restore drills.

Daily full backup alone is not an acceptable production recovery model when it permits the loss of an entire trading
day. The signed policy must define the allowed WAL archive lag and measured recovery-point objective.

### 12.2 Cluster scope

A PostgreSQL physical base backup contains the entire database cluster. The UI may describe the contained databases,
but a physical operation is requested once for `CorePostgresCluster`.

Selective `pg_dump`-style logical export is a different recovery product. It may supplement physical recovery for
portability or selective data repair but does not replace cluster backup and WAL recovery.

### 12.3 Backup boundary and validation

The PostgreSQL manifest records:

- cluster identity and system identifier;
- server major version;
- backup format and native tool version;
- start and end WAL positions and timelines;
- base and parent identities for incremental chains;
- required WAL archive range;
- tablespace mapping;
- native backup manifest and checksums; and
- native verification result.

### 12.4 Restore

PostgreSQL restore uses a fresh data volume and a compatible PostgreSQL runtime. It:

1. resolves and verifies the complete backup and WAL chain;
2. reconstructs a synthetic full backup when incrementals are involved;
3. restores files with correct ownership and permissions;
4. performs WAL recovery to the selected target time or restore point;
5. verifies database startup and recovery completion;
6. runs application schema and event-store validation; and
7. reports readiness without changing the active database endpoint.

Configuration files, roles, extensions, certificates, and external secrets not fully represented by physical backup are
included in the broader recovery inventory and validated separately.

## 13. ScyllaDB protection architecture

### 13.1 Recovery model

ScyllaDB protection includes:

- schema, roles, permissions, service levels, and topology metadata;
- coordinated snapshots for configured keyspaces and every participating node;
- complete immutable SSTable component sets;
- incremental SSTables and commit-log requirements when point-in-time or incremental recovery is enabled;
- cluster and token-ring identity;
- destination manifests and checksums; and
- recurring isolated restore drills.

ScyllaDB snapshots are per-node even when requested as one cluster operation. A cluster restore point is incomplete until
every required node and keyspace artifact is cataloged and verified.

### 13.2 Coordination strategy

Scylla Manager is the preferred architecture for multi-node production backup and restore coordination. Direct snapshot
coordination may be acceptable for a deliberately single-node development or early paper-trading environment, but its
manifest and operation semantics must remain compatible with the cluster model.

Direct single-node backup must not claim point-in-time or incremental recoverability until the required incremental
SSTable and commit-log chain has been demonstrated by restore testing.

### 13.3 Data classification

Scylla keyspaces may have different recovery roles:

- query projections may be rebuildable from PostgreSQL events;
- raw or enriched market data may be authoritative, externally recoverable, or only partially recoverable depending on
  the feed agreement and retention period;
- reference data may be externally reloadable but still require a fast local restore to meet RTO; and
- trading and fund projections require explicit reconciliation against their authoritative event checkpoint.

The AWS and local documents must use the classification registry rather than assume that every keyspace has identical
retention or restore priority.

### 13.4 Restore

Scylla restore uses a fresh compatible cluster or volume set. It:

1. validates cluster, version, topology, schema, and chain compatibility;
2. restores schema and required security metadata in the approved order;
3. restores complete SSTable sets and incremental recovery components;
4. starts the isolated target and waits for native readiness;
5. rebuilds derived indexes or views where required;
6. performs repair where appropriate;
7. validates keyspaces, representative partitions, counts, and application checkpoints; and
8. reports readiness without changing the active cluster endpoint.

Topology-changing restore requires an explicitly supported workflow rather than silently copying node files into a
different ring layout.

## 14. Destination-neutral storage architecture

### 14.1 Common semantics

AWS and local destinations must both support these logical behaviors:

- create an operation-specific unpublished area;
- write immutable artifacts;
- write immutable manifests and checksums;
- atomically publish or otherwise make a complete operation discoverable;
- list cataloged restore points without consulting an application database;
- retrieve artifacts with integrity validation;
- apply retention only to complete dependency chains;
- preserve legal holds;
- distinguish incomplete, failed, verified, and restore-tested operations; and
- record destination-replica health.

The mechanism may differ. Local storage may use atomic directory rename, while AWS may use immutable object keys and a
final catalog marker. The observable semantics remain equivalent.

### 14.2 Destination policy

A policy may require:

- AWS only;
- local only;
- both AWS and local; or
- one required destination plus one best-effort replica.

The terminal outcome is governed by the signed policy. Destination status is always reported independently so an
operator can distinguish a valid native capture from a missing required replica.

### 14.3 Staging

Staging is temporary working storage, not a backup destination. It must:

- be operation-scoped;
- reserve sufficient capacity before capture;
- tolerate cancellation and host restart;
- never expose partial artifacts as restore points;
- have an explicit abandoned-operation cleanup policy; and
- avoid retaining unencrypted sensitive artifacts longer than required.

### 14.4 AWS cloud backup and restore overview

AWS is the reference durability architecture. The AWS design will specialize the shared model with cloud-native object
storage, identity, encryption, immutability, lifecycle, audit, and regional recovery controls without changing the
SystemAdmin contracts or database-native artifact formats defined here.

At an architecture level, AWS backup:

- receives one immutable native capture for each engine operation;
- transfers artifacts through a destination-native, resumable data path;
- encrypts artifacts under approved AWS key policy;
- publishes immutable manifests and a reconstructable restore-point catalog;
- protects completed artifacts from ordinary service mutation or deletion;
- separates backup creation authority from retention-deletion authority;
- supports lifecycle and archival policy without breaking dependency chains; and
- reports replica, integrity, retention, and recovery-readiness state to SystemAdmin.

AWS restore:

- discovers restore points from AWS-resident catalog and manifest data;
- remains operable through break-glass recovery when Core and NATS are unavailable;
- verifies identity, authorization, encryption-key availability, manifests, and checksums before retrieval;
- retrieves the complete dependency chain into controlled staging or directly into the approved native restore path;
- restores fresh PostgreSQL and ScyllaDB targets;
- preserves the same validation and cutover gates used by local restore; and
- supports recovery planning for loss of the primary host, local backup device, or primary AWS failure boundary.

The AWS reference document will decide the exact AWS account, region, storage, key, network, object immutability,
lifecycle, audit, and cost boundaries. Those choices must not leak into common actor messages as AWS-specific fields.

### 14.5 Local backup and restore overview

Local backup follows the approved AWS logical architecture. It uses the same operation identities, manifests, artifact
checksums, catalog semantics, verification levels, restore qualification, retention dependencies, and actor workflows.
It does not pretend to provide AWS's geographic or account-level failure isolation.

At an architecture level, local backup:

- writes to an allowlisted mounted filesystem or directly attached backup target;
- uses operation-specific staging and atomic publication of completed artifacts;
- encrypts protected artifacts according to the local key-recovery design;
- reserves capacity for capture, verification, retention rollover, and at least one restore where policy requires;
- records device and filesystem identity so an unexpected replacement target cannot be mistaken for the approved one;
- supports optional offline or rotated media without weakening manifest identity; and
- reports device availability, capacity, integrity, and restore readiness independently from AWS state.

Local restore:

- discovers restore points from the local destination without consulting the application database;
- supports the same Core-independent break-glass workflow;
- verifies the local catalog, manifests, checksums, encryption keys, and complete dependency chains;
- restores fresh PostgreSQL and ScyllaDB targets through the same native recovery boundaries;
- applies the same application validation and cutover gates; and
- fails explicitly when the device, required chain member, key, capacity, or compatible runtime is unavailable.

The local reference document will define filesystem publication, mounted-device identity, encryption, capacity,
offline-copy, media rotation, and local disaster-boundary decisions. Where local infrastructure cannot meet an AWS
invariant, the difference must be declared as a capability limitation visible in policy and UI rather than hidden by
the destination abstraction.

## 15. Manifest and catalog architecture

### 15.1 Manifest requirements

Every engine operation has an immutable, versioned manifest containing at least:

- manifest schema version;
- operation and backup-set IDs;
- one concrete BackupSource;
- environment, protection set, engine, and cluster identity;
- backup type and consistency mode;
- request, policy, and producing-host revisions;
- native engine and backup-tool versions;
- start, boundary, completion, and publication timestamps;
- application checkpoint;
- base, parent, and dependency identities;
- WAL, commit-log, or other recovery ranges;
- included databases, keyspaces, tables, and exclusions;
- artifact identities, paths or object keys, lengths, and cryptographic checksums;
- encryption and key-reference metadata without secret material;
- destination replica status;
- native and checksum verification results;
- retention class and legal-hold status;
- audit identity; and
- compatibility constraints and known warnings.

### 15.2 Catalog

The catalog is a destination-resident index derived from immutable manifests. It accelerates restore-point discovery but
is not the sole record of backup validity. If the catalog is lost, it must be reconstructable by scanning and validating
manifests.

Catalog publication is the final backup step. Incomplete staging data is not cataloged.

### 15.3 Portability

Manifest meaning is identical across AWS and local destinations. A verified artifact replica may be copied between
destinations without changing its operation identity, native consistency boundary, or checksums. Destination-specific
replica metadata is appended through a signed catalog record rather than mutating the original manifest.

## 16. Restore governance

### 16.1 Restore classes

- **Restore drill**: isolated, automated or operator-requested proof of recovery with no production cutover.
- **Operational recovery**: restores a failed non-production or replaceable target.
- **Production recovery**: restores production data into fresh infrastructure and requires explicit approval.
- **Selective logical recovery**: a future separately governed data-repair workflow, not a physical cluster restore.

### 16.2 Approval boundaries

These actions require distinct authorization records:

1. requesting a production restore;
2. approving the selected restore point and fresh target;
3. approving cutover after validation; and
4. approving deletion or cleanup of old and restored targets.

The same person may be permitted to perform multiple actions in a single-operator deployment, but the audit model keeps
the decisions separate.

### 16.3 No implicit in-place restore

The architecture does not restore over active volumes. If an emergency eventually requires in-place recovery, it must
be introduced as an explicit exceptional policy with additional confirmation, offline safeguards, and tested rollback.

### 16.4 Break-glass recovery

Break-glass recovery must:

- work without the Core Actor Host, application databases, UI, or NATS;
- authenticate a recovery operator through an independent mechanism;
- read destination catalogs and verify immutable manifests;
- default to fresh targets;
- retain an append-only external audit log;
- require explicit environment and target confirmation;
- never automatically cut over application endpoints; and
- import its signed recovery record into SystemAdmin state after Core is restored.

## 17. Verification model

Verification is layered:

| Level | Meaning |
| --- | --- |
| Transfer verification | All expected bytes reached the destination and match transport expectations |
| Cryptographic verification | Artifact lengths and cryptographic checksums match the immutable manifest |
| Native verification | Database-native backup verification succeeds where supported |
| Engine restore verification | A fresh database target restores, starts, and completes native recovery |
| Application verification | Required schemas, checkpoints, representative data, and invariants pass |
| Coordinated-set verification | PostgreSQL and Scylla restore points are compatible with the selected application checkpoint |

Status terminology is strict:

- **Completed** means policy-required replicas and file/native verification succeeded.
- **Verified** means the documented non-restore verification levels succeeded.
- **Restore-tested** means an isolated engine and application restore succeeded.
- **Eligible for cutover** means a specific restored target passed the production validation policy.

No UI or event may use these terms interchangeably.

## 18. Restore drills

Restore drills are scheduled first-class operations. A coordinated drill:

1. selects the newest eligible backup set according to policy;
2. verifies every manifest, dependency, and destination replica;
3. restores PostgreSQL and ScyllaDB into unique isolated targets;
4. completes native recovery;
5. runs application validation and cross-store checkpoint checks;
6. measures actual RPO and RTO;
7. records resource use and failure details;
8. publishes a durable drill result; and
9. cleans up only after results are safely retained and cleanup is authorized by policy.

A missed or failed drill reduces recovery confidence and must be visible independently from the latest backup success.

## 19. Scheduling and retention

### 19.1 Scheduling

Database backup scheduling is driven by the SystemAdmin ScheduledTask feature, not by the DatabaseBackup actors or the
Database Backup Service. Schedule definitions are policy, not hard-coded day-of-week behavior. They cover:

- PostgreSQL base backups;
- PostgreSQL WAL archival health;
- PostgreSQL incremental backups when enabled;
- Scylla snapshots;
- Scylla incremental recovery artifacts when enabled;
- destination replication and repair;
- verification; and
- restore drills.

When work is due, an authorized ScheduledTask actor:

1. uses the same DatabaseBackup queries available to the UI to check policy, source health, current operation state, and
   recovery-objective compliance;
2. submits the same source-scoped DatabaseBackup command used by an authorized UI caller;
3. supplies RequestOrigin **ScheduledTask**, its actor identity, schedule identity, occurrence identity, idempotency
   identity, and one concrete BackupSource; and
4. receives command acceptance without waiting for the long-running backup behavior.

The UI uses RequestOrigin **Ui** and an operator identity. Both paths exercise one DatabaseBackup command and query
surface; neither receives a special execution path.

ScheduledTask does not publish DatabaseBackup execution-intent events or consume Database Backup Service events. The
DatabaseBackup Command Actor owns event publication, and the DatabaseBackup Event Actor owns service-event ingestion.
Whether ScheduledTask later receives a bounded feature-level completion event is intentionally deferred to the
ScheduledTask architecture. Until then it may observe state through DatabaseBackup queries without polling aggressively.

### 19.2 Retention

Retention is chain-aware and recovery-objective-aware:

- a base backup cannot be deleted while a retained incremental or log range depends on it;
- PostgreSQL WAL cannot be pruned while required by a retained restore point;
- Scylla snapshot, incremental SSTable, and commit-log dependencies remain complete;
- the latest successful restore-tested point is protected from normal pruning;
- legal hold overrides ordinary expiration;
- AWS and local replicas have independently reported retention state;
- incomplete operations follow a separate diagnostic retention policy; and
- deletion uses a planned, auditable operation rather than an unbounded filesystem sweep.

Retention evaluation first produces a plan. Execution revalidates dependencies and fencing before deleting anything.

## 20. Reliability and restart behavior

### 20.1 Idempotency and fencing

- `OperationId` is the idempotency boundary.
- Capability hosts persist enough external operation state to recover after restart.
- A fenced lease prevents split ownership.
- Replayed execution events resolve to the existing journaled operation rather than starting another capture or restore.
- Replayed host events translate into idempotent commands and do not append duplicate SystemAdmin domain events.
- Terminal manifests are immutable.
- Retention and cutover operations require stronger fencing than read-only verification.

### 20.2 Host restart

After restart, the Database Backup Host:

1. loads incomplete operation journals outside the protected databases;
2. reconciles native process and staging state;
3. resumes only explicitly resumable transfers or verification;
4. marks an unsafe native capture failed rather than guessing;
5. preserves diagnostic artifacts according to policy; and
6. reconciles the resulting state with the SystemAdmin actor.

During Stage 1, restarting `Api.Server` restarts both Core actors and the Database Backup Service, so the external
operation journal must support whole-process recovery. During Stage 2, Core and the Database Backup Service have
independent process lifecycles. Contract compatibility rules determine which rolling-version combinations may
communicate.

### 20.3 Partial destination failure

One destination being unavailable does not corrupt another completed replica. The host records replica-specific state
and may repair the missing replica from a verified source without recapturing the database when policy permits.

### 20.4 Backpressure

The Database Backup Host enforces bounded work queues and resource budgets. Overload produces an explicit queued,
delayed, or rejected state. It never starts unlimited concurrent copies or silently drops a required operation.

## 21. Security architecture

### 21.1 Credential separation

- Normal PostgreSQL and Scylla application credentials remain exclusive to Core.
- AWS destination credentials exist only in the Database Backup Host.
- Local destination permissions exist only in the Database Backup Host and authorized recovery tooling.
- Secrets never appear in actor messages, manifests, paths, logs, metrics, traces, or UI models.
- Backup credentials, if approved, are separate infrastructure identities with no normal application-query role.

### 21.2 Encryption

- Database-native and destination traffic is encrypted in transit where supported.
- AWS and local artifacts are encrypted at rest according to their reference architectures.
- Key references may appear in manifests; key material may not.
- Recovery procedures include independent access to required keys.
- Losing Core must not make every backup undecryptable.

### 21.3 Authorization

NATS subjects, management endpoints, destination access, restore, cutover, legal hold, and retention deletion each use
least-privilege authorization. Network location alone is not authorization.

### 21.4 Artifact protection

- Completed artifacts are immutable under normal service credentials.
- A compromised normal application actor cannot delete backup history.
- Retention deletion uses a narrower, separately authorized capability.
- Restore never trusts a catalog record without validating its manifest and checksums.
- Backup logs avoid exposing sensitive row data or credentials.

## 22. Observability

### 22.1 Control events versus telemetry

NATS carries domain-significant outcomes such as backup completion, recovery objective violation, or restore readiness.
General logs, metrics, and traces use each host's HTTP/OpenTelemetry observability path.

### 22.2 Required metrics

At minimum, the architecture exposes:

- operation counts by state, engine, type, and destination;
- queue depth and queue wait duration;
- backup capture, transfer, verification, restore, and validation duration;
- bytes captured, transferred, retained, and restored;
- transfer throughput and retry count;
- PostgreSQL WAL archive lag and oldest required WAL age;
- Scylla node and keyspace backup completeness;
- destination capacity and forecast exhaustion;
- latest completed, verified, and restore-tested age;
- measured RPO and RTO;
- failed restore drills;
- manifest, checksum, and native verification failures;
- lease conflicts and stale-operation recovery; and
- effective versus expected policy revision.

High-cardinality identifiers such as `OperationId` belong in logs and traces, not unbounded metric labels.

### 22.3 Alerts

Alerts include:

- missed or overdue backup;
- WAL archival failure or unacceptable lag;
- incomplete Scylla cluster backup;
- required destination unavailable;
- insufficient staging or destination capacity;
- verification failure;
- no restore-tested point within policy;
- recovery objective violation;
- policy-revision mismatch;
- repeated operation restart or lease conflict; and
- retention unable to run safely.

## 23. Configuration architecture

### 23.1 Service configuration boundary

Stage 1 uses a dedicated, validated Database Backup Service configuration section inside `Api.Server`. Backup code
receives only its typed configuration and secret references, but this is not a hard security boundary because all
components share one process.

Stage 2 moves that configuration unchanged in meaning into the Database Backup Host configuration root. Core and the
host then receive only the configuration and secret references required by their responsibilities.

Core configuration contains DatabaseBackup policy, authorization, protection-set identity, enabled BackupSources,
source-specific service capability expectations, recovery objectives, and ScheduledTask configuration. Service
configuration contains supported BackupSources, staging, destination, native backup, throttling, journal, and
break-glass settings.

Each host receives only the configuration and secret references required by its responsibility.

### 23.2 Configuration ownership

Core owns authoritative non-secret configuration:

- protection sets and data classification;
- DatabaseBackup recovery objectives and source-specific policy;
- ScheduledTask definitions, cadence, occurrence state, and DatabaseBackup schedule bindings;
- required destinations;
- verification and restore-drill policy;
- retention classes; and
- authorization policy references.

The Database Backup Service owns bootstrap configuration that will later allow its host to contact Core:

- NATS and host identity;
- destination endpoints and secret references;
- staging location and hard resource ceilings;
- database-native backup endpoint or agent location according to the approved credential model; and
- break-glass recovery configuration.

Configuration is versioned. Startup fails closed when mandatory configuration is absent, unsafe, or incompatible. Normal
development defaults to disabled or dry-run operation and cannot target production protection sets.

### 23.3 Aspire evolution

Aspire is the preferred future development and orchestration model after extraction:

```text
TomasAI.IFM.AppHost
  +-- TomasAI.IFM.Api.Server.Host
  |     +-- SystemAdmin actor and authoritative control state
  +-- TomasAI.IFM.Api.DatabaseBackup.Host
  |     +-- Database Backup Service and execution journal
  +-- NATS
  +-- PostgreSQL
  +-- ScyllaDB
  +-- observability resources
  +-- destination resource references appropriate to the environment
```

The Aspire AppHost may provide project discovery, startup ordering, development resource references, health visibility,
and local developer orchestration. Shared Service Defaults may configure telemetry, health, resilience baselines, and
service discovery for executable hosts.

After extraction, Aspire does not:

- merge Core and backup into one process;
- distribute all secrets to all resources;
- replace NATS actor contracts with implicit in-process calls;
- own authoritative operation state;
- execute backup behavior itself;
- remain a mandatory runtime dependency after the hosts are started; or
- remove the requirement for production deployment, recovery, and security procedures outside a developer dashboard.

Before extraction, the Database Backup Service must be testable through an `Api.Server` service fixture without starting
the complete application estate. After extraction, the Database Backup Host must also be runnable and testable directly.
Integration environments may then use Aspire to compose only the host, NATS, disposable databases, and disposable backup
destinations required by a backup or restore test.

## 24. Credential and native-executor boundary

The existing Core-only application credential rule remains intact. Native physical backup nevertheless requires an
infrastructure mechanism that can communicate with PostgreSQL replication/backup interfaces and Scylla administration
interfaces.

The Database Backup Service owns native backup and restore behavior in both deployment stages. During Stage 1 it shares
the `Api.Server` process with Core, but actors never run the native executor. During Stage 2 the executor moves with the
service into the separate host.

### 24.1 Backup-only infrastructure identities

The host receives separate, least-privilege native backup identities:

- a PostgreSQL replication/backup identity that cannot act as a normal application user; and
- a Scylla Manager/agent or restricted administration identity that cannot provide arbitrary CQL application access.

This identity separation means:

- backup CPU, memory, process, and I/O coordination stays outside actor execution and moves outside the Core process in
  Stage 2;
- the host can operate destination transfer and native capture as one recoverable workflow;
- native tools follow their intended network model;
- normal application connection strings, database users, and query APIs remain exclusive to Core; and
- the backup host cannot become an alternate application data-access path.

Constraints:

- Stage 1 cannot provide strong process-level credential separation from Core;
- Stage 2 is an explicit infrastructure exception to the statement that no satellite holds any database endpoint credential;
- network and identity policies must technically prevent application queries; and
- break-glass access requires stronger independent controls.

### 24.2 Engine-local agents

If PostgreSQL or Scylla requires an engine-local agent, that agent is database infrastructure controlled by the Database
Backup Service. It is not hosted in Core, does not contain business actors, and does not transfer application records
through Core.

An engine-local agent may:

- expose an allowlisted database-native backup operation;
- coordinate a local snapshot or filesystem boundary;
- apply engine-local CPU and I/O limits;
- write an opaque artifact into approved staging; and
- report native consistency and verification evidence to the Database Backup Service.

The Database Backup Service remains responsible for the operation journal, destination transfer, manifest, catalog,
retention, restore workflow, and status sent to SystemAdmin. An agent cannot accept UI or general actor commands.

### 24.3 Rejected actor placement

Running the native backup executor in a SystemAdmin or business actor is rejected. The transitional hosted service may
share the `Api.Server` process, but native tooling, transfer load, destination dependencies, backup credentials, and
long-running behavior stay behind the Database Backup Service boundary.

The architecture must not replace native physical backup with actor queries or bulk data over NATS.

## 25. Failure model

The AWS and local designs must address at least:

- Core unavailable before dispatch;
- Core unavailable after dispatch;
- NATS unavailable or partitioned;
- Database Backup Host restart;
- duplicate or reordered messages;
- native database unavailable;
- PostgreSQL WAL archive gap;
- incomplete Scylla node snapshot;
- staging exhaustion;
- AWS unavailable;
- local destination disconnected or full;
- required encryption key unavailable;
- credential expiration;
- transfer interruption;
- corrupted artifact or manifest;
- missing base, parent, WAL, commit-log, or SSTable dependency;
- restore target incompatible with engine version or topology;
- application validation failure;
- cutover failure;
- retention race with restore; and
- complete loss of Core, NATS, and active database volumes.

Every failure has an explicit terminal or recoverable state. No failure is represented only by a free-form log message.

## 26. Compatibility and versioning

- Actor contracts are versioned and support rolling host upgrades where safe.
- Manifest schemas are versioned independently from actor contracts.
- Readers reject unknown mandatory fields or unsupported native formats safely.
- Engine major-version compatibility is part of restore eligibility.
- `Diff` terminology is retired. New contracts use precise `Base`, `Incremental`, `LogArchive`, `Snapshot`, or
  engine-specific operation terminology.
- Existing serialized backup messages are deprecated rather than silently reinterpreted.
- An operation records the exact host, policy, manifest, engine, and tool versions involved.

## 27. Architecture acceptance criteria

The shared architecture is satisfied only when the AWS and local designs conform to these invariants:

1. The SystemAdmin DatabaseBackup feature has exactly three actors with explicit roles: Command Actor for event-sourced
   transitions, Event Actor for service-event-to-command translation, and Query Actor for projected read models.
2. PostgreSQL is backed up once per physical cluster boundary.
3. ScyllaDB cluster completeness accounts for every required node and keyspace.
4. NATS never transports native backup payloads.
5. AWS and local destinations share manifest and restore-point semantics.
6. Required destination success is policy-defined and visible per replica.
7. PostgreSQL production recovery includes WAL/PITR rather than daily full backup alone.
8. Incremental chains cannot be pruned into an unrestorable state.
9. No restore point is published before verification.
10. Restore uses fresh targets and requires separate cutover approval.
11. A break-glass restore works without Core, NATS, or the protected databases.
12. Backup catalogs are reconstructable from destination-resident immutable manifests.
13. Restore drills measure and report real RPO and RTO.
14. No satellite receives normal application database credentials or becomes a general query path.
15. Secrets never enter actor messages, artifacts, manifests, logs, metrics, or UI models.
16. Legacy per-database and `.bak` assumptions are absent from the replacement designs.
17. All native backup and restore behavior executes behind the Database Backup Service boundary and never in an actor;
    the service starts inside `Api.Server` and is extraction-ready.
18. SystemAdmin owns authoritative control state while the service owns recoverable execution journals.
19. Stage 1 safely recovers from a shared `Api.Server` restart; Stage 2 allows Core and the service to restart, deploy,
    throttle, and fail independently.
20. Aspire later composes the extracted hosts as separate resources without becoming an authoritative state or runtime
    dependency.
21. SystemAdmin execution intent is published to the service only after its domain event is durably committed.
22. Service observations update SystemAdmin state only after the Event Actor translates them into commands and the
    Command Actor appends accepted domain events.
23. Database backup queries read event-projected read models rather than calling the execution service directly.
24. Progress and errors are event sourced at bounded operational checkpoints without persisting per-byte or per-file
    telemetry as domain state.
25. SystemAdmin contains separate DatabaseBackup and ScheduledTask features with separate actor state and
    responsibilities.
26. UI callers and ScheduledTask actors use the same DatabaseBackup command and query contracts.
27. Every DatabaseBackup domain event, execution-intent event, service event, and translated service-event command
    carries one concrete BackupSource.
28. AWS and local processing use the same event type names and schemas; no source-specific event class is introduced.
29. An operation is permanently bound to one BackupSource, and a multi-source policy creates distinct operations under
    one BackupSetId.
30. A service response is accepted only when its BackupSource matches the operation and its producing host is
    authorized for that source.
31. ScheduledTask actors do not publish DatabaseBackup execution events, consume raw service events, or call the
    Database Backup Service directly.

## 28. Decisions required for sign-off

The following decisions must be accepted or revised before this overview is signed off:

| Decision | Proposed direction |
| --- | --- |
| Native executor and credential placement | Database Backup Service using dedicated backup-only infrastructure identities or service-controlled engine-local agents; actor execution is rejected |
| Physical protection units | `CorePostgresCluster`, `CoreScyllaCluster`, and optional `CoordinatedCoreBackupSet` |
| PostgreSQL recovery model | Periodic base backup plus continuous WAL/PITR; incremental base backups only when measurements justify them |
| Scylla coordination | Scylla Manager for multi-node production; direct snapshot permitted only for explicitly single-node early environments |
| Coordinated consistency | Application checkpoint by default; quiesced application only when proven necessary |
| Restore target | Fresh target by default; no implicit in-place restore |
| Production cutover | Separate approval after successful validation |
| Destination model | AWS, local, or both, with policy declaring required replicas |
| Break-glass restore | Mandatory and independent of Core and NATS |
| Redis | Excluded while cache-only |
| JetStream | Separate infrastructure recovery design coordinated by checkpoint where required |
| Legacy contracts | Deprecated and replaced; serialized values are not repurposed |
| SystemAdmin feature model | Separate DatabaseBackup and ScheduledTask features; DatabaseBackup has exactly three roles: Command Actor owns state transitions, Event Actor processes service events into commands, Query Actor serves projected read models; ScheduledTask owns a separate actor set whose design is deferred |
| Source-independent contracts | AWS and local use identical command, query, domain-event, execution-event, and service-event types |
| BackupSource | Required concrete value on every DatabaseBackup event; initial values are `AwsCloud` and `LocalWorkstation`; no `Any` or `Both` event value |
| Multi-source operation | One source-bound `OperationId` per BackupSource, coordinated through a shared `BackupSetId` |
| UI and ScheduledTask integration | Both invoke the same DatabaseBackup commands and queries with distinct RequestOrigin and requesting identity |
| ScheduledTask events | Deferred to the ScheduledTask architecture; ScheduledTask does not consume raw Database Backup Service events |
| Backup execution deployment | Stage 1: isolated hosted service inside `Api.Server`; Stage 2: extracted `TomasAI.IFM.Api.DatabaseBackup.Host` Aspire resource |
| Service communication | Stage 1 committed in-process event listeners; Stage 2 equivalent NATS event listeners; service events are translated into SystemAdmin commands; HTTP remains limited to observability, diagnostics, and secured break-glass recovery |
| State ownership | SystemAdmin event sourcing owns all authoritative backup state and read-model projections; Database Backup Service owns recoverable execution journals; destinations own immutable recovery evidence |
| Aspire role | Future host orchestration, development composition, and shared observability defaults; not a correctness or state dependency |

The AWS architecture resolves AWS-specific durability, identity, encryption, lifecycle, regional, and cost decisions.
The local architecture will resolve filesystem, device, capacity, encryption, offline-copy, and local disaster-boundary
decisions while preserving the approved common model.

## 29. References

- [AWS cloud backup and restore architecture](AWS-Cloud-Backup-Restore-Architecture.md)
- [PostgreSQL current backup and restore documentation](https://www.postgresql.org/docs/current/backup.html)
- [PostgreSQL `pg_basebackup`](https://www.postgresql.org/docs/current/app-pgbasebackup.html)
- [PostgreSQL continuous archiving and point-in-time recovery](https://www.postgresql.org/docs/current/continuous-archiving.html)
- [ScyllaDB backup and restore](https://docs.scylladb.com/manual/stable/operating-scylla/procedures/backup-restore/backup.html)
- [ScyllaDB Manager backup](https://manager.docs.scylladb.com/stable/backup/)
- [NATS JetStream disaster recovery](https://docs.nats.io/running-a-nats-service/nats_admin/jetstream_admin/disaster_recovery)

## 30. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-10 | Created the shared PostgreSQL/ScyllaDB cluster backup and restore architecture, SystemAdmin actor and UI model, destination-neutral contracts, AWS/local boundaries, restore governance, and decisions required before AWS design. |
| 0.2 | 2026-08-10 | Made the Database Backup Service a mandatory separate executable host, clarified actor control state versus service execution state, defined the NATS and HTTP boundaries, independent lifecycle and reconciliation, and the future Aspire project-resource model. |
| 0.3 | 2026-08-10 | Clarified the staged deployment: the isolated Database Backup Service initially runs inside `Api.Server` behind an asynchronous service gateway, then moves with unchanged behavior and contracts into a separate Aspire-managed host. |
| 0.4 | 2026-08-10 | Defined the bidirectional event contract: committed SystemAdmin execution events drive the service listener; service progress and outcomes are translated into idempotent SystemAdmin commands; accepted state, progress, and errors are persisted as domain events and projected into backup read models. |
| 0.5 | 2026-08-10 | Defined the three-actor SystemAdmin model: Command Actor owns event-sourced transitions and outbound intent, Event Actor validates service events and translates them into commands, and Query Actor serves event-projected read models. |
| 0.6 | 2026-08-10 | Approved the shared architecture after scoping the three-actor model to the SystemAdmin DatabaseBackup feature, establishing ScheduledTask as a separate shared command/query caller, making every DatabaseBackup event source-independent through mandatory BackupSource, and deferring ScheduledTask event design. |
