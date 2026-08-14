# Event Projector Implementation Details

## Purpose and current status

`TomasAI.IFM.Application.EventProjector` projects committed event-sourced domain events into target read stores.
Descriptors use a durable NATS JetStream process/replay queue by default and may explicitly select a bounded
process-local non-durable queue. SWO-06 Tranches A-E are implemented:

- additive PostgreSQL execution state and compare-and-set fencing;
- bounded, joined-keyset startup recovery;
- immutable projector descriptors;
- one claim/stage path for process and replay delivery;
- explicit target idempotency contracts; and
- fail-closed handling for unregistered and unknown source events;
- atomic PostgreSQL publication outbox transitions and leased bounded dispatch;
- typed maximum-attempt failure publication; and
- bounded operator pages plus retry-exact and skip-with-reason actions;
- durable same-projector/same-stream execution ordering across process and replay delivery; and
- low-cardinality OpenTelemetry instruments plus an optional PostgreSQL operational snapshot sampler.

`BoundedRecoveryEnabled`, `FencedExecutionEnabled`, `TransactionalOutboxEnabled`, and
`BacklogMetricsPollingEnabled` are enabled in API Server and actor integration configuration. The current projector
set has passed the cross-store idempotency and persistent-infrastructure test-isolation gates. Production rollout and
all future target-operation additions remain subject to the operational and extension gates in
`Documents/system/Event-Sourcing-Projection-Split-Brain-Controls.md`.

## Source map

| File | Responsibility |
| --- | --- |
| `BaseEventProjector.cs` | Validates descriptors, routes each event to one delivery lane, owns lifecycle/readiness, selects execution, and publishes typed actor events. |
| `EventProjectorTransientQueue.cs` | Runs explicitly non-durable descriptors through a bounded, ordered, process-local channel. |
| `EventProjectorExecutionEngine.cs` | Claims a leased execution, applies fenced stage transitions, releases failed claims for retry, terminalizes failures, and creates explicit target-operation contexts. |
| `EventProjectorOutboxDispatcher.cs` | Claims bounded outbox batches with `SKIP LOCKED`, publishes typed events, and records delivery or bounded retry. |
| `EventProjectorOutboxSerializer.cs` | Serializes concrete MessagePack payloads and assigns deterministic consumer-visible event IDs from stage-effect identities. |
| `EventProjectorRecoveryCoordinator.cs` | Pages recoverable event/state rows, preserves same-stream ordering, and enqueues stable recovery candidates with bounded cross-stream concurrency. |
| `EventProjectorMetrics.cs` | Defines allocation-free-when-dormant counters/histograms and process-local observable projector gauges. |
| `EventProjectorMetricsObserver.cs` | Optionally samples the durable PostgreSQL operational snapshot at a bounded interval. |
| `EventProjectorReliabilityOptions.cs` | Contains activation switches and bounded recovery/retry/lease settings. |
| `Contracts/EventProjectionDescriptor.cs` | Defines one immutable source type, target operation, idempotency strategy, and completion/failure factories. |
| `Contracts/IEventProjector.cs` | Exposes projector identity, descriptors, lifecycle, readiness, data-plane entry points, and infrastructure dependencies. |

The removed `EventProjectorBuilder` must not be reintroduced. It stored mutable delegates and replaced the maximum-
attempt handler during each event call, making concurrent dispatch dependent on call order. Descriptors are now created
once by the concrete projector and frozen into a type-keyed dispatch table by the base class.

## Immutable descriptor contract

Every supported source event has exactly one `EventProjectionDescriptor` containing:

- its concrete source CLR type;
- one declared `EventProjectionIdempotencyStrategy`;
- a target operation accepting `ProjectionExecutionContext`;
- a completion-event factory;
- a failure-event factory; and
- the processing-publication policy; and
- the `UseDurableReplay` delivery policy, defaulted to `true`.

Startup rejects an empty table, duplicate source types, or any mismatch between `ProjectionDescriptors` and
`ProjectedEventTypes`. A target operation returns `Applied`, `AlreadyApplied`, `Superseded`, or `Failed`. An
`Unspecified` idempotency strategy is invalid.

Concrete `Describe` helpers expose `useDurableReplay` as their last optional parameter. Existing calls remain durable:

```csharp
Describe<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent>(applyAsync);
Describe<RealtimeProjectionEvent, RealtimeProjectionCompleteEvent, RealtimeProjectionFailEvent>(
    applyAsync,
    useDurableReplay: false);
```

