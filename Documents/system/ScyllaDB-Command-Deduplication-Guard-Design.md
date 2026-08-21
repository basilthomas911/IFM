# ScyllaDB Command Deduplication Guard Design

**Status:** PostgreSQL guard plus bounded L1 accelerator implemented; ScyllaDB remains benchmark-only

**Decision date:** 2026-08-20

**Primary objective:** Reject a repeated `CommandId` before command validation, state loading, domain execution, or persistence can run a second time

**Secondary objective:** Retain the received command as a MessagePack-CSharp binary payload so terminal status and failure diagnostics can be added without another storage migration

## 1. Decision

IFM will keep the PostgreSQL `command_log` as the only authoritative runtime command guard. A dedicated ScyllaDB candidate has been implemented only for integration testing and BenchmarkDotNet comparison of this question:

> Has this exact `CommandId` already been admitted for processing?

The candidate is not registered in runtime dependency injection, is not dual-written, and does not participate in command processing. Its only responsibility in this tranche is to test whether ScyllaDB can make duplicate admission materially cheaper without risking the current path.

The ScyllaDB comparison table stores MessagePack-CSharp bytes in a blob. The existing PostgreSQL table and its Newtonsoft JSON/text payload remain unchanged, providing an apples-to-current-production comparison rather than an invented PostgreSQL replacement.

UI gates G2, G3, and G4 were accepted on 2026-08-20. The benchmark candidate was implemented only after G4 acceptance, as separately authorized.

The measured ScyllaDB LWT path was slower in every completed scenario and timed out when 64 same-command duplicates contended on the single test node. No authority switch is justified. Any future proposal must start from new topology/capacity evidence and a separately authorized migration plan.

Runtime admission now uses a bounded process-local completed-ID cache and same-ID in-flight coalescing ahead of the PostgreSQL insert. The cache accelerates repeated local deliveries but never replaces PostgreSQL authority. Its default capacity is 100,000 IDs and can be changed with `IFM_COMMAND_DUPLICATE_CACHE_CAPACITY`. Eviction removes only the memory entry; a later duplicate falls through to PostgreSQL and is rejected by the durable primary key.

## 2. Current behavior and reason for change

The `command_log` table remains in PostgreSQL beside the event-source tables. The shared command-actor pipeline now:

1. deserializes the incoming MessagePack command;
2. checks the bounded completed-ID cache;
3. joins an existing process-local reservation when the same ID is already in flight;
4. performs `INSERT ... ON CONFLICT DO NOTHING` on a cache miss;
5. acknowledges a duplicate with its original `CommandId` without validation, state loading, execution, or persistence; and
6. allows only the PostgreSQL insert winner to continue through ordinary actor processing.

Legacy command actors begin their durable audit while parsing and later reach the shared ingress guard. Those two calls must represent one admission attempt, not two independent attempts. The PostgreSQL context therefore keeps the parse-time reservation in a short-lived handoff table keyed by `CommandId`; the shared guard consumes that exact reservation result once. Only a later delivery creates a new attempt and receives the duplicate result from the L1 cache or PostgreSQL.

This ordering is a correctness invariant. If parsing inserts the row and the shared guard independently checks the same ID, the first delivery is falsely classified as a duplicate: the row remains `InProgress`, no domain handler runs, and no terminal event is emitted. The integration suite exercises the complete legacy-audit-to-central-guard handoff and verifies that its first result is accepted while a repeated command is rejected.

The PostgreSQL row retains the current JSON payload. MessagePack/blob storage remains confined to the ScyllaDB benchmark candidate.

## 3. Required semantics for any future authoritative implementation

### 3.1 First delivery

The first valid delivery of a `CommandId` performs an atomic PostgreSQL insert with `ON CONFLICT DO NOTHING`. Only the caller receiving `inserted = true` may continue to validation, replay/state loading, execution, and persistence.

### 3.2 Duplicate delivery

When the conditional insert reports `applied = false`, the actor must stop before:

- validation with side effects;
- aggregate state loading;
- command handler execution;
- domain storage mutation;
- event creation or publication; and
- any external provider or broker call.

The caller receives an idempotent successful result containing the original `CommandId`; the duplicate is not executed again. The `ifm.actor.commands.duplicates` metric distinguishes this path from newly executed commands.

### 3.3 Concurrency

