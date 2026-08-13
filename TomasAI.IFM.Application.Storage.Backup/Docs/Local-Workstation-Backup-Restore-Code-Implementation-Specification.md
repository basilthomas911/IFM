# Local Workstation Database Backup and Restore Code Implementation Specification

**Status:** Phase 10 development implementation and runtime qualification complete

**Version:** 1.5

**Date:** 2026-08-12

**Implementation target:** `BackupSource.LocalWorkstation`

**Architecture authority:**

- `Database-Backup-Architecture-Overview.md`, version 0.8
- `Local-Backup-Restore-Architecture.md`, version 0.3
- `AWS-Cloud-Backup-Restore-Architecture.md`, version 0.4

## 1. Purpose

This specification turns the approved common and local-workstation backup architecture into an ordered code plan for
IFM. It defines the projects, folders, contracts, actors, storage, messaging, dependency-injection registrations,
standalone host, deferred Docker/Aspire deployment, console and WinForms integration, tests, migration steps, and acceptance gates needed to implement
local PostgreSQL and ScyllaDB backup and restore.

The first paper-trading implementation is local-workstation only. Its public actor contracts remain source-neutral and
carry `BackupSource.LocalWorkstation` as data. The later AWS implementation must use the same commands, queries,
domain events, service events, and read models with `BackupSource.AwsCloud`.

This document is a code implementation specification. It does not authorize running a native backup, changing a
database, deleting retained data, or performing a restore.

## 2. Binding decisions

The following decisions are fixed for this implementation:

1. `DatabaseBackup` is a feature beneath `TomasAI.IFM.Domain.SystemAdmin`.
2. It has exactly three actor roles: Command, Event, and Query.
3. The Command Actor owns authoritative event-sourced state transitions and publishes committed execution intent.
4. The Event Actor consumes service observations and translates them into commands for the Command Actor.
5. The Query Actor reads `ISystemAdminDbContext` projections only.
6. UI, Console, and ScheduledTask use the same commands and queries through `IActorProducer`.
7. UI and Console use `IActorEventListener`/`NatsActorEventListener` for public domain-event notifications.
8. The standalone Database Backup Host uses `IJSActorEventListener`/`NatsJetStreamEventListener` for durable inbound
   execution-intent events.
9. The host uses `IJSActorProducer`/`NatsJetStreamActorProducer` for durable outbound service events.
10. Core Event actors continue to use the existing `IJSActorConsumer`/`NatsJetStreamActorConsumer` runtime path.
11. The host never writes `SystemAdminDbContext` and never owns application aggregate state.
12. `SystemAdminDbContext` is a rebuildable PostgreSQL projection; it is not authoritative.
13. The local host journal is an embedded transactional SQLite database on an encrypted persistent mount outside the
    protected PostgreSQL and Scylla clusters.
14. Destination manifests, catalog records, and final run evidence are immutable recovery evidence.
15. PostgreSQL and Scylla operations use high-level, typed capability interfaces. Actor messages cannot contain raw
    SQL, CQL, shell commands, connection strings, credentials, or arbitrary native arguments.
16. `BackupSource.None` is invalid for accepted operations and source-bound events. It is allowed only as an explicit
    no-source-filter value on documented list/compliance queries.
17. Restore always targets a fresh target. Production cutover requires a separate command and approval.
18. The initial runtime supports one `LocalWorkstation` processor. The processor registry and common contracts must
    still reject unsupported sources instead of silently ignoring them.

## 3. Non-goals for the first implementation

The following work is outside the LocalWorkstation implementation gates:

- AWS execution adapters, DynamoDB journal implementation, S3, AWS Backup, or cross-account replication;
- automatic in-place production restore;
- automatic production cutover after a successful restore;
- direct database utility execution by UI, Console, ScheduledTask, Core actors, or `Api.Server`;
- storing raw native output, stack traces, credentials, paths, object keys, or high-frequency samples in actor events or
  `SystemAdminDbContext`;
- one actor per database engine or per source;
- changing the future WPF application beyond keeping shared models and contracts compatible;
- using HTTP from the WinForms UI for this feature; and
- using the existing projection-migration executable as the backup processor.
- adding Aspire orchestration during functional paper-trading development; and
- designing MarketData, TradeBroker, GeneralLedger, or other future capability hosts in this implementation.

## 4. Current-state migration constraint

Before Phase 0, `TomasAI.IFM.Application.Storage.Backup` was an executable whose `Program.cs` performed projection migration
and reconciliation for Reference, Securities, Fund, and Market projections. It is not a database backup application.
Repurposing it in place would mix unrelated operational tools and make deployment unsafe.

Phase 0 renames that existing executable and namespace to:

```text
TomasAI.IFM.Application.Storage.ProjectionMigration/
  TomasAI.IFM.Application.Storage.ProjectionMigration.csproj
  Program.cs
  ProjectionMigrationCommandLine.cs
```

The rename must preserve its behavior and tests. The new backup orchestration library may then use the unambiguous
name `TomasAI.IFM.Application.DatabaseBackup`; the Database Backup Console and standalone host remain separate executable
projects. The architecture documents may remain under the current documentation directory until a documentation-only
move is approved.

The legacy SystemAdmin `BackupDatabase` command/event path and the current per-database WinForms workflow remain in
place until the new end-to-end gates pass. They are removed in the final migration gate, not reused as aliases for the
new contracts.

## 5. Target solution structure

### 5.1 Projects

| Project | Output | Responsibility |
| --- | --- | --- |
| `TomasAI.IFM.Domain.SystemAdmin.Shared` | library | Source-neutral DatabaseBackup IDs, enums, commands, queries, events, service events, and read models |
| `TomasAI.IFM.Domain.SystemAdmin` | library | DatabaseBackup Command, Event, and Query actors and aggregate state |
| `TomasAI.IFM.Application.Storage` | library | `ISystemAdminDbContext`, projections, SQL, schema, checkpointing, and repository implementation |
| `TomasAI.IFM.Application.Api.Nats.Client` | library | Typed DatabaseBackup command/query client wrappers over `IActorProducer` |
| `TomasAI.IFM.Application.DatabaseBackup` | library | Destination-neutral service orchestration, journal and native capability ports, workflow coordinators |
| `TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation` | library | SQLite journal, local vault/media, manifest/catalog, PostgreSQL, Scylla, filesystem, signing, and native process adapters |
| `TomasAI.IFM.Api.DatabaseBackup.Host` | worker executable | Standalone durable execution listener, processor registry, work dispatch, reconciliation, service-event outbox, health; packaged for Ubuntu 24.04 later |
| `TomasAI.IFM.Application.DatabaseBackup.Console` | console executable | Operator commands, queries, follow mode, deterministic exit codes |
| existing `TomasAI.IFM.UI.Net.*` projects | WinForms | Legacy views with new NATS-only models/view-models and public event consumption |
| `TomasAI.IFM.Application.Storage.ProjectionMigration` | console executable | Renamed existing projection migration tool; unrelated to backup execution |

Each new production project requires a matching unit-test project. Integration tests may be grouped where the
solution already has an appropriate test assembly, as specified in section 23.

### 5.2 Dependency direction

```text
Domain.SystemAdmin.Shared <- Domain.SystemAdmin <- Api.Server composition
             ^                    ^
             |                    +-- Application.Storage
             |
             +-- Application.Api.Nats.Client <- UI.Net / DatabaseBackup.Console
             |
             +-- Application.DatabaseBackup <- Framework.Storage.DatabaseBackup.LocalWorkstation
                            ^                             ^
                            +------ Api.DatabaseBackup.Host ------+

Shared.EventModelActor.Contracts <- Framework.Messaging.Nats
```

Rules:

- Shared domain contracts reference `TomasAI.IFM.Shared`, never a framework or host project.
- Domain actors do not reference local or AWS execution adapters.
- Application orchestration depends on interfaces, not SQLite, filesystem, process, PostgreSQL, or Scylla types.
- The framework LocalWorkstation project implements application capability ports.
- Only executable composition roots select concrete adapters.
- `Api.Server` does not receive backup-native credentials or journal access.

### 5.3 Required folders

