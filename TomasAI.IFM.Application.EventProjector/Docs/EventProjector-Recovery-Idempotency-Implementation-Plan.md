# Event-projector recovery and idempotency implementation plan

## Implementation status

Tranche A was completed on 2026-08-09. The repository now contains the reliability options, stable effect identity,
explicit projection idempotency/result/context/descriptor contracts, readiness snapshot contract, additive PostgreSQL
execution-state and outbox schema, fenced create/claim/renew/transition/terminal APIs, joined keyset recovery page, and
deterministic storage contention tests. The current projector runtime has not been switched to these contracts.

The captured current-path CPU/allocation lower bound is 52.44 ms/6.49 MB at 1,000 events, 109.56 ms/64.85 MB at
10,000 events, and 1,002.74 ms/648.53 MB at 100,000 events. PostgreSQL and NATS latency are intentionally excluded.
Tranche B is the next implementation gate.

## Status and scope

This document is the accepted staged implementation plan for system-wide optimization work package SWO-06. Each
tranche remains independently gated; completing contracts or schema does not authorize production activation of the
new projector runtime path.

The current production implementation has one concrete projector, `FundEventProjector`, backed by PostgreSQL event
source/projector state, ScyllaDB Fund projections, Redis-backed Blackboard state, and NATS JetStream process/replay
queues. The design must remain generic enough for additional domain projectors without introducing a distributed
transaction coordinator.

The goal is bounded, observable at-least-once processing with deterministic effect identities and idempotent business
effects. The goal is not to claim exactly-once delivery across PostgreSQL, ScyllaDB, Redis, and NATS; that guarantee is
not achievable across those independent systems without stronger coordination than the application currently owns.

## Verified current behavior

The repository audit established the following facts:

1. Projector state is keyed by `(EventId, ProjectorName)` in PostgreSQL. `EventId` is the globally unique event-log
   version, so that pair is already a stable source-projection identity.
2. `DomainEventsProjectionAsync` persists initial state before publishing to the process queue. This closes the
   event-log-commit/queue-publication gap because startup recovery can find explicitly marked events.
3. Every workflow side effect runs before its next state checkpoint. A crash in that interval repeats the side effect.
   Processing, target projection, completion, and failure publication are therefore all at-least-once.
4. `event_projector_state` uses an unconditional upsert. It has no revision, execution owner, fencing token, or compare-
   and-set transition, so two deliveries can both execute the same active stage and a stale worker can overwrite newer
   state.
5. `NatsJSDurableReplayQueue.DequeueAsync` registers the handler and starts workers. `EnqueueAsync` also starts workers.
   Consequently, `BaseEventProjector.StartAsync` can consume while startup recovery is still scanning and enqueueing,
   despite its current comments describing registration-before-start behavior.
6. Startup recovery materializes every eligible event into one `ICollection`, reloads each state individually, and
   enqueues sequentially. Memory, query time, state-read round trips, and startup duration grow with the complete
   backlog.
7. The process and replay workers can run concurrently. JetStream message de-duplication suppresses duplicate publishes
   only within its configured duplicate window; it is not a permanent business-effect idempotency guarantee.
8. `EventProjectorBuilder.RunAsync` replaces the projector-wide maximum-attempt callback for every event execution.
   The callback closes over that call's generic completion/failure types. Concurrent events of different types can
   therefore leave the projector with the wrong terminal callback.
9. Maximum-attempt handling marks state terminal and clears cache, but does not publish the typed failure event.
10. The public builder accepts `Func<TEvent, Task>`. Its explicit unsuccessful-`ServiceResult` branch is unreachable;
    normal failures are exceptions. Conversion returning `null` can also silently suppress a terminal event.
11. `AttemptNumber`, `IsReplay`, the unused stream-oriented `EventProjectorState`, `EventProjectorRetryActionType`, and
    `EventProjectorStageTimings` do not currently form one coherent runtime model.
12. `FundEventProjector` is the only concrete `BaseEventProjector` consumer. Its eight handlers are inserts, updates,
    deletes, or a no-op, but their retry/idempotency contract is implicit rather than enforced by the projector API.

## Required correctness model

### Stable identities

Use these identities everywhere; never generate a new identity during retry:

