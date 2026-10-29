# Market Data Feed Recovery Audit Implementation Plan

Status: Implementation and isolated packaging complete. Audit unit, BDD and real-service integration gates pass. The additional public-recovery observer mismatch is corrected and both quiet-heartbeat and production-duration stalled-provider tests pass. Final native regression passed (432 tests, zero failures). The authorized separate acceptance session has been launched; live baseline and visible-UI verification remain pending. See `MarketData-Feed-Recovery-Audit-Verification-v0.1.md` for the measured results.

## Purpose and scope

### Recovery outcome dialog follow-up

Every native fault incident must send an explicit completed or failed outcome. Rust publishes version-1 JSON confirmations on `Notify.MarketDataRecoveryOutcome.{ValueDate}` directly to the standalone UI. Existing typed failure notifications remain unchanged. Completion notices use the same immutable audit RecordId/OperationId, distinguish restored transport from verified live monitoring, and are retained in a bounded native journal outside dataset resources for reconnect retries. Each lane retains sixteen recent outcome facts; the UI deduplicates RecordIds and retains at most sixty-four queued outcome notices. A Host process restart does not replay this in-memory UI journal; durable incident history remains in the recovery event log/table.

An unacknowledged error must remain visible. Later confirmations queue behind it. Standalone application dialogs show message text on the left and equal 56-point vector icons on the right: red X for failure, green check for success and yellow triangle for warning. The OK button is centered under the message/icon area. Retryable diagnostics remain console warnings unless an explicit warning dialog is requested; this does not restore nonfatal application-error popups. The existing hybrid UI source is unchanged.

Active-failure budget selection must exclude stale deadlines belonging to healthy pending output. A new background-policy failure receives its actual recovery allowance; concurrent real failures and an active setup retain the earliest original deadline. Regression tests cover unrelated expired output, original active failures, zero/expired real budgets, notification deduplication, dialog geometry and isolated NATS delivery while backend queries are unavailable.

Record every native dataset recovery incident so an operator can determine when it began, whether a full reset ran, what was verified afterward, and whether the incident completed or failed. Rust remains the sole authority for dataset recovery. The API records facts through the existing actor, event source and projector infrastructure.

The initial target is the Rust market data backend. Record identity includes role and dataset so the same contracts support Options/GLBX.MDP3 and both futures datasets. Existing C# backend recovery behavior, pricing, quote freshness rules and live trading commands are unchanged.

The motivating incident is operation `74e1fd86-b113-441f-8773-3f058a06becb`. Its UI notification was delivered 607 times, but the Host/API logs contained no correlated completion. Subscription acknowledgements afterward proved reconnection, not complete trade monitoring. A missing audit record must never be interpreted as successful recovery.

## Agreed architecture

```text
Rust dataset recovery supervisor
    -> native recovery record journal outside dataset resources
    -> durable outbox and independent NATS delivery task
    -> MarketDataFeedRecoveryRecordFunctionActor
    -> MarketDataFeedRecoveryCommandActor
    -> committed recovery events in the event log
    -> acknowledged record storage

Committed recovery events
    -> existing event projector with durable process and replay queues
    -> market_data_service.marketdata_recovery_log

Actor and projector structured logs
    -> API durable log files
    -> existing OTLP log sink
```

Use the current NATS command/function request and reply conventions. Do not introduce a new JetStream command mechanism. The existing projector queues protect committed events; the Rust outbox protects the earlier delivery handoff.

### Actor identities and folders

Keep the requested domain command actor at:

`TomasAI.IFM.Domain.MarketData.Feed/Recovery/Command/Actor/MarketDataFeedRecoveryCommandActor.cs`

Add the acknowledged recording function at:

`TomasAI.IFM.Domain.MarketData.Feed/Recovery/Function/Actor/MarketDataFeedRecoveryRecordFunctionActor.cs`