## Command actor adoption convention

Every command actor should eventually own a corresponding EventProjector. The projector is the command actor's
standard boundary for applying its committed domain events to read models and for publishing processing,
completion, or failure outcomes. Adoption may remain incremental while existing command actors are migrated, but new
command actors should include their projector and immutable descriptor table as part of their initial design.

This convention does not require every projection to use durable replay. Each descriptor selects its delivery lane:
durable JetStream process/replay remains the default, while explicitly best-effort projections may set the final
`Describe` parameter to `useDurableReplay: false`. A command actor with mixed projection requirements may own both
descriptor modes in the same EventProjector.

`ProjectionExecutionContext` carries the durable projector name, event ID, event-stream ID, stream version, execution token,
deterministic target-effect identity, strategy, and cancellation token. The effect identity is stable across retries;
the execution token is deliberately unique to one claim.

Projector target operations must never allocate a fresh row identity during replay. Current append-style projections
preserve a positive identity already carried by the event payload, with the persisted `EventId` used as the stable
fallback for legacy zero-valued futures tick, futures option tick, futures ITI signal, futures trade signal, and
option-trade spread payloads. Spread distribution events historically contain two zero IDs, so that projector derives
two stable negative IDs from the event ID; the positive sequence range remains reserved for ordinary non-projector
inserts. Projectors must also treat the committed event object as immutable. Any derived business state belongs in the
command workflow before event persistence, not in a post-write projector callback.

## Fund projector contract

`FundEventProjector` owns eight immutable descriptors. All use `NaturalKeyMutation`:

| Source event | Target mutation | Repeat-apply basis |
| --- | --- | --- |
| `FundCreatedEvent` | Upsert `fund` by `FundId` | Same key and complete deterministic payload. |
| `OrderAddedToFundEvent` | Upsert `fund_order` by `(FundId, OrderId)` | Same key/payload; permanent order ownership prevents identity reuse. |
| `TradeAddedToFundOrderEvent` | Upsert `fund_order_trade` by `(FundId, OrderId, TradeId)` | Same key and complete deterministic payload. |
| `OrderRemovedFromFundEvent` | Delete `fund_order` by key | A repeated delete leaves the same absent state. |
| `TradeRemovedFromFundOrderEvent` | Delete `fund_order_trade` by key | A repeated delete leaves the same absent state. |
| `FundOrderTradeStateChangedEvent` | Set state/audit fields by key | Repeating the same values leaves identical state. |
| `FundOrderClosedEvent` | Set order status to `Closed` | Repeating the same status leaves identical state. |
| `FundMaxProfitGeneratedEvent` | No target write | The no-op is intrinsically repeat safe. |

No Fund operation allocates a business ID, increments a value, appends under a new key, or calls an external target.
Therefore Tranche C does not add a ScyllaDB receipt table. A future operation with any of those behaviors must use
`TargetReceipt` or a proven conditional/commutative contract before registration.

## Startup lifecycle

`StartAsync` performs these phases in order:

1. validate and freeze descriptors;
2. start the bounded transient worker when any descriptor has `UseDurableReplay = false`;
3. when durable descriptors exist, set maximum attempts and register one stable terminal callback;
4. prepare JetStream resources without starting consumption;
5. register the single process/replay handler;
6. inventory and enqueue recovery candidates for durable descriptors only;
7. start the outbox dispatcher when independently enabled;
8. start process and replay workers; and
9. publish readiness after every required lane is ready.

Any startup failure stops partially started workers, clears the actor context, leaves readiness false, records the
failure reason, and rethrows.

When bounded recovery is enabled, recovery reads one joined event/state keyset page at a time. It excludes active
unexpired leases, groups a page by event stream, maintains ascending event ID within a stream, and processes only
independent streams concurrently. Normal events are enqueued without a recovery-time claim; the queue worker owns the
single claim path. Multiple projector instances may attempt to publish the same recovery candidate, but the durable
queue uses a stable NATS message ID so JetStream de-duplicates the publication. An event that cannot be deserialized is
claimed and terminalized with `BlockedReason = unknown-source-event` because it cannot enter typed dispatch.

## Optional non-durable execution

`EventProjectionDescriptor.UseDurableReplay` is immutable and defaults to `true`. When it is `false`, live intake
writes the event to a bounded `Channel<IEvent>` with multiple writers, one reader, and `Wait` full-mode backpressure.
No NATS Core or JetStream work-queue message is published. Processing/completion/failure actor events still use their
normal actor delivery convention. The single reader preserves local projector enqueue order and drains accepted work
during a graceful stop.