```text
TomasAI.IFM.Domain.SystemAdmin.Shared/DatabaseBackup/
  Contracts/
  Commands/
  Queries/
  Events/Domain/
  Events/Execution/
  Events/Service/
  Models/
  ReadModels/

TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup/
  Command/Actor/
  Command/State/
  Command/Validation/
  Event/Actor/
  Event/Translation/
  Query/Actor/
  Projection/
  Startup/

TomasAI.IFM.Application.Storage/SystemAdminDb/
  Commands/
  Queries/
  Models/
  Schema/
  Sql/

TomasAI.IFM.Application.DatabaseBackup/
  Contracts/
  Execution/
  Journal/
  Manifest/
  Native/PostgreSql/
  Native/Scylla/
  Reconciliation/
  Retention/
  Validation/

TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation/
  Journal/Sqlite/
  Native/PostgreSql/
  Native/Scylla/
  Storage/OnlineVault/
  Storage/OfflineMedia/
  Manifest/
  Security/
  Process/
  Startup/

TomasAI.IFM.Api.DatabaseBackup.Host/
  Configuration/
  HostedServices/
  Health/
  Startup/

TomasAI.IFM.Application.DatabaseBackup.Console/
  Commands/
  Formatting/
  Follow/
```

## 6. Common contract model

### 6.1 Identifiers

Add strongly typed, MessagePack-compatible actor identities in
`Domain.SystemAdmin.Shared/DatabaseBackup/Contracts`:

| Type | Backing value | Use |
| --- | --- | --- |
| `DatabaseRecoveryOperationId` | `Guid` | Immutable idempotency, actor entity, and operation key |
| `DatabaseBackupSetId` | `Guid` | Coordinated protection-set operation group |
| `DatabaseProtectionSetId` | validated `string` | Logical protected cluster/set identity |
| `DatabaseRestorePointId` | validated `string` | Immutable cataloged recovery point identity |
| `DatabaseRetentionPlanId` | `Guid` | Approved, revision-bound retention plan |
| `DatabaseBackupPolicyId` | validated `string` | Environment/policy aggregate identity |
| `DatabaseBackupHostId` | validated `string` | Authorized producing host identity |
| `DatabaseArtifactId` | validated `string` | Immutable logical artifact identity |
| `DatabaseArtifactReplicaId` | validated `string` | One source/destination replica identity |

`DatabaseRecoveryOperationId` implements the solution's `IActorEntityId` convention. It is allocated by Core after
command validation and never by the native capability. Strings are length-bounded, ordinal, trimmed, and reject path
separators or control characters where they may later influence a safe logical name.

### 6.2 Required enums

Define the following source-neutral enums with explicit numeric values and an `Unknown` or `None` zero value:

- `BackupSource`: `None = 0`, `LocalWorkstation = 1`, `AwsCloud = 2`;
- `DatabaseRecoveryOperationKind`: Backup, Verification, Restore, RestoreDrill, Cutover, Reconciliation, Retention;
- `DatabaseEngine`: PostgreSql, ScyllaDb;
- `DatabaseRecoveryPhase` covering the architecture state machines;
- `DatabaseRecoveryOutcome`: None, Succeeded, Failed, Cancelled, Rejected, Degraded;
- `DatabaseRequestOrigin`: UI, Console, ScheduledTask, Reconciliation;
- `DatabaseArtifactReplicaState`: Planned, Staging, Transferring, Durable, Verified, Published, Failed, Deleted;
- `DatabaseVerificationLevel`: Checksum, Native, IsolatedRestore, ApplicationValidation;
- `DatabaseErrorClassification`: Retryable, OperatorActionable, Terminal;
- `DatabaseRestoreClass`: Drill, ProductionRecovery;
- `DatabaseCutoverState`; and
- `DatabaseServiceCapabilityState`.

Unknown future enum values are rejected at behavior boundaries. They are not coerced to a default.

### 6.3 Envelope fields

Every externally submitted command or query includes:

- contract version;
- immutable request/idempotency ID;
- caller identity and roles/authorization reference;
- `DatabaseRequestOrigin`;
- correlation and causation IDs;
- environment identity; and
- created time in UTC.

Every source-bound execution event, service event, translated command, and domain event additionally includes:

- `DatabaseRecoveryOperationId` and optional `DatabaseBackupSetId`;
- one concrete `BackupSource`;
- protection-set identity;
- policy revision;
- immutable source event ID;
- operation kind and phase;
- producing host identity where applicable;
- source domain-event revision or monotonic service sequence; and
- observed time in UTC.

Use `DateTimeOffset` normalized to UTC for transport and persistence. Do not use local wall-clock time in contracts.
Collections have documented maximum counts. Public messages use bounded safe diagnostic references rather than paths
or logs.

## 7. Public commands

Create the following request commands under `Domain.SystemAdmin.Shared/DatabaseBackup/Commands`:

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

All commands use the normal IFM `ICommand<TEntityId>` pattern. Commands that create an operation target the stable
DatabaseBackup command mailbox with a request entity identity and return a `DatabaseOperationAcceptedResult` containing
the allocated `OperationId`, optional `BackupSetId`, accepted source, policy revision, and initial state. Commands that
change an existing operation use its `DatabaseRecoveryOperationId` as the actor entity identity.

Minimum behavior-specific payloads are:

| Command | Additional payload |
| --- | --- |
| Request backup | protection set, concrete source, consistency mode, required logical destinations, expected policy revision |
| Cancel backup/restore | operation ID, expected state revision, safe reason |
| Request restore | restore point, concrete source, fresh target descriptor, restore class, expected manifest revision |
| Approve restore | operation ID, approval identity/reference, expected state revision |
| Approve cutover | operation ID, separate approval identity/reference, validation revision, expected state revision |
| Restore drill | restore point, concrete source, disposable target profile, validation profile |
| Policy update | policy identity, expected revision, enabled sources, protected sets, recovery objectives, retention and verification policy |
| Legal hold | restore point or backup-set scope, reason/reference, expected revision |
| Retention evaluation | concrete source, policy revision, evaluation boundary |
| Retention execution | concrete source, retention plan ID and exact plan revision, approval reference |

No command accepts a filesystem path, bucket name, native program, arbitrary native option, or credential.

### 7.1 Internal translated commands

Create internal actor commands for Event Actor translation:

- `RecordDatabaseOperationAdmissionCommand`
- `RecordDatabaseOperationStartedCommand`
- `RecordDatabaseOperationProgressCommand`
- `RecordDatabaseBackupBoundaryCommand`
- `RecordDatabaseArtifactReplicaCommand`
- `RecordDatabaseOperationVerificationCommand`
- `RecordDatabaseOperationErrorCommand`
- `RecordDatabaseRestoreReadyForCutoverCommand`
- `CompleteDatabaseOperationCommand`
- `FailDatabaseOperationCommand`
- `RecordDatabaseOperationCancelledCommand`
- `RecordDatabaseBackupPolicyStatusCommand`
- `RecordDatabaseRetentionResultCommand`
- `ReconcileDatabaseBackupServiceStateCommand`
- `RecordDatabaseBackupServiceCapabilityCommand`
- `RecordDatabaseRecoveryRunStatisticsCommand`

These preserve the service event ID, source, service sequence, host, correlation, causation, and observation time. They
cannot be constructed from UI/Console payloads and are authorized only for the DatabaseBackup Event Actor identity.

## 8. Execution-intent and service-event contracts

### 8.1 Core-to-host execution events

Add these source-neutral events under `Events/Execution`:

- `DatabaseBackupExecutionRequestedEvent`
- `DatabaseBackupCancellationRequestedEvent`
- `DatabaseBackupVerificationRequestedEvent`
- `DatabaseRestoreExecutionRequestedEvent`
- `DatabaseRestoreCancellationRequestedEvent`
- `DatabaseRestoreDrillRequestedEvent`
- `DatabaseCutoverExecutionRequestedEvent`
- `DatabaseRetentionEvaluationRequestedEvent`
- `DatabaseRetentionExecutionRequestedEvent`
- `DatabaseBackupPolicyActivatedEvent`
- `DatabaseBackupReconciliationRequestedEvent`