The command actor owns one recovery aggregate per OperationId. The function owns one acknowledgement per RecordId. This separation is necessary because an incident has several transitions, while the existing function framework caches a completed request. Completing the start-record function must not suppress subsequent reset/completion records.

Commands, events, identifiers and wire contracts live in `TomasAI.IFM.Application.Contracts.MarketData.Feed/Recovery`, retaining the logical namespace `TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery`. This follows the completed domain -> application -> framework separation. SQL and database implementations live in Application.Storage. The domain projector uses Application.EventProjector's existing generic registration and durable replay infrastructure.

### Verified existing integration points

| Existing component | Use |
| --- | --- |
| Rust `runtime/recovery.rs` | Authoritative fault, proof, expiry and terminal state transitions |
| Rust `dataset/supervisor.rs` | Actual cancel, retirement, replacement and final reset outcomes |
| `BaseEventSourceFunctionActor` | Completion replay, explicit execution deadlines and EventThenProjection persistence ordering |
| Event source actor repositories | Aggregate persistence, optimistic concurrency and snapshot replay |
| `BaseEventProjector` | Durable process/replay queues and independently recoverable projections |
| API `ApiServerBuilderConfiguration` | Durable structured file logging and configurable OTLP export; Development enables log export |

The UI currently writes rotating JSONL and stderr, without an OTLP exporter. Direct UI telemetry export is outside this implementation. Backend recovery facts will become available through server telemetry independently of UI visibility.

## Recovery records and completion meaning

| Record | Authoritative trigger |
| --- | --- |
| `MarketDataFeedRecoveryStartedEvent` | The first classified fault opens an incident and its original recovery deadline |
| `MarketDataFeedDatasetResetStartedEvent` | The supervisor actually enters full reset after the soft recovery deadline; recording an intention alone is insufficient |
| `MarketDataFeedDatasetResetCompletedEvent` | Retirement of the old resource tree is confirmed and a replacement owner/generation is created; connection readiness is separate |
| `MarketDataFeedConnectionRestoredEvent` | Rust verifies current-generation provider activity and records subscription evidence separately |
| `MarketDataFeedRecoveryCompletedEvent` | Rust closes the incident under its existing recovery policy, with explicit completion scope and missing proof list |
| `MarketDataFeedRecoveryFailedEvent` | Final recovery/reset fails, expires, or cannot confirm retirement; include failed phase and cleanup certainty |
| `MarketDataFeedMonitoringRestoredEvent` | Required real reference/pricing/publication/persistence and loaded-workload evidence is satisfied, if this follows connection recovery |

Soft recovery expiry followed by reset is an escalation, not a final failed incident. Preserve the existing immediate UI reset/error notification, while recording the escalation accurately. Failure to store an audit record is `AuditDeliveryFailed`/`RecordStorageFailed`, never an invented dataset `RecoveryFailed` fact.

Completion scope must distinguish `TransportOnly`, `DatasetOperational` and `MonitoringReady`. Match the actual native completion decision; do not change the heartbeat policy to force a quote. Regular heartbeats can end a connection incident while financial data remains unavailable. In that case record `MonitoringReady=false` and missing proofs. Never manufacture quotes, update quote timestamps, or infer monitoring readiness from continuing trade-plan calculations.

Every opened incident needs an explicit completed or failed outcome when Rust makes that decision. Intentional stop, market close or process shutdown during an incident must be recorded as an interruption with a reason, not silently reported as successful recovery. An abrupt process termination before an outcome is captured leaves the incident incomplete/unknown; API startup must not guess a terminal result.

### Required record fields

