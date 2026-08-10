# Event Projector Implementation Details

## Purpose and current status

`TomasAI.IFM.Application.EventProjector` projects committed event-sourced domain events into target read stores through
a durable NATS JetStream queue. SWO-06 Tranches A-D are implemented:

- additive PostgreSQL execution state and compare-and-set fencing;
- bounded, joined-keyset startup recovery;
- immutable projector descriptors;
- one claim/stage path for process and replay delivery;
- explicit target idempotency contracts; and
- fail-closed handling for unregistered and unknown source events;
- atomic PostgreSQL publication outbox transitions and leased bounded dispatch;
- typed maximum-attempt failure publication; and
- bounded operator pages plus retry-exact and skip-with-reason actions.

`BoundedRecoveryEnabled`, `FencedExecutionEnabled`, and `TransactionalOutboxEnabled` remain `false` in production
configuration until the rollout gate is approved.

## Source map

| File | Responsibility |
| --- | --- |
| `BaseEventProjector.cs` | Validates descriptors, owns lifecycle and readiness, initializes durable intake, selects legacy or fenced execution, and publishes typed actor events. |
| `EventProjectorExecutionEngine.cs` | Claims a leased execution, applies fenced stage transitions, releases failed claims for retry, terminalizes failures, and creates explicit target-operation contexts. |
| `EventProjectorOutboxDispatcher.cs` | Claims bounded outbox batches with `SKIP LOCKED`, publishes typed events, and records delivery or bounded retry. |
| `EventProjectorOutboxSerializer.cs` | Serializes concrete MessagePack payloads and assigns deterministic consumer-visible event IDs from stage-effect identities. |
| `EventProjectorRecoveryCoordinator.cs` | Pages recoverable event/state rows, preserves same-stream ordering, and enqueues stable recovery candidates with bounded cross-stream concurrency. |
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
- the processing-publication policy.

Startup rejects an empty table, duplicate source types, or any mismatch between `ProjectionDescriptors` and
`ProjectedEventTypes`. A target operation returns `Applied`, `AlreadyApplied`, `Superseded`, or `Failed`. An
`Unspecified` idempotency strategy is invalid.

`ProjectionExecutionContext` carries the durable projector name, event ID, event-stream ID, execution token,
deterministic target-effect identity, strategy, and cancellation token. The effect identity is stable across retries;
the execution token is deliberately unique to one claim.

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
2. set the queue maximum attempts and register one stable terminal callback;
3. prepare JetStream resources without starting consumption;
4. register the single process/replay handler;
5. inventory and enqueue recovery candidates;
6. start the outbox dispatcher when independently enabled;
7. start process and replay workers; and
8. publish readiness.

Any startup failure stops partially started workers, clears the actor context, leaves readiness false, records the
failure reason, and rethrows.

When bounded recovery is enabled, recovery reads one joined event/state keyset page at a time. It excludes active
unexpired leases, groups a page by event stream, maintains ascending event ID within a stream, and processes only
independent streams concurrently. Normal events are enqueued without a recovery-time claim; the queue worker owns the
single claim path. Multiple projector instances may attempt to publish the same recovery candidate, but the durable
queue uses a stable NATS message ID so JetStream de-duplicates the publication. An event that cannot be deserialized is
claimed and terminalized with `BlockedReason = unknown-source-event` because it cannot enter typed dispatch.

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
    "BoundedRecoveryEnabled": false,
    "FencedExecutionEnabled": false,
    "TransactionalOutboxEnabled": false
  }
}
```

Activation requires draining projector workers, enabling bounded recovery and fenced execution on one canary, and
verifying PostgreSQL state, ScyllaDB Fund rows, JetStream lag/redelivery, blocked rows, and claim conflicts before
expanding. Never run legacy and fenced workers concurrently for the same persisted projector identity.

## Tranche D verification

The verification gate covers:

- atomic state/outbox persistence and leased competing claims against real PostgreSQL;
- publish failure followed by retry with the identical payload/event identity;
- publish success followed by a lost delivery marker and safe identical re-publication;
- stable typed completion and maximum-attempt failure conversion;
- bounded pending/failed/blocked pages, exact-stage retry, and skip-with-reason state;
- real PostgreSQL, NATS JetStream queue, ScyllaDB target, and outbox completion delivery;
- all eight Fund target idempotency contracts from Tranche C; and
- legacy/fenced/outbox configuration compatibility while all activation flags remain off.

Relevant suites are:

- `TomasAI.IFM.Domain.Fund.UnitTests/FundEventProjectorTests.cs`;
- `TomasAI.IFM.Domain.Fund.UnitTests/EventProjectorRecoveryCoordinatorTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventSourceActorSnapshotRangeTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventProjectorStatePersistenceTests.cs`; and
- `TomasAI.IFM.Domain.Fund.IntegrationTests/FundEventProjectionIntegrationTests.cs`.

## Next tranche

Tranche E adds cross-page same-stream execution ordering, projector OpenTelemetry instruments and Grafana guidance,
recovery/steady-state/outbox benchmarks, and the staged canary rollout procedure. No production activation is implied
by Tranche D completion.