The transient worker publishes the optional processing event, resolves the committed event-stream ID without creating
projector state, invokes `ApplyAsync` once, and then publishes either completion or failure once. A processing-event
publication error is logged but does not suppress the target action. Apply exceptions and explicit `Failed` results
run the failure factory. Completion/failure conversion or publication errors are logged and dropped; none of these
failures enter replay.

The transient lane never creates legacy or fenced projector state, Blackboard state, claims, recovery candidates, or
transactional outbox rows. `RetryExactAsync` returns `false` for a non-durable descriptor. Bounded recovery filters its
source event types from the frozen descriptor table, so a mixed projector routes each event to exactly one lane.

This mode intentionally accepts loss on crash or forced shutdown, has no cross-instance ordering fence, and provides
one process-local execution attempt. It is for projections whose business contract accepts those guarantees. The
target action should remain idempotent because duplicate upstream commands or source events can still request another
execution.

## Fenced intake and execution

With `FencedExecutionEnabled = true`, live intake conditionally creates execution state before queue publication. The
insert joins `event_log` and `event_name_id` by persisted event ID, deriving `EventStreamId` and `SourceEventName`
atomically. It does not depend on the serialized event carrying `AggregateId`, and it does not add a stream lookup.

Process and replay deliveries both call the same execution engine:

1. load or conditionally create `(EventId, ProjectorName)` state;
2. short-circuit a terminal state;
3. claim with a random execution token and bounded lease;
4. perform the current stage's external operation;
5. advance only through a token/revision/stage compare-and-set; and
6. terminalize by clearing the token and lease.

The normal state path is:

```text
PublishProcessingEvent -> ApplyProjection -> PublishCompletedEvent -> Completed
                                      |
                                      +-> PublishFailedEvent -> Completed / Failed
```

`ValidateSourceEvent` and `PersistCompletion` remain accepted resume stages for compatible durable rows. Fund uses
`NeverSupersede` semantics: its handlers never return `Superseded`.

If an operation throws or a checkpoint loses its fence, the engine conditionally releases the claim at the same stage:

- token and lease are cleared;
- outcome becomes `Retrying`;
- retry count and bounded exponential next-attempt time are recorded; and
- the exception is rethrown to JetStream.

Immediate release is essential. Without it, a 30-second redelivery could exhaust its delivery count while a stale
two-minute lease still prevents every retry from claiming the state. A stale owner cannot release, transition, or
terminalize after another owner changes the token or revision.

## Same-stream execution ordering

The fenced claim is the authoritative ordering boundary. Its PostgreSQL predicate rejects an event while an earlier
`StreamVersion` for the same `(ProjectorName, EventStreamId)` remains unresolved. Completed, already-completed, and explicitly
superseded predecessors do not block; failed, cancelled, blocked, retrying, leased, or otherwise nonterminal
predecessors do. This applies identically to process and replay workers and uses the
`(ProjectorName, EventStreamId, StreamVersion)` index rather than a process-local lock.

On a rejected claim, the engine reloads durable state. An unresolved predecessor throws
`EventProjectorStreamOrderDeferredException`; another valid owner for the same event records a claim conflict and lets
the duplicate delivery acknowledge; other transient claim conditions throw `EventProjectorDeliveryDeferredException`.
The NATS process and replay workers negatively acknowledge deferred deliveries with bounded delay. Deferrals do not
consume the application processing-failure budget: the maximum-attempt callback cross-checks the durable
`RetryCount` before terminalizing. Genuine failures still terminalize at the configured maximum.

The initialization contract remains important: supported live events persist their initial projector state before
queue publication, and bounded recovery enumerates persisted state joined to its event. A missing earlier state is not
interpreted as a successful projection. Fund keeps `NeverSupersede`; an unresolved predecessor therefore blocks later
Fund events until retry succeeds or an operator deliberately skips it with a recorded reason.

The per-projector/per-stream checkpoint records target application, not terminal message publication. It suppresses
only missing or pre-apply work already covered by the same/newer target version. A state in `PublishCompletedEvent`,
`PublishFailedEvent`, or `PersistCompletion` is never reconciled away by its checkpoint: it resumes after lease
takeover and continues blocking later versions until its terminal workflow is durably staged.

## Transactional publication outbox

