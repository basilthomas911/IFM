# ScyllaDB Command Deduplication Guard Design

**Status:** Approved design; G2/G3 prerequisite satisfied; implementation remains a separate post-UI task

**Decision date:** 2026-08-20

**Primary objective:** Reject a repeated `CommandId` before command validation, state loading, domain execution, or persistence can run a second time

**Secondary objective:** Retain the received command as a MessagePack-CSharp binary payload so terminal status and failure diagnostics can be added without another storage migration

## 1. Decision

IFM will separate command admission/deduplication from the PostgreSQL event-source database. A dedicated ScyllaDB command guard will become the authoritative answer to this question:

> Has this exact `CommandId` already been admitted for processing?

The guard is not initially a general-purpose command history, reporting, or audit subsystem. Its first responsibility is to make duplicate command delivery cheap to detect and to stop duplicate processing at the earliest safe point.

The stored command payload will use the existing MessagePack-CSharp contract and options already used by the actor/NATS transport. Newtonsoft JSON will not be the durable command representation in the new store.

UI gates G2 and G3 were accepted on 2026-08-20. Implementation remains outside this UI change set and starts only as a separately authorized post-UI task, so the storage migration is not mixed into restoration acceptance.

## 2. Current behavior and reason for change

The current `command_log` table is in PostgreSQL beside the event-source tables. Each command actor:

1. deserializes the incoming MessagePack command;
2. serializes the command again as Newtonsoft JSON;
3. starts a PostgreSQL `command_log` insert during parsing; and
4. awaits that insert before domain validation continues.

The current table is keyed by `CommandId`, but the runtime path does not use the insert as a universal atomic admission decision. Duplicate protection therefore depends on downstream behavior rather than a single guard that stops work before state loading and command execution.

The target removes JSON reserialization, gives duplicate admission one explicit contract, and uses ScyllaDB's partition-key lookup path for the guard record.

## 3. Required semantics

### 3.1 First delivery

The first valid delivery of a `CommandId` performs an atomic conditional insert. When ScyllaDB reports `applied = true`, the actor may continue to validation, replay/state loading, execution, and persistence.

### 3.2 Duplicate delivery

When the conditional insert reports `applied = false`, the actor must stop before:

- validation with side effects;
- aggregate state loading;
- command handler execution;
- domain storage mutation;
- event creation or publication; and
- any external provider or broker call.

The caller receives a typed duplicate-command outcome containing the original `CommandId`. A duplicate must not be presented as a newly executed command.

### 3.3 Concurrency

Two concurrent deliveries of the same `CommandId` must not both pass admission. This requires ScyllaDB lightweight-transaction semantics:

```sql
INSERT INTO command_guard_by_id (...)
VALUES (...)
IF NOT EXISTS;
```

A normal ScyllaDB insert is an upsert and cannot distinguish the first delivery from a duplicate. The first implementation must not replace the conditional insert with a read-then-write sequence because two coordinators could both observe absence and execute the command.

### 3.4 Crash window

An admission record can be committed immediately before the process crashes. A later delivery would then be a duplicate even though domain processing may never have completed. A permanent guard without recovery would convert at-least-once delivery into command loss.

For that reason, the minimum record includes admission state and a renewable processing lease even though rich terminal failure reporting is deferred. The first implementation requires these states:

- `Admitted`: the conditional insert succeeded;
- `Processing`: one actor instance owns a bounded processing lease; and
- `Completed`: domain persistence reached its declared success boundary.

Detailed failure status, stack diagnostics, retry policy, and operator reporting may be added later. The lease/recovery distinction is not optional because it is required to make the deduplication guard safe across crashes.

A duplicate observing an unexpired `Admitted` or `Processing` record returns `DuplicateInProgress`. A delivery observing an expired lease may acquire recovery ownership with a conditional update. A duplicate observing `Completed` returns `DuplicateCompleted` without executing domain behavior.

## 4. ScyllaDB table

The initial table is query-specific and supports exact lookup by command ID:

```sql
CREATE TABLE IF NOT EXISTS command_guard_by_id (
    command_id uuid PRIMARY KEY,
    stream_id text,
    actor_name text,
    command_name text,
    contract_id text,
    contract_version smallint,
    payload_codec text,
    command_payload blob,
    admission_state tinyint,
    admitted_at timestamp,
    processing_owner text,
    lease_expires_at timestamp,
    completed_at timestamp
);
```

Design rules:

- `command_id` is the partition key and the only required first-phase lookup.
- No secondary index is required.
- No `ALLOW FILTERING` query is permitted.
- No TTL is applied by default. Expiring a guard would permit an old command to execute again. Any future retention period must be at least as long as the corresponding event and business-state replay horizon and requires an explicit archival policy.
- `LocalQuorum` is used for ordinary reads/writes and `LocalSerial` for conditional admission and lease transfer, matching the repository's current ScyllaDB consistency policy.
- The guard row remains small. Chronological reporting, if later required, uses a separate bucketed projection rather than changing this partition.

## 5. MessagePack payload

### 5.1 Codec

Use MessagePack-CSharp, not Nerdbank.MessagePack. The repository's current Nerdbank decision remains deferred. The stored codec identifier begins as:

```text
messagepack-csharp-lz4-block-array-v1
```

If measurements show LZ4 costs more than it saves for small commands, a second explicit codec may store uncompressed MessagePack. Codec changes are versioned; existing rows are never reinterpreted under new options.

### 5.2 Preserve the received bytes