| Identity | Format | Purpose |
| --- | --- | --- |
| Projection | `(ProjectorName, EventId)` | One projector's handling of one immutable source event. |
| Stage effect | `(ProjectorName, EventId, EffectKind)` | Processing publication, target apply, completion publication, or failure publication. |
| Message | deterministic UUID/hash of the stage-effect identity | Durable outbox key, NATS message identity, logs, metrics, and downstream de-duplication. |
| Execution | random UUID plus monotonically increasing state revision | Temporary worker ownership and stale-worker fencing; never a business identity. |
| Stream | persisted source `EventStreamId` | Per-stream ordering and explicit supersession decisions. |

`EffectKind` must be a closed enum. Type names, timestamps, retry count, process ID, and queue delivery sequence are not
valid idempotency keys.

### Invariants

1. Immutable event-log rows remain the source of truth and are never deleted or rewritten by recovery.
2. A projector state transition succeeds only from its expected stage/revision and only for the active execution token.
3. A second delivery of a terminal projection performs no target or publication effect.
4. Applying the same source event more than once produces the same durable target state and no duplicate business row.
5. Every enabled downstream publication has one durable outbox identity. Re-publication may occur after an ambiguous
   acknowledgement, but it retains that same identity so consumers can de-duplicate it.
6. A typed terminal failure is recorded durably before a replay message is acknowledged at maximum attempts.
7. Events in the same source stream do not overtake an earlier unresolved event unless a reviewed projector policy says
   the earlier event is safely superseded.
8. Missing projection state never implies completion or supersession.
9. Recovery queries, in-memory batches, concurrent work, and retry delay are bounded by configuration.
10. Redis/Blackboard is a cache only. PostgreSQL state, outbox, and immutable event log determine recovery.

### Delivery guarantee wording

The supported guarantee will be documented as:

> Durable at-least-once delivery with deterministic effect identities, fenced checkpoint transitions, idempotent target
> operations, and consumer-visible publication keys.

Do not describe the design as exactly once. A publisher can crash after NATS accepts a message but before PostgreSQL
records delivery, and a target database can acknowledge an idempotent mutation before the projector checkpoint is
advanced. Both windows are handled by safe repetition rather than a false exactly-once claim.

## Target architecture

### Durable execution state

Extend the existing `event_projector_state` additively so an older binary can ignore the new fields during rollback.
The reviewed migration should add equivalent strongly typed columns for:

- source event stream ID and event name;
- state revision;
- active execution token and lease expiry;
- retry count, next-attempt timestamp, and last error timestamp;
- blocked/manual-resolution reason;
- last successfully completed stage; and
- a real UTC timestamp column for indexed backlog-age queries, while retaining the legacy text timestamps until the
  rollback window closes.

All transitions use one PostgreSQL conditional statement such as `UPDATE ... WHERE ProjectorName = ... AND EventId =
... AND Revision = ... AND ExecutionToken = ... RETURNING ...`. Unconditional runtime upsert remains only for initial
state creation with `ON CONFLICT DO NOTHING` and for an explicit migration/repair tool.

Claims use a bounded lease plus a monotonically increasing revision. Expiry permits recovery after process death, but a
resumed stale worker cannot checkpoint because its token/revision no longer matches. The target operation must still be
idempotent because fencing the checkpoint cannot revoke an external side effect that has already started.

The active runtime will continue to use `EventProjectorStateReadModel`, extended or replaced through an explicit
mapping. The unused `Application.EventProjector.EventProjectorState` record must not become a second competing state
model. Either migrate its useful stream fields into the active contract and delete it, or delete it after confirming no
serialization dependency.

### Durable publication outbox

Add `event_projector_outbox` in the PostgreSQL event-source database with a primary key equivalent to
`(ProjectorName, EventId, EffectKind)` and fields for deterministic message ID, event type, serialized payload, status,
attempt count, next-attempt time, created/published timestamps, and last error.

The transition that selects the next workflow stage and the insert of its publication record must commit in one
PostgreSQL transaction. A bounded dispatcher publishes pending records and records delivery. If delivery acknowledgement
is ambiguous, it publishes the same payload with the same message ID again. JetStream de-duplication is helpful but
finite; downstream handlers must retain permanent business de-duplication where duplicate effects would matter.

Processing-event publication remains configurable. Completion and terminal-failure publications are always represented
explicitly when their event conversion exists. A failed or null conversion becomes a durable manual-resolution error;
it is never silently treated as successful publication.

### Projection operation contract

Replace the untyped `Func<TEvent, Task>` reliability boundary with an explicit operation contract carrying a
`ProjectionExecutionContext`:

- projector name, source event ID, source stream ID, stage-effect key, and execution token;
- cancellation token;
- result `Applied`, `AlreadyApplied`, `Superseded`, or `Failed`;
- declared idempotency strategy: natural-key upsert/delete, target receipt, or reviewed commutative operation.

Every concrete handler must document and test its strategy. Non-idempotent read-modify-write operations cannot be
registered until their target database stores a durable receipt or performs a conditional mutation using the stage-
effect identity. PostgreSQL projector state alone is not a receipt for a ScyllaDB side effect.

For the initial Fund implementation, audit all eight operations. Existing key-based inserts/updates/deletes can use
natural idempotency only after repeat-apply tests prove identical state. Any operation that increments, appends under a
new key, calls an external service, or generates a new ID must use a target receipt or be excluded from activation.

### Stable projector descriptor

Configure supported event types once during projector construction/startup. Each descriptor owns:

- source event type;
- target operation;
- completion-event factory;
- failure-event factory;
- processing-publication policy;
- stream-order/supersession policy; and
- target idempotency strategy.

The queue's maximum-attempt callback is registered once per projector and dispatches through this descriptor table.
It is not replaced for every event. Unknown types fail closed into manual resolution with their source event preserved.

### Queue lifecycle separation

Split durable-queue preparation from worker execution. The target lifecycle is:

1. validate projector descriptors and options;
2. ensure JetStream resources exist;
3. register the process and terminal handlers without starting consumers;
4. scan pending projector states in bounded keyset pages ordered by source stream/event version;
5. publish missing recovery messages without implicitly starting workers;
6. record recovery inventory metrics;
7. start process/replay/outbox workers; and
8. publish projector readiness.

`DequeueAsync` and `EnqueueAsync` currently violate this separation by starting workers. Introduce explicit prepare,
publish, and start APIs, then retain the old methods only as compatibility wrappers until every caller is migrated.
Startup cancellation must leave readiness false, stop any partially started workers, and preserve all durable rows.

### Bounded recovery and stream ordering

Replace the all-rows recovery query with keyset pagination. The query returns source event and state together, avoiding
the current state N+1 read. Proposed initial configuration, subject to baseline measurements:

| Option | Initial value | Constraint |
| --- | ---: | --- |
| Recovery batch size | 256 | 1-2,048 |
| Concurrent recovery streams | `min(Environment.ProcessorCount, 8)` | 1-32 |
| Maximum replay attempts | 3 | 1-20 |
| Initial replay delay | 30 seconds | positive; exponential backoff capped at 2 minutes |
| Claim lease | 2 minutes | longer than measured p99 stage time plus safety margin |
| Outbox batch size | 256 | 1-2,048 |

Concurrency is across streams, never within one stream. A stable stream hash assigns work to a bounded set of logical
lanes, preserving source version order without creating one task or semaphore per aggregate. Process and replay
deliveries both enter the same ordering/claim path.

The default supersession policy is `NeverSupersede`. An event may be marked `Superseded` only when a projector-specific
policy proves from immutable source versions and event semantics that the newer event fully replaces the older effect.
No Fund event currently has that proof, so SWO-06 should ship with no automatic Fund supersession. The durable policy
decision records superseding event ID, policy version, timestamp, and reason so it is auditable and replayable.

An unresolved terminal failure blocks later events in the same stream by default. Operator resolution can retry the
exact event, mark it skipped with a required reason, or apply an approved supersession policy. Continuing silently would
make a stateful projection appear current after omitting a required transition.

## Crash-point behavior

| Crash point | Required restart behavior |
| --- | --- |
| Event/state committed before process publish | Recovery republishes the same process message identity. |
| Process delivery before execution claim | JetStream redelivers; only one conditional claim wins. |
| Processing publication accepted before checkpoint | Outbox republishes the same message identity; consumer de-duplication prevents duplicate business effect. |
| Target mutation accepted before projector checkpoint | Retry repeats the same idempotent operation/receipt and receives `AlreadyApplied` or identical final state. |
| Checkpoint committed before completion outbox dispatch | Outbox dispatcher publishes the pending completion record. |
| Completion/failure publish accepted before delivered marker | Dispatcher republishes the same message identity. |
| Maximum attempts reached before terminal record | One PostgreSQL transaction records terminal state and failure outbox before queue acknowledgement. |
| Worker lease expires while a stage is running | New owner may retry; stale owner cannot checkpoint, and target idempotency absorbs duplicate application. |
| Unknown event type or failed terminal conversion | Preserve source/state, mark manual resolution, emit metrics, and do not acknowledge as successful completion. |