Each event is a complete bounded work order. The host may validate or reject it, but it must not synchronously query
Core for missing policy or restore-target fields after admission.

### 8.2 Host-to-Core service events

Add these source-neutral events under `Events/Service`:

Backup:

- `DatabaseBackupServiceAcceptedEvent`
- `DatabaseBackupServiceRejectedEvent`
- `DatabaseBackupServiceStartedEvent`
- `DatabaseBackupServiceProgressEvent`
- `DatabaseBackupBoundaryEstablishedEvent`
- `DatabaseBackupArtifactReplicaUpdatedEvent`
- `DatabaseBackupVerificationCompletedEvent`
- `DatabaseBackupServiceErrorEvent`
- `DatabaseBackupServiceCompletedEvent`
- `DatabaseBackupServiceFailedEvent`
- `DatabaseBackupServiceCancelledEvent`

Restore and drill:

- `DatabaseRestoreServiceAcceptedEvent`
- `DatabaseRestoreServiceRejectedEvent`
- `DatabaseRestoreServiceStartedEvent`
- `DatabaseRestoreServiceProgressEvent`
- `DatabaseRestoreValidationCompletedEvent`
- `DatabaseRestoreReadyForCutoverEvent`
- `DatabaseRestoreDrillCompletedEvent`
- `DatabaseRestoreServiceErrorEvent`
- `DatabaseRestoreServiceCompletedEvent`
- `DatabaseRestoreServiceFailedEvent`
- `DatabaseRestoreServiceCancelledEvent`

Policy, retention, reconciliation, and statistics:

- `DatabaseRecoveryRunStatisticsCapturedEvent`
- `DatabaseBackupPolicyAppliedEvent`
- `DatabaseBackupPolicyRejectedEvent`
- `DatabaseRetentionPlanCreatedEvent`
- `DatabaseRetentionExecutionCompletedEvent`
- `DatabaseRetentionExecutionFailedEvent`
- `DatabaseBackupServiceReconciliationEvent`
- `DatabaseBackupServiceCapabilityChangedEvent`

Progress events occur only at phase changes, configured byte/percentage thresholds, replica lifecycle changes,
verification milestones, bounded heartbeats, or terminal transitions. Native output lines and per-file changes are not
events.

### 8.3 Authoritative domain events

The Command Actor appends new domain events under `Events/Domain`. At minimum implement:

- request, authorization, and execution-requested events for backup, restore, drill, retention, and cutover;
- admission-recorded, started, bounded-progress, native-boundary, replica-recorded, verification/validation-recorded,
  ready-for-cutover, error-recorded, statistics-recorded, terminal success, failure, and cancellation events;
- backup-set checkpoint/completeness events;
- policy revision and service-enforcement events;
- legal-hold events; and
- service capability and reconciliation events.

Every source-bound domain event carries the concrete `BackupSource`. The new types must not reuse or deserialize into
the deprecated `DatabaseBackupEvent`, `DatabaseBackupCompleteEvent`, or `DatabaseBackupFailEvent` contracts.

## 9. Query contracts and read models

Create these queries under `DatabaseBackup/Queries`:

- `GetDatabaseProtectionSetsQuery`
- `GetDatabaseBackupPolicyQuery`
- `GetDatabaseBackupOperationQuery`
- `ListDatabaseBackupOperationsQuery`
- `GetDatabaseBackupSetQuery`
- `ListDatabaseRestorePointsQuery`
- `GetDatabaseRestorePointQuery`
- `GetLatestVerifiedDatabaseBackupQuery`
- `GetLatestRestoreTestedDatabaseBackupQuery`
- `GetDatabaseRecoveryObjectiveComplianceQuery`
- `GetDatabaseRestoreOperationQuery`
- `ListDatabaseRestoreDrillsQuery`
- `GetDatabaseRetentionForecastQuery`
- `GetDatabaseBackupServiceHealthQuery`
- `GetDatabaseRecoveryRunStatsQuery`

Single-item queries use a concrete identity and return a nullable bounded read model through the existing
`ServiceResult<T>` convention. List queries require page size and continuation identity with a hard maximum page size.
Source-specific queries require a concrete source. Only list/compliance queries documented as cross-source accept
`BackupSource.None`, and every returned row still contains its concrete source.

Create the following read models:

- `DatabaseProtectionSetReadModel`
- `DatabaseBackupOperationReadModel`
- `DatabaseBackupSetReadModel`
- `DatabaseRestorePointReadModel`
- `DatabaseRestoreOperationReadModel`
- `DatabaseBackupPolicyReadModel`
- `DatabaseBackupHealthReadModel`
- `DatabaseRetentionReadModel`
- `DatabaseRecoveryRunStatsReadModel`

Manifests, raw logs, native output, and unbounded artifact listings are excluded from query replies.

## 10. Messaging implementation

### 10.1 New JetStream listener contract

Add the following interface to
`TomasAI.IFM.Shared/EventModelActor/Contracts/IActorEventListener.cs`:

```csharp
/// <summary>
/// Identifies an actor event listener that uses durable JetStream delivery.
/// </summary>
public interface IJSActorEventListener : IActorEventListener
{
}
```

Inheritance is intentional. It permits consumers that need both transports in one process to request distinct DI
services while preserving the established listener lifecycle and handler contract:

```text
IActorEventListener   -> NatsActorEventListener          (NATS Core)
IJSActorEventListener -> NatsJetStreamEventListener      (JetStream)
```

Do not make `NatsJetStreamEventListener` inherit from `NatsActorEventListener`. They may share internal validation and
subject-building helpers, but their connection, stream, durable consumer, acknowledgement, and redelivery lifecycles
are different.

### 10.2 `NatsJetStreamEventListener`

Implement `NatsJetStreamEventListener` in `TomasAI.IFM.Framework.Messaging.Nats`. It must:

1. validate `eventListenerId`, `eventMap`, and handler exactly as the Core listener does;
2. resolve/create the configured JetStream stream without deleting or replacing overlapping streams;
3. create one stable explicit-ack durable consumer per listener/mailbox filter;
4. derive durable names from an allowlisted configured prefix, `eventListenerId`, and mailbox identity;
5. consume into bounded channels with `MaxAckPending` aligned to admitted capacity;
6. convert the JetStream message to `NatsMsg<byte[]>` for the inherited handler contract;
7. invoke the handler only for the configured verbs;
8. acknowledge only after the handler completes successfully;
9. negatively acknowledge or leave unacknowledged on admission/handler failure so the message is redelivered;
10. treat duplicates as normal and expose redelivery, handler-failure, pending, and message-count metrics;
11. drain deterministically on stop without acknowledging unprocessed messages; and
12. never swallow an exception and then acknowledge the affected message.

For the Database Backup Host, handler success means the execution event has been transactionally admitted to the
SQLite inbox/journal or identified as an exact duplicate. It does not mean the native backup completed.

Add `INatsJetStreamEventListenerOptions` and `NatsJetStreamEventListenerOptions` with:

- URL;
- stream name;
- durable-name prefix;
- optional filter subject;
- deliver policy;
- acknowledgement wait and maximum delivery count;
- dispatcher count and capacity;
- `MaxAckPending`, maximum batch size, and refill threshold; and
- negative-ack delay.

Validate all positive bounds and subject/name syntax during startup. The initial host uses `DeliverPolicy.All` with
explicit acknowledgement so an offline host resumes from its durable position.

### 10.3 Existing JetStream producer

Use the existing `IJSActorProducer` and `NatsJetStreamActorProducer`. Do not add another producer interface for this
feature. Extend producer options or diagnostics only if an implementation gate proves a missing requirement.

The host records every outbound service event in its SQLite outbox before calling `IJSActorProducer.SendAsync`. The
outbox marks an event published only after the server-acknowledged publish succeeds. A crash may cause duplicate publish,
so the Event Actor and Command Actor deduplicate using source event ID, operation ID, source, and service sequence.

### 10.4 Route matrix