With `TransactionalOutboxEnabled = true`, the engine serializes processing, completion, and failure publications and
executes one PostgreSQL statement whose data-modifying CTE both changes projector state and inserts the outbox row.
The primary key `(ProjectorName, EventId, EffectKind)` and deterministic `MessageId` make every retry reuse one durable
publication identity.

The dispatcher claims at most `OutboxBatchSize` eligible rows with `FOR UPDATE SKIP LOCKED`, a unique dispatch token,
and a bounded lease. It publishes in created/event/effect order, then conditionally marks the row `Published`. A send
failure releases the row as `Retrying` with capped exponential backoff. A process crash or lost acknowledgement leaves
the row reclaimable after lease expiry; the same MessagePack payload and deterministic `IEvent.Id` are published again.
After `MaximumOutboxAttempts`, status becomes `Failed` rather than retrying silently forever.

This is durable at-least-once publication, not exactly once. Current Fund completion/failure event types have no
registered business-effect consumers in the Fund event actor. Future consumers must persist or naturally absorb the
deterministic `IEvent.Id`; finite JetStream duplicate windows are not a permanent business receipt.

## Unknown and terminal behavior

An unregistered runtime type is never treated as successful. If durable execution state exists, it is terminalized as
`Failed` with `BlockedReason = unregistered-source-event`. Failed failure-event conversion uses
`failed-event-conversion`. Maximum delivery exhaustion converts through the immutable descriptor and atomically stages
its typed failure event with `maximum-attempts-reached`. A null or throwing terminal conversion records manual
resolution without an outbox row. These states preserve the source event/state for later operator resolution.

Terminal failures persist `BlockedStage`, so `RetryExactAsync` can reopen the exact failed stage, reload the immutable
source event, and durably re-enqueue it. `SkipAsync` requires a reason and records `operator-skip:<reason>`.
`GetOperationalStatesAsync` provides bounded keyset pages for pending, failed, or blocked states.

A state is terminal when its stage is `Completed` or its outcome is `Completed`, `Failed`, `Cancelled`, `Superseded`,
or `AlreadyCompleted`.

## Compatibility path and activation

When `FencedExecutionEnabled` is false, immutable descriptors still replace the mutable builder, but the base class
updates the legacy `EventProjectorStateReadModel` through the existing upsert/cache flow. This permits descriptor
deployment without mixing legacy and fenced workers.

Current application settings:

```json
{
  "EventProjectorReliability": {
    "BoundedRecoveryEnabled": true,
    "FencedExecutionEnabled": true,
    "TransactionalOutboxEnabled": true,
    "BacklogMetricsPollingEnabled": true,
    "MetricsPollingInterval": "00:00:05",
    "NonDurableQueueCapacity": 8192
  }
}
```

`MetricsPollingInterval` must be between one second and five minutes. `NonDurableQueueCapacity` must be between one
and 1,048,576. Polling is an independent operational switch; the event counters and histograms are available whenever
the host OTLP meter pipeline is enabled.

## OpenTelemetry and Grafana contract

The shared telemetry pipeline registers meter `TomasAI.IFM.Application.EventProjector`. The canonical instruments are:

| Instrument | Type | Purpose |
| --- | --- | --- |
| `ifm.event_projector.events` | Counter | Outcomes such as accepted, claimed, applied, already-applied, retried, superseded, blocked, terminal-failed, completed, recovery queued/conflict, and outbox published/retried. |
| `ifm.event_projector.stage.duration` | Histogram, ms | Stage duration by projector, bounded stage, and outcome. |
| `ifm.event_projector.recovery.batch.duration` | Histogram, ms | One bounded recovery-page duration. |
| `ifm.event_projector.recovery.batch.size` | Histogram, events | Events discovered in one recovery page. |
| `ifm.event_projector.startup.duration` | Histogram, ms | Projector startup/recovery-to-readiness duration and outcome. |
| `ifm.event_projector.outbox.publish.duration` | Histogram, ms | Outbox publication attempt duration and outcome. |
| `ifm.event_projector.backlog.pending` | Gauge, events | Durable nonterminal state count. |
| `ifm.event_projector.backlog.oldest.age` | Gauge, seconds | Age of the oldest durable nonterminal state. |
| `ifm.event_projector.backlog.blocked` | Gauge, events | Durable states requiring resolution. |
| `ifm.event_projector.backlog.terminal_failed` | Gauge, events | Terminal failed states. |
| `ifm.event_projector.lease.expired` | Gauge, leases | Expired execution leases awaiting recovery. |
| `ifm.event_projector.outbox.pending` | Gauge, messages | Unpublished outbox rows. |
| `ifm.event_projector.outbox.oldest.age` | Gauge, seconds | Age of the oldest unpublished outbox row. |
| `ifm.event_projector.outbox.retrying` | Gauge, messages | Outbox rows currently retrying. |
| `ifm.event_projector.worker.busy` | Gauge, workers | Projector logical workers currently executing. |
| `ifm.event_projector.worker.utilization` | Gauge, percent | Busy logical workers divided by registered capacity. |
| `ifm.event_projector.ready` | Gauge | One when the projector is ready, otherwise zero. |