Two concurrent deliveries of the same `CommandId` must not both pass admission. PostgreSQL enforces this across processes with its primary key and `ON CONFLICT DO NOTHING`. Within one process, callers share one in-flight reservation and only its owner can receive an accepted result.

The benchmark-only ScyllaDB equivalent still requires lightweight-transaction semantics:

```sql
INSERT INTO command_guard_by_id (...)
VALUES (...)
IF NOT EXISTS;
```

A normal ScyllaDB insert is an upsert and cannot distinguish the first delivery from a duplicate. The first implementation must not replace the conditional insert with a read-then-write sequence because two coordinators could both observe absence and execute the command.

### 3.4 Crash window

An admission record can be committed immediately before the process crashes. A later delivery would then be a duplicate even though domain processing may never have completed. This is a known first-phase limitation of the requested guard-first implementation.

Status, a renewable processing lease, and recovery ownership remain the required follow-up before automatic recovery is enabled:

- `Admitted`: the conditional insert succeeded;
- `Processing`: one actor instance owns a bounded processing lease; and
- `Completed`: domain persistence reached its declared success boundary.

Detailed failure status, stack diagnostics, retry policy, and operator reporting are not implemented in this tranche. Until they are, an admitted row fails closed and requires operator reconciliation if processing does not reach its durable boundary.

A duplicate observing an unexpired `Admitted` or `Processing` record returns `DuplicateInProgress`. A delivery observing an expired lease may acquire recovery ownership with a conditional update. A duplicate observing `Completed` returns `DuplicateCompleted` without executing domain behavior.

## 4. Future production table design (not deployed by the benchmark)

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

## 6. Implemented runtime boundary

The shared actor runtime resolves this contract at startup and calls it immediately after command materialization:

```csharp
public interface ICommandDuplicateGuard
{
    ValueTask<bool> TryAcceptAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}
```

The command actor pipeline becomes:

1. Materialize the command and release its pooled transport payload.
2. Call `TryAcceptAsync`.
3. If duplicate, reply successfully with the original ID without validation or loading state.
4. If admitted, continue through ordinary validation and processing.
5. Publish/reply using the existing correlated command ID.

Command-specific domain validation still occurs after admission. Invalid commands may later gain a terminal `Rejected` state, but they must not execute repeatedly while that reporting work is pending.

## 7. Performance result

The implemented comparison uses the current PostgreSQL `ON CONFLICT DO NOTHING` guard with JSON/text and an isolated ScyllaDB `IF NOT EXISTS` guard with a MessagePack/blob. Serialization occurs once during setup, outside the timed database operation. Both providers use their configured production-strength consistency behavior; ScyllaDB uses `LOCAL_QUORUM` and `LOCAL_SERIAL`.

The completed 2026-08-20 BenchmarkDotNet run produced these mean workload latencies:

| Workload | Concurrency | PostgreSQL | ScyllaDB | Scylla/PostgreSQL |
|---|---:|---:|---:|---:|
| Duplicate shortcut | 1 | 1.414 ms | 18.000 ms | 12.76x |
| Duplicate shortcut | 16 | 3.100 ms | 326.724 ms | 105.52x |
| Duplicate shortcut | 32 | 4.402 ms | 671.593 ms | 152.69x |
| First insert | 1 | 5.452 ms | 17.880 ms | 3.29x |
| First insert | 16 | 10.272 ms | 249.815 ms | 24.33x |
| First insert | 32 | 14.154 ms | 327.712 ms | 23.93x |

The initial 64-way same-ID Scylla workload also produced a server-side `LOCAL_SERIAL` write timeout. The final supported report therefore uses the 1/16/32 levels that pass the live correctness suite. Managed allocations were also 38-77% higher on the measured Scylla path depending on scenario.

These results refute the assumption that ScyllaDB inserts are automatically faster for this guard. The operation is a lightweight transaction, not an ordinary upsert, and same-key duplicate bursts serialize consensus work. PostgreSQL remains the preferred implementation for the current topology and workload.

The completed-ID L1 benchmark measured the following total shortcut time per invocation:

| Local duplicates in invocation | Mean |
|---:|---:|
| 1 | 135.4 ns |
| 16 | 608.7 ns |
| 32 | 1.031 us |

Creating one new local reservation plus the remaining same-ID local duplicates cost 30.965 us at one request, 31.629 us at 16 requests, and 32.863 us at 32 requests, excluding database latency. The forced-concurrency unit test separately verifies that 32 callers share exactly one durable callback. These process-local results are directional BenchmarkDotNet measurements on the development host; cache misses continue to have the PostgreSQL latencies shown above.