| Sender | Message | Transport/API | Receiver | Durable acknowledgement boundary |
| --- | --- | --- | --- | --- |
| UI/Console/ScheduledTask | public command/query | NATS Core request/reply through `IActorProducer` | Command or Query Actor | actor response after validation/commit |
| Command Actor/outbox | execution-intent domain event | JetStream | Host `IJSActorEventListener` | SQLite journal admission or exact duplicate |
| Host outbox | service event | JetStream through `IJSActorProducer` | Core `IJSActorConsumer` -> Event Actor | Event Actor translation and Command Actor durable result |
| Event Actor | translated internal command | NATS actor request/reply | Command Actor | durable event-store append or idempotent already-applied result |
| Command Actor | public domain event | existing actor event publication | projectors and authorized listeners | subscriber-specific |
| UI/Console follow | public domain event | NATS Core through `IActorEventListener` | UI dispatcher or console observer | non-authoritative notification only |

NATS subjects may contain source for routing, but every receiver validates the source inside the payload. Subject
matching alone never authorizes work.

## 11. DatabaseBackup actors

### 11.1 Command Actor

Implement `DatabaseBackupCommandActor` using the existing event-source command actor base and repository conventions.
It owns:

- request authorization and policy validation;
- operation/backup-set identity allocation;
- source immutability;
- legal state transitions and optimistic expected revisions;
- per-protection-set concurrency and restore/cutover fencing decisions;
- event-store append;
- transactional/outbox-backed execution-intent publication; and
- durable command results.

Partition operation commands by `DatabaseRecoveryOperationId`. Policy, backup-set, and service-health updates use their
own stable entity IDs; do not collapse all operations into one global SystemAdmin state. The actor rejects duplicate
definitions, stale revisions, service-sequence gaps, host/source mismatch, illegal terminal transitions, and public
attempts to submit internal record commands.

State objects required initially:

- `DatabaseBackupOperationState`
- `DatabaseRestoreOperationState`
- `DatabaseBackupSetState`
- `DatabaseBackupPolicyState`
- `DatabaseBackupServiceState`
- `DatabaseRetentionState`

### 11.2 Event Actor

Implement `DatabaseBackupEventActor` with a translation registry from every supported service-event type to exactly one
internal command shape. The actor:

- validates contract version, producing host, concrete source, operation identity, safe payload bounds, and sequence;
- preserves the original service-event identity and envelope;
- sends the translated command through `IActorProducer.RequestAsync`;
- treats a durable success or exact already-applied result as success; and
- lets the JetStream delivery fail/redeliver when translation or durable command handling fails.

It does not mutate storage, invoke native capabilities, authorize cutover, or accept unknown service events.

### 11.3 Query Actor

Implement `DatabaseBackupQueryActor` over `ISystemAdminDbContext`. It performs authorization, validates query bounds and
source filters, and returns projection read models. It never calls the Database Backup Host, SQLite journal, local vault,
native tools, or destination catalog as an implicit query fallback.

### 11.4 Actor registration

Add only these DatabaseBackup registrations to SystemAdmin startup:

```text
DatabaseBackup.Command
DatabaseBackup.Event
DatabaseBackup.Query
```

Do not create `LocalDatabaseBackupActor`, `PostgreSqlBackupActor`, `ScyllaBackupActor`, or host-side actors. Engine and
source selection are service orchestration concerns behind interfaces.

## 12. `SystemAdminDbContext`

### 12.1 Context boundary

Add:

```csharp
public interface ISystemAdminDbContext
{
    // Typed projector upserts and bounded query methods only.
}

public sealed class SystemAdminDbContext : ..., ISystemAdminDbContext
{
}
```

Follow current `Application.Storage` repository, connection settings, cancellation, SQL parameterization, schema, and
logging conventions. The context uses a logical SystemAdmin schema/database within `CorePostgresCluster`; it does not
require another physical PostgreSQL server.

Actors depend on the interface through DI. SQL and provider details remain under `Application.Storage/SystemAdminDb`.
The host must not reference this project for writes.

### 12.2 Initial projection tables

| Table | Primary/unique identity | Purpose |
| --- | --- | --- |
| `database_recovery_operation_v1` | operation ID | current/history summary for all operation kinds |
| `database_recovery_phase_v1` | operation ID + phase + attempt/revision | bounded phase transitions and outcome |
| `database_recovery_run_stats_v1` | operation ID + phase + engine + logical replica + stats revision | durable structured measurements |
| `database_restore_point_v1` | restore point ID + source | eligibility, dependency chain, verification/drill state |
| `database_artifact_replica_v1` | artifact replica ID + source | logical replica lifecycle and safe destination reference |
| `database_recovery_error_v1` | operation ID + stable error identity | bounded/coalesced structured errors |
| `database_backup_policy_v1` | environment + policy ID | effective policy and enforcement state |
| `database_backup_service_health_v1` | environment + source + host | readiness and reconciliation state |
| `database_retention_state_v1` | retention plan/restore point identity | forecast, legal holds, proposed/executed plans |
| `database_backup_projection_checkpoint_v1` | projector identity | last applied event/revision and recovery metadata |

Every mutable projection row stores the source domain-event revision and last event ID. Upserts accept a greater
revision, treat the same event/revision as idempotent, and reject conflicting or regressing writes. Child tables have
foreign keys or validated logical ownership where the current storage technology permits it.

`database_recovery_run_stats_v1` stores nullable structured columns for timestamps, elapsed duration, source/stored/
transferred/restored bytes, artifact count, average and bounded peak throughput, compression ratio, retries, warnings,
verification duration/result, achieved RPO/RTO, native boundary summary, host, tool revision, policy revision, and source
event revision. An inapplicable measurement is null, not an invented zero.

### 12.3 Projectors

Add idempotent DatabaseBackup projectors to the existing event projection runtime. A projector:

1. receives an authoritative domain event only after event-store commit;
2. starts a projection mutation with stable event/projector identity;
3. updates all affected rows transactionally where possible;
4. commits the checkpoint only with the projection mutation; and
5. safely replays after failure.

Projection deletion/rebuild must never delete event-source state. Reconciliation evidence is submitted through the
Command Actor and then projected; it is never written directly into these tables.

## 13. Application service capability contracts

Put destination-neutral service ports in `TomasAI.IFM.Application.DatabaseBackup/Contracts`.

### 13.1 Processor and registry

```csharp
public interface IDatabaseRecoveryProcessor
{
    BackupSource Source { get; }
    ValueTask<DatabaseExecutionAdmission> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken);
}

public interface IDatabaseRecoveryProcessorRegistry
{
    IDatabaseRecoveryProcessor GetRequired(BackupSource source);
}
```

`GetRequired` throws a typed unsupported-source exception for `None`, unknown values, or an unregistered source. The
LocalWorkstation processor handles orchestration only after journal admission.

### 13.2 Execution journal

```csharp
public interface IDatabaseBackupExecutionJournal
{
    ValueTask<JournalAdmissionResult> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken);
    ValueTask<JournalLease?> TryAcquireLeaseAsync(DatabaseRecoveryOperationId operationId, DatabaseBackupHostId hostId,
        TimeSpan leaseDuration, CancellationToken cancellationToken);
    ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken);
    ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken);
    ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken);
    IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(int maximumCount,
        CancellationToken cancellationToken);
    ValueTask MarkServiceEventPublishedAsync(Guid eventId, DateTimeOffset publishedUtc,
        CancellationToken cancellationToken);
    IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(
        CancellationToken cancellationToken);
    ValueTask MarkCoreAcknowledgedAsync(DatabaseRecoveryOperationId operationId, long domainRevision,
        CancellationToken cancellationToken);
}
```

Additional typed methods may be added for artifact/replica checkpoints, cancellation, reconciliation, and safe
terminal compaction. No method accepts raw SQL or an arbitrary command.

### 13.3 Native capabilities

```csharp
public interface IPostgreSqlBackupCapability
{
    ValueTask<PostgreSqlBackupBoundary> CreateBaseBackupAsync(
        PostgreSqlBackupRequest request, IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
    ValueTask<PostgreSqlVerificationResult> VerifyAsync(
        PostgreSqlVerificationRequest request, CancellationToken cancellationToken);
    ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
        PostgreSqlRestoreRequest request, IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}

public interface IScyllaBackupCapability
{
    ValueTask<ScyllaBackupBoundary> CreateBackupAsync(
        ScyllaBackupRequest request, IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
    ValueTask<ScyllaVerificationResult> VerifyAsync(
        ScyllaVerificationRequest request, CancellationToken cancellationToken);
    ValueTask<ScyllaRestoreResult> RestoreToFreshTargetAsync(
        ScyllaRestoreRequest request, IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}
```