- Contract version, immutable RecordId, OperationId, monotonic per-incident Sequence and record kind.
- Native Host/runtime instance, dataset role/name and authoritative API-supplied ValueDate.
- OccurredAtUtc, original StartedAtUtc, original recovery deadline, elapsed monotonic duration and reset phase/deadline where applicable.
- Original and replacement owner/generation identities, incident/attempt counters and original/latest failure details.
- Completion scope, old-resources-retired flag, subscription evidence, provider activity evidence, ready/missing proofs, affected workloads and monitoring readiness.
- Canonical immutable payload digest, with bounded redacted diagnostics. Preserve explicit truncation metadata where an upstream diagnostic is already bounded.
- Separate delivery metadata: AttemptId, AttemptStartedAtUtc, RequestDeadlineUtc, API ReceivedAtUtc, event-log commit identity and projection time.

Generate OperationId once when the incident starts and carry it through soft recovery, reset and outcome. Generate RecordId once for each fact. Preserve identities and occurrence timestamps across all retries and restarts. Do not reuse the current UI practice of assigning a new occurrence timestamp on each publication retry.

A later independent incident receives a new OperationId. Do not reuse an operation merely because its dataset and ValueDate match. The native recovery policy remains responsible for rearming reset allowances after verified recovery.

## Event source state and snapshot

Use **`MarketDataFeedRecoveryStartedEvent` as the snapshot event** for the recovery aggregate, as requested. It must contain the complete initial incident state and immutable initial record identity. The state repository loads the latest start snapshot for that OperationId and replays subsequent transitions in sequence.

The aggregate tracks accepted RecordIds/digests, sequence, current phase, timing, resource identities and outcome evidence. Recovery records are small and infrequent; do not retain tick data, provider objects, sockets or actors in this state. Do not use a later completion event as an implicit replacement for the requested start snapshot.

The function's per-record completion state is separate. Its completed event is `MarketDataFeedRecoveryRecordStoredEvent`; its failure response is `MarketDataFeedRecoveryRecordStorageFailedEvent`. Neither is the incident's recovery outcome.

Register stable persisted write identities and read aliases for every new event using the existing event identity registry. Test old/new event naming formats. Do not rename existing persisted events or alter their write identities.

## Acknowledgement and timeout protocol

1. Rust captures the fact and places it in a journal owned outside the replaceable dataset. The durable outbox writer and delivery worker cannot own dataset leases, sockets or cancellation guards.
2. Rust sends the recording function request with RecordId, digest, immutable record, AttemptId and an explicit request deadline.
3. The function forwards the record to `MarketDataFeedRecoveryCommandActor` under that same deadline. The command validates the sequence and digest, then commits the corresponding domain event.
4. After confirmed aggregate commit, the function persists its `RecordStored` completion using the framework's EventThenProjection ordering and replies with RecordId, digest, OperationId and committed event identity/version.
5. Rust acknowledges/removes the outbox record only after validating that durable completion. The table projection and OTLP export need not finish first.
6. The existing projector persists the table row and advances its durable checkpoint. A projection outage cannot revoke an already committed record acknowledgement.

Use a configurable **120-second logging request budget by default**, explicitly carried with each attempt. It is independent of the dataset's original 120-second recovery window. All nested function/command persistence calls inherit the same attempt deadline; no additional one-, three- or five-second timers are introduced. Delivery retries can continue after dataset recovery has finished.

An old record's OccurredAtUtc is not its new delivery attempt's start time. A record queued during an API outage must not arrive already expired merely because it occurred hours earlier.

### Ambiguous outcomes and duplicates

- Timeout, cancellation, missing reply or disconnect means **commit outcome unknown**, not proven rollback. Retain the record and reconcile/retry the same RecordId and immutable payload.
- Each attempt has a fresh transport AttemptId/deadline, but function state matching and deduplication compare RecordId and immutable payload digest. Mutable attempt headers must not cause a conflict.
- A late confirmed commit remains authoritative. A retry returns the stored acknowledgement without appending another incident event or table row.
- Persist only successful function storage completion as a cached success. A transient storage/transport failure must not permanently prevent retries for that RecordId.
- Same RecordId with different content is a conflict: preserve the original, log the conflict and retain a diagnosable rejected record; never overwrite audit history.
- Deliver an incident in sequence. Sequence 1 is the Started snapshot. Reject missing predecessors as retryable; never fabricate the Started event from a later completion message. Preserve all earlier records while later facts wait.
- Process independent incidents with bounded concurrency. Storage ordering cannot serialize or block native dataset recovery.