## 8. Failure handling

- A PostgreSQL reservation failure fails command admission closed. The L1 coordinator records an ID only after PostgreSQL definitively returns inserted or already present; exceptions and cancellation remove the in-flight operation so the ID is not poisoned in memory.
- Cancellation by a local follower does not cancel or remove the process-local reservation owner.
- Cache eviction never deletes a PostgreSQL row and therefore changes latency, not correctness.
- A ScyllaDB timeout after a conditional insert is an unknown admission result. The actor must read the exact partition at `LocalQuorum` before deciding whether it may execute.
- A guard outage fails command admission closed. The actor must not process a command whose uniqueness cannot be established.
- A failed completion update does not rerun domain behavior. Recovery compares the guard with the event/business persistence boundary before changing state.
- Duplicate outcomes are observable and counted separately from validation failures and infrastructure failures.
- Payload decoding failure never deletes or overwrites the guard row.

## 9. Implemented comparison and runtime acceleration

1. Added a deliberately narrow `ICommandLogBenchmarkStore`; no ScyllaDB runtime service registration was added.
2. Added isolated ScyllaDB schema, prepared LWT insert, exact lookup/delete, and MessagePack blob mapping.
3. Added a PostgreSQL adapter over the existing table and exact `ON CONFLICT` SQL.
4. Added unit contract/codec tests and live first/duplicate/32-way contention integration tests.
5. Added and ran the side-by-side BenchmarkDotNet suite.
6. Retained PostgreSQL as the sole operational path based on the measured result.
7. Added the bounded L1/in-flight accelerator to the production PostgreSQL context and registered its shared ingress contract in both actor hosts.
8. Added actor short-circuit, cache, eviction, cancellation, failure recovery, and independent-process PostgreSQL tests.
9. Added and ran the L1 BenchmarkDotNet suite.
10. Added a compatibility handoff for actors that start command auditing during parsing, plus a real-PostgreSQL regression test proving the first reservation is consumed once and a later delivery is rejected.

No shadow writer or ScyllaDB migration is authorized. If materially different multi-node hardware, latency, or workload distribution warrants another experiment, rerun the isolated benchmark first. Lease recovery, terminal status, and automatic reconciliation of admitted-but-unfinished commands remain future work.

Synchronous dual-writing to both databases is not the final architecture because it makes command latency and availability depend on both stores.

## 10. Benchmark-tranche acceptance criteria

- PostgreSQL schema, JSON serialization, and authority remain unchanged; runtime admission now uses the L1 accelerator and atomic insert result.
- ScyllaDB code is accessible only to explicit tests and benchmarks; there is no application registration or dual-write.
- Both providers atomically admit exactly one of 32 concurrent same-ID attempts.
- ScyllaDB stores and round-trips the MessagePack blob; PostgreSQL stores and round-trips the existing JSON text.
- The real-provider benchmark records complete first-insert and duplicate results at 1, 16, and 32 concurrent requests.
- The benchmark result, including the failed 64-way exploratory run, is used as a decision gate rather than assuming provider performance.
- Exactly one of concurrent deliveries with the same `CommandId` receives first-admission ownership.
- A duplicate is rejected before validation, aggregate replay, domain execution, event persistence, provider calls, or broker calls.
- A duplicate returns a deterministic successful result carrying the original `CommandId`.
- Cache eviction and process restart fall back to the PostgreSQL primary key without weakening correctness.

The following remain acceptance criteria for lease/status recovery or a separately approved future ScyllaDB migration:

- A crash after admission but before completion can be recovered through an expired conditional lease without allowing two active owners.
- An ambiguous conditional-write timeout is reconciled before processing.
- The persisted blob round-trips through the stable concrete command registry.
- Existing MessagePack payloads remain readable after compatible contract evolution.
- ScyllaDB unavailability prevents unguarded command execution and produces coded diagnostics.
- First-delivery and duplicate P95/P99 measurements are recorded under representative concurrency.
- PostgreSQL `command_log` retirement occurs only after shadow reconciliation reports no unexplained missing or mismatched command IDs.

## 11. Explicit exclusions

This design does not move the PostgreSQL event log, projector checkpoints, or aggregate concurrency controls. It does not claim globally exactly-once side effects across external systems. External provider and broker operations still require their own idempotency keys using the same `CommandId` or a stable derivative.