Requests contain allowlisted logical configuration resolved by the host, not actor-supplied executable names or
arguments. Implementations use the database-supported tools/APIs selected by the local architecture: PostgreSQL base
backup plus WAL continuity, and Scylla Manager/API-driven snapshot/backup where available. Exact native command lines
are framework-private and require tool-version compatibility tests.

### 13.4 Destination and evidence capabilities

Define:

- `ILocalBackupVault`
- `IOfflineBackupMediaProvider`
- `IRestoreWorkspace`
- `IDatabaseBackupManifestWriter`
- `IDatabaseBackupManifestReader`
- `IDatabaseBackupCatalog`
- `IArtifactChecksumService`
- `IManifestSignatureService`
- `ILocalBackupCapacityReader`
- `IBackupPathPolicy`
- `IDatabaseRecoveryRunStatsCollector`

All storage operations use validated logical identities. `IBackupPathPolicy` resolves them beneath configured roots,
canonicalizes the result, rejects traversal/reparse/symlink escapes, and ensures staging and publication remain on
approved filesystems. Callers cannot pass an arbitrary absolute path.

## 14. SQLite execution journal

Implement `SqliteDatabaseBackupExecutionJournal` in the LocalWorkstation framework project. The database resides at a
configured fixed path under an encrypted persistent Docker volume or bind mount. Startup fails if it resolves inside
the disposable container filesystem or inside a protected database data directory.

Initial journal tables:

| Table | Purpose |
| --- | --- |
| `journal_operation_v1` | admitted bounded intent, source, phase, lease/fence, resumability, terminal status |
| `journal_inbox_v1` | source execution event ID and content hash for exact deduplication/conflict rejection |
| `journal_checkpoint_v1` | native, staging, transfer, verification, cancellation, and cleanup checkpoints |
| `journal_artifact_replica_v1` | private artifact/replica publication progress and immutable identities |
| `journal_outbox_v1` | serialized service events, service sequence, publish state and retry metadata |
| `journal_run_stats_v1` | bounded phase/final statistics awaiting or proving publication |
| `journal_reconciliation_v1` | last Core acknowledgement and reconciliation state |

Admission is one SQLite transaction: insert inbox identity, validate the immutable operation/source definition, insert
or match the operation, allocate service sequence if needed, and enqueue accepted/rejected service evidence. The
JetStream message is acknowledged only after that transaction commits.

Use WAL mode only when the selected persistent filesystem and backup procedure safely support it. Apply migrations
before starting the listener. Enable foreign keys, busy timeout, bounded retries, integrity check, and synchronous
durability appropriate to the encrypted local volume. Never log SQL parameter values that could reveal infrastructure
details.

Leases contain a monotonically increasing fencing token. A worker must prove the current token before recording a
checkpoint or terminal result. Expired work is reconciled before reacquisition; it is not blindly restarted.

Terminal rows are compacted only after Core acceptance is known and immutable destination manifest/run evidence is
durable. Inbox identities and minimal reconciliation tombstones are retained for the configured deduplication horizon.

## 15. LocalWorkstation processor workflows

### 15.1 Backup

1. JetStream listener validates envelope and source, then transactionally admits the execution intent.
2. Processor registry selects `LocalWorkstationDatabaseRecoveryProcessor`.
3. Worker acquires a fenced journal lease.
4. Preflight validates policy revision, host capability, protection set, native tool/API version, credentials by
   reference, online vault/media identity, restore workspace, free capacity, encryption state, and concurrency.
5. Processor emits a journaled accepted event, followed by started after preflight.
6. PostgreSQL and/or Scylla capability establishes its native consistency boundary.
7. Processor checkpoints artifacts and publishes only bounded progress events.
8. Required replicas are durably staged/transferred with no-overwrite identities.
9. Checksums and native verification complete.
10. Signed manifest, final run evidence, and catalog entry are atomically published.
11. Processor journals and publishes terminal completion, failure, or safe cancellation.
12. Lease is released and later compacted only after Core acknowledgement/reconciliation.

No restore point is query-eligible before immutable manifest publication and required verification.

### 15.2 Restore and drill

1. Resolve an eligible restore point and validate the signed manifest/dependency chain.
2. Select an available replica for the command's concrete source.
3. Validate a fresh, allowlisted target and isolated restore workspace.
4. Retrieve/copy immutable artifacts without modifying the source backup.
5. Invoke the engine-specific fresh-target restore.
6. Run native and configured application validation.
7. For a drill, publish measured RPO/RTO and end in `DrillCompleted`; never cut over.
8. For a production recovery, publish `ReadyForCutover` and wait for a separate revision-bound cutover command.
9. Execute cutover only with a current validation revision, separate approval, and fencing token.

The normal actor path may be unavailable during disaster restore. The host must therefore write a bounded break-glass
recovery record and final evidence, then reconcile it through authenticated actor commands after Core is restored.

### 15.3 Cancellation

Cancellation is journaled intent. The processor stops only at an engine/destination-defined safe boundary, records
cleanup disposition, prevents incomplete artifacts from becoming eligible, and publishes cancelled only after reaching
that boundary. A native atomic step that cannot be interrupted reports its current non-cancellable phase.

### 15.4 Reconciliation

On startup or explicit request, the host:

- loads non-terminal journal entries;
- checks native task, staging, replica, and manifest state;
- resumes only explicitly resumable transfer/verification work;
- fails ambiguous native capture safely;
- republishes pending outbox events;
- reports last service sequence and terminal evidence; and
- never promotes an artifact merely because files exist.

Core accepts recovered facts only through the Event Actor -> translated command -> Command Actor -> domain event path.

## 16. Standalone Database Backup Host

Create `TomasAI.IFM.Api.DatabaseBackup.Host` as an independently runnable .NET 10 Worker/hosted-service executable. Do
not add a Dockerfile or Aspire dependency during the functional implementation gates. Its
composition root registers:

- validated host and source options;
- shared `NatsConnectionManager`;
- `IJSActorEventListener` -> `NatsJetStreamEventListener`;
- `IJSActorProducer` -> `NatsJetStreamActorProducer`;
- `IDatabaseBackupExecutionJournal` -> `SqliteDatabaseBackupExecutionJournal`;
- `IDatabaseRecoveryProcessorRegistry`;
- `IDatabaseRecoveryProcessor` -> `LocalWorkstationDatabaseRecoveryProcessor`;
- PostgreSQL and Scylla native capabilities;
- vault, offline media, workspace, path policy, manifest, catalog, checksum, signature, capacity, and stats services;
- execution dispatcher;
- service-event outbox publisher;
- startup reconciliation service; and
- readiness/liveness health checks and telemetry.

Hosted-service order:

1. validate configuration and mounted path safety;
2. migrate and integrity-check SQLite journal;
3. validate native and destination capabilities;
4. start outbound outbox publisher;
5. reconcile incomplete operations;
6. start bounded execution workers;
7. start durable inbound listener;
8. become ready.

Shutdown reverses admission first: mark not ready, stop listener/drain admission, stop scheduling, checkpoint/cancel
within the configured grace period, flush the outbox where possible, release leases, and close the journal.

The host exposes only health/metrics management endpoints. It does not expose an HTTP backup/restore command API.

## 17. Configuration and deployment

Add a dedicated `DatabaseBackup` configuration root. Core-owned policy is transported in versioned execution intent;
host bootstrap configuration contains only what the service needs to execute safely.

Minimum host sections:

```text
DatabaseBackup:Host
DatabaseBackup:Sources:LocalWorkstation
DatabaseBackup:Journal
DatabaseBackup:PostgreSql
DatabaseBackup:Scylla
DatabaseBackup:OnlineVault
DatabaseBackup:OfflineMedia
DatabaseBackup:RestoreWorkspace
DatabaseBackup:Manifest
DatabaseBackup:Limits
Nats:JetStreamEventListener
Nats:JetStreamProducer
```