No distributed transaction is required across the function and aggregate streams: both persistence points are idempotent, and a crash between them is repaired by retrying the original RecordId. Explicitly test that boundary.

## Outbox and isolation requirements

The outbox is outside dataset resources and survives their reset. Use a configured per-Host path independent of a disposable build output directory, atomic committed entries and validated recovery of incomplete writes. Serialize values only; never serialize credentials or native resources.

Proposed configurable defaults are 256 in-memory records, 256 KiB maximum encoded record, 128 MiB durable storage, 4,096 stored records and at most four independent deliveries. Reserve capacity for terminal/escalation facts. Record phase changes and aggregate retry counts rather than generating an audit event for every repeated health observation or delivery retry.

Recovery must not await file I/O, broker publication, function replies, database commits, table projection or telemetry export. A separate owned writer/delivery service handles those operations. Recovery state transitions remain observable even if that service is slow.

The persistence guarantee applies after an outbox entry has durably committed. Eliminating the window between creating a fact and durable disk completion would require waiting for storage; do not claim crash-proof delivery before that commit while also promising nonblocking recovery. Crash tests must state which boundary they exercise.

Capacity exhaustion, disk failure and invalid entries require explicit bounded diagnostics and an audit-unavailable signal. Do not silently discard required records, substitute quote-style oldest-message replacement, grow queues without limit, or stall the dataset. Expose pending count/bytes, oldest unacknowledged age, retries, rejected records and outbox write/export failures. Infinite outages cannot be covered by finite storage; preserve that operational limit in acceptance reporting.

## Database projection and telemetry

Create `market_data_service.marketdata_recovery_log` through normal schema startup/migration, before the projector begins. One immutable row represents one committed recovery fact.

- Primary/unique key: RecordId. Store OperationId, Sequence, ValueDate, role/dataset, kind, original timing, generation/owner IDs, outcome/evidence and bounded diagnostic payload.
- Add unique `(OperationId, Sequence)` and indexes for `(ValueDate, Role, Dataset, OccurredAtUtc)` and `(OperationId, Sequence)`.
- Idempotent insert must verify matching payload digest on conflict. It must not silently accept conflicting data.
- Use the existing event projector descriptor, durable process queue, replay queue and checkpoint conventions. Event replay rebuilds the table without duplicate rows.
- Keep history append-only. Market close does not erase recovery audit history. Retention/archiving is explicit and separate from live feed intent cleanup.
- Provide bounded queries by ValueDate, dataset and OperationId so operators can inspect a whole incident and identify missing outcome records.

Write structured server logs after durable event commit with RecordId, OperationId, transition, original OccurredAtUtc, ReceivedAtUtc, elapsed time, generations, verification level and errors. Log projection completion/failure separately. Duplicate delivery does not emit another recovery-start/completion business log; attempt-level diagnostics retain the same correlation.

Use the existing API log file and OTLP sinks. Verify export at the collector, not just configuration. Telemetry can be delayed or dropped independently; event-log/table persistence remains authoritative. Log storage and telemetry delivery failures without recursively submitting those failures through the broken audit pipeline.

Preserve the current UI notification behavior independently of audit persistence. A dataset reset/error cannot wait for the database to become available. Deduplicate the UI by stable identity and distinguish the old occurrence timestamp from later delivery times. Do not introduce direct UI OTLP export in this scope.

Use RecordId for UI notification deduplication and OperationId for incident correlation. Repeated delivery of one reset record produces one dialog, while a later distinct final-failure record must remain eligible for its own required notification. Deduplicating the whole incident by OperationId would incorrectly suppress that escalation.

## Implementation stages