Each row becomes a deterministic fault-injection test. No tranche is complete based only on happy-path tests.

## Implementation tranches

### Tranche A: Baseline, contracts, and additive schema

1. Capture current recovery/query/queue round trips, allocations, and elapsed time at 1,000, 10,000, and 100,000
   pending events using a fake transport plus a real PostgreSQL comparison where practical.
2. Add projector options with validated bounds; do not hide constants in queue/projector implementations.
3. Add stage-effect identity, execution context/result, descriptor, idempotency strategy, and projector readiness
   contracts.
4. Add the state columns, outbox table, indexes for `(ProjectorName, terminal status, EventId)` and pending outbox
   scheduling, plus idempotent schema migration definitions.
5. Add conditional state-creation, claim, renew, transition, terminalize, and keyset-page storage APIs.
6. Add storage concurrency tests proving stale revisions/tokens cannot regress state.

Gate: schema rollback compatibility, storage integration tests, zero production activation, and recorded baseline.

### Tranche B: Lifecycle separation and bounded recovery

1. Separate JetStream resource preparation/handler registration from worker startup and publish-without-start behavior.
2. Page pending events with event/state joined in one query; remove the per-event state reload.
3. Enqueue recovery through bounded stream lanes while preserving order within each stream.
4. Publish readiness only after recovery inventory is durably queued and all workers start successfully.
5. Roll back partial startup and keep readiness false on cancellation/failure.
6. Add 0/1/partial/multi-page/100,000-event recovery tests, cancellation tests, and multiple-instance claim tests.

Gate: bounded memory, no worker consumption before the recovery handoff, and no source-stream reordering.

### Tranche C: Fenced execution and Fund target idempotency

1. Replace mutable per-call builder setup with immutable projector descriptors.
2. Route process and replay deliveries through one claim/fencing path.
3. Convert all eight Fund handlers to the explicit operation contract.
4. Prove repeat application for every Fund insert, update, delete, and no-op. Add target receipts only where natural
   idempotency is insufficient.
5. Reject unregistered/unknown event types into manual resolution.
6. Remove or migrate the unused parallel state/retry/timing types once references and serialized compatibility are
   proven.

Gate: crash-after-target-write tests show identical durable Fund state and no duplicate business identities.

### Tranche D: Transactional outbox and terminal failures

1. Persist publication payloads and state transitions atomically in PostgreSQL.
2. Add the bounded outbox dispatcher using deterministic message identities and retry scheduling.
3. Register one stable maximum-attempt handler per projector.
4. Create and enqueue the typed failure event before terminal acknowledgement; conversion failure becomes manual
   resolution rather than silent success.
5. Add consumer idempotency guidance and verify all in-scope Fund completion/failure consumers use stable identities.
6. Add operator queries/actions for pending, failed, blocked, retry-exact, and skip-with-reason states.

Gate: every publication/checkpoint crash window is fault-injected, and terminal failure is visible and delivered.

### Tranche E: Ordering, observability, benchmark, and rollout

1. Enforce same-stream ordering across process and replay paths; retain `NeverSupersede` for Fund until a separate
   semantic policy is approved.
2. Add OpenTelemetry instruments for accepted, claimed, applied, already-applied, retried, superseded, blocked,
   terminal-failed, and completed events; stage duration; recovery batch duration/size; pending count; oldest backlog
   age; claim conflict; lease expiry; outbox pending/age/retry; and worker utilization.
3. Register the projector meter with the existing OTLP pipeline and document Grafana panels and alerts.
4. Run before/after recovery and steady-state benchmarks on the same host, PostgreSQL, ScyllaDB, and NATS topology.
5. Run unit, PostgreSQL storage, NATS queue, Fund projection integration, complete Fund integration, and all ten domain
   integration projects sequentially in Release.
6. Roll out observe-only telemetry, then fenced claims/bounded recovery, then target idempotency, then outbox behavior.
   Each activation has an independent configuration switch and rollback checkpoint.

Gate: acceptance criteria, performance budgets, dashboards, operator procedure, and full regression suite are complete.

## Benchmark and performance gates

Create `TomasAI.IFM.Application.EventProjector.Benchmarks` or place the benchmark in the closest existing application
benchmark project only if dependency direction remains clean. Record raw BenchmarkDotNet artifacts outside source
control and summarize results in `Documents/system/System-Wide-Optimization-Results.md`.

Measure at minimum:

- startup recovery at 1,000, 10,000, and 100,000 pending events;
- peak managed memory and allocated bytes during recovery;
- PostgreSQL rows/round trips per recovered event and per page;
- enqueue rate and time until projector readiness;
- steady-state single-event p50/p95/p99 and allocations;
- eight and 64 independent streams under backlog;
- one hot stream, proving order and absence of overlap;
- process/replay duplicate delivery and claim-conflict cost;
- outbox dispatch throughput and oldest-message age; and
- shutdown/cancellation time with pending work.

Initial budgets for review:

- recovery memory is O(batch size + configured lanes), not O(total backlog);
- no state N+1 query during recovery;
- same-stream active projection count never exceeds one;
- 100,000-event recovery completes without unbounded task, cache, or semaphore growth;
- steady-state p99 regression is no more than 15% unless the added durable guarantee is separately approved;
- no retry can create a second Fund business identity; and
- every terminal failure appears in state, metrics, logs, and the typed failure publication path.

## Verification matrix

| Layer | Required coverage |
| --- | --- |
| Shared contracts | Identity stability, option validation, state classification, policy defaults. |
| PostgreSQL storage | Initial insert race, claim contention, lease takeover, stale token/revision rejection, atomic state/outbox commit, keyset paging. |
| NATS queue | Prepare/register without start, publish without start, start/stop/restart, duplicate message ID, terminal callback failure, cancellation. |
| Projector unit | Every stage transition and every crash point, terminal short-circuit, unknown type, null/throwing conversion, cache loss. |
| Fund target | Repeat each of eight operations, conflicting duplicate payload, delete retry, update retry, no new generated IDs. |
| Integrated Fund | Real PostgreSQL + ScyllaDB + NATS restart/replay, maximum attempts and typed failure, same-stream order. |
| System | Complete Release build and the established 193-test ten-domain sequential integration gate. |

## Metrics and operational views

Use meter name `TomasAI.IFM.Application.EventProjector`. Low-cardinality tags are projector, stage, outcome, retry action,
and operation type. Never tag metrics with event ID, stream ID, command ID, aggregate ID, exception message, or entity ID;
those belong in structured logs/traces.

The initial Grafana view should include:

1. processing/completion/failure rates by projector;
2. pending projector states and oldest age;
3. p50/p95/p99 stage and end-to-end duration;
4. retry, claim-conflict, lease-expiry, already-applied, and terminal-failure rates;
5. recovery scanned/enqueued rate and readiness duration;
6. outbox pending count, oldest age, publish latency, and retry rate; and
7. NATS process/replay pending and redelivery metrics correlated with PostgreSQL backlog.

Alerts should cover projector not ready, oldest backlog above the operating objective, terminal/manual-resolution state,
outbox age, repeated lease expiry, and sustained replay exhaustion.

## Deployment and rollback

1. Back up the event-source database and record current nonterminal projector states and JetStream pending counts.
2. Apply additive PostgreSQL schema and NATS-compatible changes with reliability mode disabled.
3. Deploy metrics/observe-only identity computation and compare state/backlog counts.
4. Drain projector workers before enabling fenced execution. Never run legacy and enforced workers concurrently for the
   same projector.
5. Enable bounded recovery and claims, then Fund idempotency enforcement, then the publication outbox in separate
   deployments or configuration changes.
6. Preserve old columns and queue resources through the rollback window. Do not delete pending outbox records on
   rollback.
7. To roll back, close projector readiness/intake, stop workers, verify no active claims, deploy the prior binary, and
   leave additive schema/data in place. An older upsert must be verified not to erase new columns.
8. Schema cleanup and removal of compatibility wrappers is a later migration after paper-trading evidence and the
   rollback window close.

## Accepted decisions

The following decisions were accepted before Tranche A implementation:

1. Accept durable at-least-once plus deterministic idempotency rather than claiming distributed exactly once.
2. Require the maximum-attempt path to create a typed failure publication before acknowledging terminal replay.
3. Default to blocking a stream after an unresolved terminal failure.
4. Ship Fund with `NeverSupersede`; add supersession only for a separately proven event semantic.
5. Keep actor/application intake closed until the recovery inventory is durably handed to the queue and projector
   workers are ready.
6. Implement SWO-06 in the five tranches above, with a review/activation gate after each tranche.

Tranche B may now implement queue lifecycle separation and the fenced execution path, but production activation stays
disabled until its crash-point, ordering, shutdown, and regression gates pass.