Configuration contains secret references, not secret values copied into domain messages. Development defaults are
disabled or dry-run and cannot target production protection-set IDs.

After the functional paper-trading gates pass, package the unchanged Worker using the official .NET 10 Ubuntu 24.04
image. The Docker gate adds persistent journal, vault/media/workspace mounts, non-root execution, Linux native tools,
health checks, and restart tests. It does not change domain or NATS contracts.

A future full-system Linux production migration plan may add Aspire composition:

```text
Api.Server -> NATS, CorePostgresCluster, Scylla
Api.DatabaseBackup.Host -> NATS, persistent encrypted journal mount,
                           vault/media/workspace mounts, backup-native secret refs
```

Aspire may later supply discovery, references, startup ordering, shared OpenTelemetry defaults, and health visibility.
It does not run native backup logic or merge the host into `Api.Server`. The Worker and eventual container must run
independently for integration and recovery use. OpenTelemetry-compatible instrumentation and health semantics are
developed before Aspire so paper trading validates them.

## 18. Typed NATS client APIs

Add these interfaces and implementations to `TomasAI.IFM.Application.Api.Nats.Client`:

```csharp
public interface IDatabaseBackupCommandApi
{
    // One typed method per public DatabaseBackup command.
}

public interface IDatabaseBackupQueryApi
{
    // One typed method per DatabaseBackup query.
}
```

Each method is a thin cancellation-aware wrapper over `IActorProducer.RequestAsync`. It supplies the correct actor
subject/entity ID, returns `ServiceResult<T>`, and does not perform local policy decisions or invoke HTTP. The command
API returns accepted operation identity promptly; it never waits for native completion.

Do not create separate LocalWorkstation and AWS client APIs. Source is a command/query field.

## 19. Database Backup Console

Create a NATS-only console executable. Initial verbs:

```text
status
list-operations
show-operation
list-restore-points
backup
cancel
verify
restore
restore-drill
approve-restore
approve-cutover
retention-evaluate
retention-execute
reconcile
follow
```

The console uses `IDatabaseBackupCommandApi`, `IDatabaseBackupQueryApi`, and optionally `IActorEventListener` for follow
mode. It prints the accepted `OperationId` immediately. Follow mode can reconnect and query current state because NATS
Core UI/console events are notifications, not its authoritative history.

Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | requested action/query succeeded |
| 1 | operation reached terminal failure in explicit follow mode |
| 2 | invalid arguments or unsafe request |
| 3 | command rejected by domain policy/state |
| 4 | query target not found |
| 5 | service unavailable/transport failure |
| 6 | reconciliation mismatch |
| 130 | cancelled by caller |

Destructive commands require explicit flags and show source, restore point, fresh target, and approval reference before
submission. Command history must not expose secrets.

## 20. WinForms integration

All legacy WinForms changes stay under `TomasAI.IFM.UI.Net` and its existing Models, ViewModels, Views,
Presentation.UnitTests, SystemTests, and `TomasAI.IFM.UI.EventConsumer` projects. The future `TomasAI.IFM.UI` WPF project
receives documentation only until its migration is authorized.

Replace the current per-database `BackupDatabaseAsync` model path with:

- `IDatabaseBackupModel` in `TomasAI.IFM.UI.Net.Models`;
- a model implemented with `IDatabaseBackupCommandApi` and `IDatabaseBackupQueryApi`;
- immutable UI state records derived from read models;
- a `DatabaseBackupViewModel` that submits commands and refreshes through bounded queries; and
- an updated `SystemAdminUIEventConsumer` that listens only to authorized public DatabaseBackup domain events.

NATS callbacks publish through the existing UI-safe dispatch abstraction. They never mutate bound state from the NATS
thread. The event notification triggers a targeted state update/query; it is not treated as a complete durable UI
history.

Keep view changes minimal: protection-set/source selection, operation identity/status, last verified/restore-tested
point, and safe validation/error summaries. Restore/cutover UI controls remain disabled until the corresponding backend
gates and authorization workflow pass.

## 21. Security and safety requirements

Implementation reviews must reject code that violates any of these rules:

- Actor payloads contain credentials, connection strings, native arguments, raw SQL/CQL, or arbitrary paths.
- A UI/Console call can invoke a native tool without Command Actor authorization and committed execution intent.
- The host can write `SystemAdminDbContext` or event-source tables.
- `Api.Server` receives native backup credentials.
- The LocalWorkstation processor accepts `BackupSource.None` or `AwsCloud`.
- Restore modifies an existing production target before separate cutover approval.
- A relative or logical artifact identity can escape configured roots after canonicalization.
- A symlink/reparse point can redirect staging/publication outside an approved root.
- An incomplete artifact can be cataloged as an eligible restore point.
- A duplicate/redelivered message can repeat destructive native work.
- Journal or outbox state exists only in the container writable layer.
- Raw process output or secrets are included in public events, projection rows, logs, or metrics labels.

Use a least-privilege backup-native identity distinct from application data-access identities. Encrypted volume state,
recovery keys, manifest signing keys, secret rotation, and offline media enrollment follow the local architecture.

## 22. Observability and health

Add bounded metrics for:

- execution events received, admitted, deduplicated, rejected, redelivered, and dead-lettered;
- journal latency, lease conflicts, incomplete/recoverable operations, and pending outbox count/age;
- operation/phase duration, bytes, throughput, retries, verification time, achieved RPO/RTO;
- vault/media capacity and expected-media mismatch;
- latest completed, verified, and restore-tested age per protection set/source;
- service sequence gaps and reconciliation mismatches; and
- native capability/tool version readiness.

Metric labels are allowlisted: source, engine, operation kind, phase, outcome, and protection-set class. Do not label by
operation ID, path, error text, or artifact ID.

Liveness means the process and journal loop are responsive. Readiness additionally requires valid configuration,
healthy journal, NATS connectivity, supported native capability versions, safe mounted paths, and no unreconciled
startup condition that makes new work unsafe.

## 23. Test implementation

### 23.1 Shared contract tests

Add tests to `TomasAI.IFM.Shared.UnitTests` and/or `TomasAI.IFM.Domain.SystemAdmin.UnitTests` for:

- `IJSActorEventListener` inheritance and distinct DI resolution;
- MessagePack round-trip and stable keys for every new contract;
- enum numeric stability and rejection of unknown/None sources;
- envelope and bounded collection validation;
- strongly typed ID equality, parsing, and invalid-string rejection; and
- no secret/path/native argument fields in serialized contract shapes.

### 23.2 Messaging tests

Add unit and integrated tests to the existing Framework.Messaging.Nats test projects:

- Core and JetStream listener registrations can coexist in one service provider;
- stable durable consumer naming and subject filters;
- handler success acknowledges exactly once;
- handler failure is redelivered and is not acknowledged;
- bounded-channel backpressure honors `MaxAckPending`;
- duplicate delivery reaches the idempotent handler;
- stop/drain leaves unprocessed messages available;
- reconnect resumes from the durable position;
- unsupported verb is handled by the defined filtering policy; and
- listener metrics and state/message count are correct.

Use a disposable real NATS server with JetStream for integrated acknowledgement/redelivery tests.

### 23.3 Domain actor tests

Add unit, integration, BDD, and benchmark coverage to the existing SystemAdmin test projects:

- every legal and illegal backup/restore state transition;
- source immutability and host/source authorization;
- idempotent request and service-event replay;
- conflicting duplicate rejection;
- service-sequence gap and reconciliation behavior;
- event-to-command translation for every service event;
- Event Actor acknowledgement only after durable Command Actor success;
- restore approval and separate cutover approval;
- cancellation safe-boundary behavior;
- retention plan revision/fencing and legal holds;
- projection replay/query behavior; and
- actor throughput/allocation benchmarks for bounded progress and duplicate bursts.

### 23.4 Storage tests

Add `SystemAdminDb` unit/integration tests to the existing Application.Storage test projects:

- schema create/upgrade idempotency;
- projection upsert idempotency and revision regression rejection;
- transactional checkpoint behavior;
- query paging/source filters;
- run-stat nullable semantics;
- bounded/coalesced error rows;
- full projection rebuild from domain events; and
- PostgreSQL container integration with cancellation and reconnect.

### 23.5 Application and LocalWorkstation tests

Create:

- `TomasAI.IFM.Application.DatabaseBackup.UnitTests`;
- `TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.UnitTests`;
- `TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.IntegrationTests`; and
- `TomasAI.IFM.Api.DatabaseBackup.Host.IntegrationTests`.

Cover journal transactionality, inbox deduplication, content-conflict rejection, fenced leases, outbox retry, crash-point
recovery, compaction preconditions, path traversal/reparse rejection, no-overwrite publication, checksum/signature
validation, wrong media, insufficient capacity, cancellation, manifest/catalog atomicity, and reconciliation.

Native integration tests use disposable PostgreSQL and Scylla environments with synthetic data. Tests must prove both
data restoration and database-native/application validation. They never target developer or production databases.

### 23.6 Console and UI tests

Add console parser/API/exit-code tests. Extend `TomasAI.IFM.UI.Net.Presentation.UnitTests` for model/view-model state,
source selection, non-blocking command submission, UI-thread dispatch, duplicate/out-of-order notification handling,
query refresh, and safe error display. Extend `TomasAI.IFM.UI.Net.SystemTests` with FlaUI startup and backup dashboard
smoke tests after the backend disposable environment is reliable.

### 23.7 End-to-end qualification

The final local gate composes disposable NATS JetStream, Core PostgreSQL, Scylla, Database Backup Host, encrypted-test
equivalent journal/vault mounts, and Core actors. It proves:

1. UI/Console command returns an operation ID;
2. committed intent reaches the host after restart/redelivery;
3. PostgreSQL and Scylla artifacts and signed manifest are published;
4. service events reach the Event Actor and authoritative domain state;
5. projections and queries converge;
6. a fresh-target restore reproduces synthetic data;
7. a restore drill records measured RPO/RTO;
8. duplicate events do not repeat native work;
9. host/Core restarts reconcile correctly; and
10. legacy contracts are no longer referenced.

Destructive qualification tests are separately tagged and opt-in.

## 24. Implementation phases and gates

### Phase 0: Baseline and naming cleanup

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-0-Validation-Report.md`.

- Rename the existing projection-migration executable.
- Record baseline solution build and relevant test counts.
- Add new empty projects/folders and solution references.
- Freeze common naming and contract version 1.

**Gate 0:** renamed utility retains behavior; solution builds; no production behavior changes.

### Phase 1: JetStream event listener

- Add `IJSActorEventListener`.
- Implement options, listener, DI registration, metrics, unit and real-NATS integrated tests.

**Gate 1:** durable acknowledge/redelivery/reconnect tests pass and Core/JS listeners resolve independently.

### Phase 2: Shared contracts and typed client API

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-2-Validation-Report.md`.

- Add IDs, enums, envelopes, commands, execution events, service events, domain events, queries, read models.
- Add MessagePack/validation tests and NATS client wrappers.

**Gate 2:** serialization compatibility, contract validation, and API wrapper tests pass with no native dependencies.

### Phase 3: Domain actors

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-3-Validation-Report.md`.

- Implement Command state machines/repositories, Event translations, Query handlers, registration, and event outbox.
- Add unit, integration, BDD, and initial benchmark tests.

**Gate 3:** every contract route and legal/illegal transition is tested; duplicate and sequence-gap tests pass.

### Phase 4: `SystemAdminDbContext` and projectors

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-4-Validation-Report.md`.

- Add schema, repositories, read models, projectors, checkpointing, replay, and reconciliation query support.

**Gate 4:** PostgreSQL integration and full projection rebuild tests pass; actors cannot query the service journal.

### Phase 5: Host and SQLite journal skeleton

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-5-Validation-Report.md`.

- Add application ports, LocalWorkstation processor registry, SQLite journal/inbox/outbox/leases, host lifecycle,
  health, and NATS plumbing using fake native capabilities.

**Gate 5:** a fake operation runs end to end across JetStream, actor state, host restart, outbox replay, and projections.

### Phase 6: PostgreSQL LocalWorkstation capability

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-6-Validation-Report.md`.

- Implement base backup, WAL continuity evidence, verification, fresh-target restore, stats, and disposable integration
  tests.

**Gate 6:** synthetic PostgreSQL data restores to a fresh target and passes native/application validation after a host
restart scenario.

### Phase 7: Scylla LocalWorkstation capability

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-7-Validation-Report.md`.

- Implement manager/API-driven capture, schema/metadata evidence, verification, fresh-target restore, stats, and
  disposable integration tests.

**Gate 7:** synthetic Scylla data restores to a fresh target and passes native/application validation after a host
restart scenario.

### Phase 8: Vault, offline media, manifest, catalog, retention, and drills

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-8-Validation-Report.md`.

- Implement safe publication, signed shared manifest, media enrollment/rotation, retention plan/execute split,
  restore-source selection, drill evidence, and break-glass record.

**Gate 8:** no-overwrite, wrong-media, tamper, capacity, retention dependency, restore drill, and break-glass
reconciliation tests pass.

### Phase 9: Console and WinForms migration

**Implementation status:** Complete on 2026-08-12. See
`Local-Workstation-Backup-Restore-Phase-9-Validation-Report.md`.

- Implement console and NATS-only WinForms model/view-model/event-consumer migration with minimal view changes.

**Gate 9:** Console and UI use the new commands/queries; UI remains responsive; FlaUI smoke workflow passes against the
disposable backend.

### Phase 10: Ubuntu 24.04 Docker qualification, runtime validation, and legacy removal

**Implementation status:** Complete except host encryption evidence. The standalone Ubuntu 24.04/.NET 10 packaging,
health, non-root runtime, persistent-journal restart, pinned PostgreSQL 16.14 and Scylla 6.2.2 native qualifications,
fresh-target restores, relevant regression suites, scheduled-task migration, and legacy removal passed by 2026-08-13.
See `Local-Workstation-Backup-Restore-Phase-10-Container-Packaging-Validation-Report.md` and
`Local-Workstation-Backup-Restore-Phase-10-Validation-Report.md`. Development backup storage is not required to be
encrypted, although it must not contain production data or production secrets. Production backup storage remains
subject to the encrypted-at-rest requirements in the production architecture and deployment gates.

- Package the existing Worker with the official Ubuntu 24.04/.NET 10 image; do not add Aspire in this phase.
- Run standalone Docker end-to-end backup, crash recovery, restore drill, and fresh-target restore.
- Remove deprecated SystemAdmin backup messages, APIs, event listeners, model methods, and per-database assumptions.
- Verify no legacy type references remain with `rg` and compile all solution projects.

**Gate 10:** all relevant unit/integration/BDD/system tests pass; both engines restore successfully; documentation and
configuration samples match runtime behavior; the legacy backup API is absent from the codebase.

Do not start a phase whose preceding gate is red. A phase may be split into small commits, but each commit must compile
and preserve unrelated user changes.

## 25. Legacy removal inventory

The final migration must find and replace, then remove, at least:

- `TomasAI.IFM.Domain.SystemAdmin/Command/BackupDatabase.cs`;
- legacy backup handling in `SystemAdminCommandActor`, `SystemAdminEventActor`, and legacy SystemAdmin state;
- deprecated backup command/event contracts in `TomasAI.IFM.Domain.SystemAdmin.Shared`;
- old methods in `Application.Api.Nats.Client/SystemAdminCommandApi` and related query APIs;
- `TomasAI.IFM.UI.Net.Models/SystemAdminModel.BackupDatabaseAsync`;
- per-database backup behavior in `BackupDatabasesViewModel` and `BackupDatabasesView`;
- legacy event mappings in `TomasAI.IFM.UI.EventConsumer/SystemAdminUIEventConsumer`; and
- configuration/routes used only by the old database-name backup workflow.