| Stage | Work | Required exit evidence |
| --- | --- | --- |
| 1 Contracts and compatibility | Define immutable record, IDs, completion scopes, audit commands/events, failure responses, event identities and actor routes | Serialization fixtures; immutable digest and retry identity tests; persisted identity approval |
| 2 Recovery aggregate | Implement the named command actor, state/repository and Started snapshot loading | Ordered replay, duplicate/conflict rejection and snapshot-plus-tail tests |
| 3 Recording function | Implement the function actor, forwarding, cached storage acknowledgement and explicit common attempt deadline | Lost reply, late commit, retry after timeout and crash-between-streams tests |
| 4 Projection and schema | Add deployment schema registration, additive initialization before first audit projection/query, storage/query contracts and the durable projector descriptor | Idempotent insert, restart, replay, DB outage and table reconstruction tests |
| 5 Native instrumentation | Capture exact supervisor transitions, stable incident IDs, original timestamps and readiness evidence; retain detailed raw-reference broker failure cause | Healthy/retry/reset/final-failure, quiet heartbeat and accurate evidence tests |
| 6 Native delivery | Add bounded independent journal/outbox, NATS function request/reply, acknowledgement validation and restart replay | API/NATS outage, capacity/disk failure and resource-retirement isolation tests |
| 7 Server telemetry | Emit correlated commit/projection logs through existing sinks and add audit health metrics/queries | Durable file evidence plus actual OTLP collector records with matching IDs |
| 8 Integrated acceptance | Exercise native -> NATS -> function -> aggregate event log -> replay projection -> SQL -> telemetry | All acceptance cases below pass; then run live validation on updated binaries |

Document Rust ownership, cancellation, wire layout, unsafe boundaries, bounded capacities and idempotency decisions. Keep changes limited to recovery audit and the existing generic integration points needed by it. A milestone is not implementation completion.

## Acceptance criteria

### Persistence and identity

1. A successful soft recovery produces one Started record and the appropriate restored/completed records with one OperationId and original timestamps.
2. Soft expiry records actual reset entry; reset retirement and replacement have matching old/new identities; successful connection recovery is recorded with its actual completion scope.
3. Final timeout, reset error, panic or unconfirmed cleanup produces an explicit failed outcome with original cause and final failed step. No false completion is emitted.
4. Replaying the motivating 607-delivery scenario creates one event and one table row per distinct RecordId. The occurrence timestamp never changes.
5. Same RecordId/different digest and same operation/sequence/different payload are explicit conflicts.
6. Snapshot restoration uses `MarketDataFeedRecoveryStartedEvent` and subsequent events reconstruct the exact incident state. Completion of one record's function cannot suppress later records.
7. A fresh API process accepts retained records, including their old ValueDate. The API's current ValueDate does not replace or invalidate historical audit dates.

### Transport and failure boundaries

8. API offline before delivery, NATS disconnect, no responders, lost completion reply and cancellation retain the outbox record. Recovery/reset still progresses under its original budget.
9. Commit-before-reply timeout and crash-between-aggregate/function-commit reconcile to one stored fact. A failed attempt does not poison future recording attempts.
10. Logging attempt expiry is bounded by its explicit 120-second default and inherited deadline. No nested short timer or restarted dataset recovery budget is introduced.
11. Outbox restart after durable commit replays the same IDs. Incomplete writes, full disk, full capacity and invalid entries produce explicit audit health failures without blocking dataset retirement.
12. Broker, function actor, database, projector and OTLP collector stalls independently cannot retain or delay native dataset resources. Verify cancellation and actual joins, not just a canceled token.

### Projection and observability

13. Event-log commit succeeds while table projection is unavailable; durable replay later produces the row without a second source event.
14. Table rebuild and repeated projector delivery are idempotent; a newer API session can query the earlier recovery timeline.
15. Confirm original start/reset/completion/failure IDs and timestamps in the event log, SQL table, durable server file and actual OTLP receiver.
16. The existing UI receives required terminal/reset notification even when audit persistence fails; duplicates of one RecordId yield one dialog. A later distinct final-failure notification in the same operation is not suppressed. Recording failures do not create recursive dataset recovery failures.