Only the bounded `projector`, `stage`, `outcome`, and `operation` dimensions are emitted. IDs and exception text stay in
structured logs/traces. A Grafana dashboard should graph event rates by outcome; p50/p95/p99 stage/startup/outbox
histograms; pending/blocked/failed counts and oldest ages; expired leases and claim conflicts; worker utilization and
readiness; and NATS consumer lag/redelivery beside the PostgreSQL backlog.

For Prometheus-compatible backends, example queries after the backend's normal dot-to-underscore translation are:

```promql
rate(ifm_event_projector_events_total{outcome="terminal-failed"}[5m])
histogram_quantile(0.99, sum by (le, projector, stage) (rate(ifm_event_projector_stage_duration_milliseconds_bucket[5m])))
max by (projector) (ifm_event_projector_backlog_oldest_age_seconds)
```

Exporter/backend suffix rules vary, so validate the actual translated names before importing a dashboard. Alerts
should fire when readiness is zero after startup, blocked or terminal-failed counts are nonzero, backlog/outbox oldest
age exceeds the operating objective, expired leases repeat, or retry/claim-conflict rates remain elevated. Warning and
critical age thresholds must be selected from paper-trading service-level objectives, not hard-coded from synthetic
benchmarks.

## Staged activation and rollback

1. Deploy the additive schema, meter, and queue compatibility with all reliability/polling switches off.
2. Enable the host OTLP exporter and then `BacklogMetricsPollingEnabled` on one instance; reconcile gauges with bounded
   operator queries and establish normal stage, backlog, retry, and worker-utilization ranges.
3. Close projector readiness, drain legacy workers, and enable `BoundedRecoveryEnabled` plus
   `FencedExecutionEnabled` on one canary. Never run legacy and fenced workers for the same projector identity.
4. Verify PostgreSQL state, Fund ScyllaDB projections, process/replay lag, same-stream deferrals, expired leases,
   blocked rows, and readiness before expanding.
5. Drain the canary again and enable `TransactionalOutboxEnabled` separately. Verify pending/oldest/retrying gauges and
   deterministic completion/failure delivery before expanding.
6. To roll back, close readiness/intake, stop and drain projector workers, verify no active leases, restore the prior
   switch set or binary, and preserve all additive state/outbox rows and queue resources for reconciliation.

Paper-trading on the intended PostgreSQL/ScyllaDB/NATS/OTLP topology remains the production activation gate.

## Tranche E verification

The verification gate covers:

- real PostgreSQL same-stream claim blocking and operational snapshot classification;
- a live process/replay Fund flow where the later event remains deferred until its predecessor completes;
- NATS deferral redelivery without consuming the replay application-failure budget;
- meter/outcome and polling-option contract tests;
- recovery, stage-instrumentation, and outbox CPU/allocation benchmarks;
- the complete NATS unit suite (58/58), Fund unit suite (208/208), and Fund integration suite (29/29);
- all ten domain integration projects sequentially in Release (196/196); and
- the complete solution Release build with zero warnings and zero errors.

Relevant suites are:

- `TomasAI.IFM.Domain.Fund.UnitTests/FundEventProjectorTests.cs`;
- `TomasAI.IFM.Domain.Fund.UnitTests/EventProjectorRecoveryCoordinatorTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventSourceActorSnapshotRangeTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventProjectorStatePersistenceTests.cs`; and
- `TomasAI.IFM.Domain.Fund.IntegrationTests/FundEventProjectionIntegrationTests.cs`.

## Remaining operational gate

SWO-06 implementation Tranches A-E are complete. Activation remains explicitly separate: collect observe-only data,
run the canary sequence above under paper-trading load, set topology-specific objectives/alerts, exercise rollback, and
approve each independent switch. No production activation is implied by Tranche E completion.