Removal occurs only after Gate 9. Do not delete unrelated SystemAdmin commands, HTTP endpoints used for other purposes,
or the `Api.Server` process. `Api.Server` remains the process hosting the NATS actor server as well as HTTP capabilities
for other clients.

## 26. Definition of done

LocalWorkstation implementation is complete only when all statements are true:

- [x] Common contracts contain `BackupSource` and no source-specific message types.
- [x] DatabaseBackup has exactly Command, Event, and Query actor roles.
- [x] UI and Console submit commands/queries through NATS actor APIs only.
- [x] The host consumes execution intent through `IJSActorEventListener` with durable explicit acknowledgement.
- [x] The host publishes service events through the existing `IJSActorProducer` and journaled outbox.
- [x] Event Actor acknowledgement follows durable Command Actor acceptance.
- [x] `SystemAdminDbContext` projections and `DatabaseRecoveryRunStats` rebuild from domain events.
- [x] The host cannot write application projection or event-source databases.
- [ ] SQLite journal survives container restart on an encrypted persistent mount.
- [x] PostgreSQL backup, verification, WAL/dependency evidence, and fresh-target restore pass.
- [x] Scylla backup, verification, schema/dependency evidence, and fresh-target restore pass.
- [x] Signed manifests and catalog entries publish atomically without overwrite.
- [x] Restore drill, RPO/RTO evidence, cancellation, retention fencing, and reconciliation pass.
- [x] Duplicate/redelivered messages never repeat destructive native work.
- [x] Public events and storage contain no secrets, arbitrary paths, or raw native output.
- [x] Standalone Worker and Ubuntu 24.04 Docker smoke tests pass; Aspire remains outside this implementation.
- [x] Legacy backup contracts and call sites are removed only after the replacement is validated.
- [x] AWS can later implement the same application capability contracts without changing public actor schemas.

## 27. Required implementation discipline

For each phase, the implementing agent must:

1. inspect the actual current interfaces and project references before editing;
2. preserve unrelated dirty-worktree changes;
3. introduce contracts additively before migrating consumers;
4. add XML documentation to every public type/member;
5. use cancellation tokens on I/O and long-running operations;
6. use `apply_patch` for source edits and normal non-interactive build/test commands;
7. run the smallest relevant tests first, then the bounded phase gate;
8. record exact build/test commands and results in the implementation report;
9. avoid real database or retention mutations outside explicitly provisioned disposable test targets; and
10. stop at the gate and report any architecture conflict instead of silently changing this design.

## 28. Architecture traceability

| Architecture requirement | Implementation component |
| --- | --- |
| Same local/AWS actor API | shared Domain.SystemAdmin DatabaseBackup contracts plus `BackupSource` |
| Three domain actors | `DatabaseBackupCommandActor`, `DatabaseBackupEventActor`, `DatabaseBackupQueryActor` |
| Durable service ingress | `IJSActorEventListener` and `NatsJetStreamEventListener` |
| Durable service egress | existing `IJSActorProducer` plus SQLite outbox |
| Authoritative state | event-source Command Actor aggregates |
| Query and run history | `ISystemAdminDbContext` projections |
| Host restart recovery | SQLite execution journal, fenced leases, reconciliation |
| Database-native execution | PostgreSQL and Scylla high-level capabilities |
| Local durability | online vault, offline media provider, signed manifest/catalog |
| UI/Console parity | typed NATS command/query APIs and common read models |
| Self-reference recovery gap | immutable destination run evidence plus controlled reconciliation |
| Future AWS compatibility | destination-neutral application ports and source-neutral serialized contracts |

## 29. Revision history

| Version | Date | Change |
| --- | --- | --- |
| 0.1 | 2026-08-11 | Initial code implementation specification for LocalWorkstation, including `IJSActorEventListener`, actor contracts, SystemAdmin projections, SQLite journal, native capabilities, Docker/Aspire host, Console, WinForms migration, tests, and gated delivery phases. |
| 0.2 | 2026-08-12 | Changed the paper-trading sequence to standalone .NET 10 Worker development, deferred Ubuntu 24.04 Docker packaging to Gate 10, deferred Aspire to a future full-system Linux production migration, and excluded other capability-host designs. |
| 0.3 | 2026-08-12 | Implemented and validated Gate 1: distinct Core/JetStream listener contracts and DI registrations, durable explicit-ack JetStream event listening, bounded admission, failure redelivery, restart recovery, metrics, and real-NATS tests. |
| 0.4 | 2026-08-12 | Implemented and validated Gate 2: versioned DatabaseBackup IDs, enums, envelopes, commands, execution/service/domain events, queries, read models, validation, MessagePack compatibility coverage, and cancellation-aware typed NATS client APIs. |
| 0.5 | 2026-08-12 | Implemented and validated Gate 3: DatabaseBackup Command/Event/Query actors, event-sourced aggregate states and repository, service-event translation, execution-intent outbox tracking, dependency registration, and unit/integration/BDD/benchmark coverage. |
| 0.6 | 2026-08-12 | Implemented and validated Gate 4: Core PostgreSQL SystemAdmin schema, bounded query repository, transactional idempotent projections, checkpoints, authoritative-event replay, full projection rebuild, runtime projector wiring, and live PostgreSQL integration coverage without service-journal access. |
| 0.7 | 2026-08-12 | Implemented and validated Gate 5: destination-neutral recovery ports, fenced SQLite journal/inbox/outbox/leases, standalone host lifecycle and health, durable JetStream ingress/egress, fake native capabilities, restart recovery, and a real JetStream-to-PostgreSQL end-to-end test. |
| 0.8 | 2026-08-12 | Implemented and validated Gate 6: allowlisted PostgreSQL native execution, physical base backup with streamed WAL evidence, native manifest verification, restart-safe capture recovery, fenced-lease renewal, bounded run statistics, isolated fresh-target boot and validation, and disposable PostgreSQL 17 restore coverage. |
| 0.9 | 2026-08-12 | Implemented and validated Gate 7: allowlisted Scylla Manager capture/restore, topology/schema/native-manifest evidence, restart-safe verification, fresh-target validation, bounded run statistics, and disposable native SSTable restore coverage. |
| 1.0 | 2026-08-12 | Implemented and validated Gate 8: no-overwrite online/offline publication, ECDSA-signed manifests/commits/catalog/enrollment/evidence, dependency-complete source selection, capacity admission, exact revision-bound retention, immutable drill evidence, and offline break-glass reconciliation. |
| 1.1 | 2026-08-12 | Implemented and validated Gate 9: NATS-only operator console parsing/API/exit codes, immutable WinForms backup dashboard state, protection-set/source selection, non-blocking command acceptance, bounded query refresh, public-event refresh signaling, UI-thread ownership, and FlaUI startup/responsiveness smoke coverage. |
| 1.2 | 2026-08-12 | Started Phase 10 and validated its container-packaging slice: official Ubuntu 24.04/.NET 10 images, standalone NATS composition, non-root/read-only runtime, health probes, persistent backup/restore mounts, PostgreSQL 16 native tools, and SQLite journal reuse across Worker restart. Gate 10 remains open for native end-to-end qualification, encrypted-mount evidence, and legacy removal. |
| 1.3 | 2026-08-13 | Completed Phase 10 code and runtime qualification with pinned PostgreSQL 16.14 and Scylla 6.2.2 native fresh-target restores, PostgreSQL 16 manifest compatibility, scheduled-task migration, complete legacy SystemAdmin backup removal, regression coverage, and repeat Ubuntu container/restart validation. Gate 10 remains blocked only on administrator-only BitLocker evidence for the Docker backing volume. |
| 1.4 | 2026-08-13 | Confirmed Docker Desktop's `CustomWslDistroDir` as `D:\Docker\wsl\data` and corrected the encryption evidence target. The elevated `C:` result is non-applicable, and `manage-bde -status D:` reports that `D:` is not a valid BitLocker volume. Gate 10 awaits alternative encryption evidence or encrypted-storage remediation. |
| 1.5 | 2026-08-13 | Clarified the environment boundary: workstation development storage may be unencrypted, while production backup storage still requires encryption at rest. Added independent PostgreSQL/Scylla native-source selection and the `E:\IFM\DatabaseBackup` development composition/runbook. |