### Recovery semantics and live checks

17. Fifteen minutes of valid provider activity/heartbeats without quotes produces no false dataset failure or invented MonitoringReady record.
18. Connection restoration with unavailable prices records the limited verification correctly. Full monitoring restoration requires actual current-generation evidence; cached trade-plan processing is insufficient.
19. A real recoverable fault and a forced two-minute escalation/final-failure test show a complete correlated timeline, without changing the C# backend behavior or stopping unrelated datasets.
20. Before any live fault injection, verify the API/UI and futures/signals baseline. Verify fresh option updates when full monitoring is claimed. Do not retroactively mark the earlier incident accepted from the new session's health.

Implementation acceptance requires passing relevant Rust tests, strict Clippy, managed actor/repository/projector tests, real NATS and PostgreSQL integration tests, native FFI/wire tests where used, and collector export checks. Record unavailable live/service gates as incomplete with their specific blocker; do not claim acceptance from unit tests alone.

## Deliverables and completion reporting

- Actors, contracts, snapshot repository, projector, schema/query support and stable event identities.
- Native transition capture and bounded retained delivery implementation with documented lifecycle ownership.
- Correlated durable server logs, OTLP records and audit delivery health evidence.
- Automated happy, boundary, duplicate, unknown-commit and failure tests, plus a timestamped integration report.
- A live report that distinguishes transport restoration, dataset readiness and verified trade monitoring, with any remaining incomplete incidents listed explicitly.

This plan does not claim that the old recovery completed. It provides the records needed to establish the outcome of future incidents.

## Implementation decisions and operational limits

- The existing backend-status DTO gains one optional MessagePack field, `RecoveryAudit` (key 9). The nine existing fields and all `IMarketDataApi` methods are preserved. Old nine-field payloads decode with a null audit health value. The native baseline guard permits only this exact additive fragment; the original C# backend checksums are unchanged.
- The normal API startup does not execute the full deployment schema initializer. The audit table therefore also initializes before its first projection/query, using the canonical additive schema SQL. Initialization failures are retryable through projector replay. Audit storage availability is not an API startup or dataset recovery gate.
- Audit source events have frozen CLR write identities and explicit stable read aliases in both persisted-identity manifests. The Started source event is the aggregate snapshot; subsequent facts remain replayed in sequence.
- Native capture admits at most 256 undurable facts, reserving 32 slots for terminal/reset facts. The disk spool is capped at 4096 entries/128 MiB, with terminal reserve. Exhaustion, invalid entries and write failures are visible in passive audit health; they cannot delay dataset retirement. Bounded storage cannot promise infinite retention during an unlimited outage.
- A record becomes restart-durable after its file is synced and atomically committed. Capture before that point has a finite crash-loss window. Tests qualify partial-file and process-restart behavior, not a physical power-cut guarantee.
- Broker delivery is independent of dataset ownership. Each recording attempt has one explicit 120-second deadline; retry scheduling does not renew the dataset recovery budget. An unavailable API retains committed records until a matching durable acknowledgement arrives.
- Root shutdown attempts an audit-only bounded flush of captured interruption records. It does not await broker acknowledgement and does not change the dataset's recovery deadline.
- An invalid API time source rejects audit capture visibly rather than inventing a timestamp. Dataset recovery still proceeds. Historical ValueDate always comes from the copied authoritative API context.
- `GET /api/market-data/recovery-log` returns a bounded timeline by ValueDate, optional dataset and OperationId. `GET /api/market-data/recovery-audit-health` exposes passive outbox diagnostics. Neither endpoint drives recovery or introduces UI polling.
- Existing UI error transport remains independent. Rust notifications use RecordId for deduplication and retain OperationId for correlation. A reset notification and a later final failure in the same incident are separate eligible notifications.