The preferred payload is an exact copy of the MessagePack bytes received from NATS. The actor currently owns pooled message memory and releases it after parsing, so the command-admission boundary must make one explicit owned copy before release or transfer ownership to the guard write. It must never retain a reference to returned pooled memory.

Preserving the received bytes is preferred to serializing the materialized command again because it:

- records exactly what was admitted;
- removes Newtonsoft JSON allocation and formatting;
- avoids a second serialization interpretation; and
- retains the existing tested wire contract.

### 5.3 Type resolution and compatibility

Each row stores a stable `contract_id` and numeric `contract_version`. Rehydration uses a closed registry from stable contract ID to concrete command type. Typeless deserialization and assembly-qualified CLR names are prohibited.

MessagePack numeric keys are append-only:

- existing keys are never reordered;
- removed keys are never reused;
- new optional members receive new keys; and
- every persisted command type receives an old-payload/new-code compatibility test.

## 6. Runtime boundary

Extract command admission from `IEventSourceActorDbContext`:

```csharp
public interface ICommandDeduplicationGuard
{
    ValueTask<CommandAdmissionResult> TryAdmitAsync(
        CommandAdmission admission,
        CancellationToken cancellationToken);

    ValueTask<CommandLeaseResult> TryAcquireExpiredLeaseAsync(
        Guid commandId,
        string owner,
        DateTimeOffset leaseExpiresAt,
        CancellationToken cancellationToken);

    ValueTask MarkCompletedAsync(
        Guid commandId,
        string owner,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
}
```

The command actor pipeline becomes:

1. Parse and validate only the transport envelope needed to resolve the concrete command and `CommandId`.
2. Copy or transfer the exact MessagePack payload.
3. Call `TryAdmitAsync`.
4. If duplicate, reply with the typed duplicate outcome and release the message without loading state.
5. If admitted or recovery ownership was acquired, continue through ordinary domain validation and processing.
6. Mark the guard completed only after the command's declared durable success boundary.
7. Publish/reply using the existing correlated command ID.

Command-specific domain validation still occurs after admission. Invalid commands may later gain a terminal `Rejected` state, but they must not execute repeatedly while that reporting work is pending.

## 7. Performance position

ScyllaDB is selected because the target workload is a high-concurrency, exact-partition-key admission lookup/write and because IFM already operates a ScyllaDB storage path. The expected result is better scaling and lower contention than the shared PostgreSQL event-source database as command volume grows.

The atomic guard uses a lightweight transaction, however, so its latency cannot be inferred from ordinary ScyllaDB upsert or bulk-write benchmarks. Acceptance requires a direct comparison using identical durability and concurrency:

- current JSON plus PostgreSQL unique insert;
- MessagePack plus PostgreSQL binary insert;
- MessagePack plus ScyllaDB `IF NOT EXISTS`;
- first delivery and duplicate delivery;
- 1, 8, 32, and 128 concurrent command streams; and
- P50, P95, P99, throughput, allocation, and payload size.

The migration proceeds with ScyllaDB as the target, while the benchmark establishes capacity, timeout, and alert thresholds rather than reopening the architectural decision.

## 8. Failure handling

- A ScyllaDB timeout after a conditional insert is an unknown admission result. The actor must read the exact partition at `LocalQuorum` before deciding whether it may execute.
- A guard outage fails command admission closed. The actor must not process a command whose uniqueness cannot be established.
- A failed completion update does not rerun domain behavior. Recovery compares the guard with the event/business persistence boundary before changing state.
- Duplicate outcomes are observable and counted separately from validation failures and infrastructure failures.
- Payload decoding failure never deletes or overwrites the guard row.

## 9. Migration sequence after G2 and G3

1. Add `ICommandDeduplicationGuard` and a provider-independent contract test suite.
2. Add the ScyllaDB schema, prepared statements, conditional result mapping, timeout reconciliation, and lease operations.
3. Add explicit transport-payload ownership/copy support.
4. Add the stable command contract registry and MessagePack compatibility tests.
5. Run a non-authoritative shadow period that records ScyllaDB admissions while PostgreSQL remains the operational path; compare command IDs and payload hashes.
6. Run the command-specific performance and failure-injection gates.
7. Switch admission authority to ScyllaDB.
8. Retain PostgreSQL command-log reads temporarily for migration diagnostics, then remove the old write path and table in a separately reviewed schema migration.
9. Add terminal failure/rejection status and operator diagnostics as the next tranche.

Synchronous dual-writing to both databases is not the final architecture because it makes command latency and availability depend on both stores.

## 10. Acceptance criteria

- Exactly one of two concurrent deliveries with the same `CommandId` receives first-admission ownership.
- A duplicate is rejected before aggregate replay, domain execution, event persistence, provider calls, or broker calls.
- A duplicate completed command returns a deterministic typed result carrying the original `CommandId`.
- A crash after admission but before completion can be recovered through an expired conditional lease without allowing two active owners.
- An ambiguous conditional-write timeout is reconciled before processing.
- The persisted blob round-trips through the stable concrete command registry.
- Existing MessagePack payloads remain readable after compatible contract evolution.
- ScyllaDB unavailability prevents unguarded command execution and produces coded diagnostics.
- First-delivery and duplicate P95/P99 measurements are recorded under representative concurrency.
- PostgreSQL `command_log` retirement occurs only after shadow reconciliation reports no unexplained missing or mismatched command IDs.

## 11. Explicit exclusions

This design does not move the PostgreSQL event log, projector checkpoints, or aggregate concurrency controls. It does not claim globally exactly-once side effects across external systems. External provider and broker operations still require their own idempotency keys using the same `CommandId` or a stable derivative.
