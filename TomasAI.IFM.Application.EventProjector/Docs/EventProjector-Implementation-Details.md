# Event Projector Implementation Details

## Purpose and current status

`TomasAI.IFM.Application.EventProjector` projects committed event-sourced domain events into target read stores through
a durable NATS JetStream queue. SWO-06 Tranches A-C are implemented:

- additive PostgreSQL execution state and compare-and-set fencing;
- bounded, joined-keyset startup recovery;
- immutable projector descriptors;
- one claim/stage path for process and replay delivery;
- explicit target idempotency contracts; and
- fail-closed handling for unregistered and unknown source events.

The transactional publication outbox is reserved for Tranche D. Both `BoundedRecoveryEnabled` and
`FencedExecutionEnabled` remain `false` in production configuration until the rollout gate is approved.

## Source map

| File | Responsibility |
| --- | --- |
| `BaseEventProjector.cs` | Validates descriptors, owns lifecycle and readiness, initializes durable intake, selects legacy or fenced execution, and publishes typed actor events. |
| `EventProjectorExecutionEngine.cs` | Claims a leased execution, applies fenced stage transitions, releases failed claims for retry, terminalizes failures, and creates explicit target-operation contexts. |
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
6. start process and replay workers; and
7. publish readiness.

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

## Unknown and terminal behavior

An unregistered runtime type is never treated as successful. If durable execution state exists, it is terminalized as
`Failed` with `BlockedReason = unregistered-source-event`. Failed failure-event conversion uses
`failed-event-conversion`. Maximum delivery exhaustion uses `maximum-attempts-reached`. These states preserve the source
event/state for later operator resolution.

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
    "FencedExecutionEnabled": false
  }
}
```

Activation requires draining projector workers, enabling bounded recovery and fenced execution on one canary, and
verifying PostgreSQL state, ScyllaDB Fund rows, JetStream lag/redelivery, blocked rows, and claim conflicts before
expanding. Never run legacy and fenced workers concurrently for the same persisted projector identity.

## Tranche C verification

The verification gate covers:

- descriptor uniqueness and explicit strategy for all eight Fund events;
- crash after target write but before checkpoint, followed by safe repeat application;
- unregistered-event manual-resolution terminalization;
- conditional release and immediate takeover by a new owner;
- stale token/revision rejection;
- repeat application of all eight operations against real ScyllaDB;
- fenced live projection through real PostgreSQL, NATS JetStream, and ScyllaDB; and
- legacy projection/recovery compatibility while activation flags remain off.

Relevant suites are:

- `TomasAI.IFM.Domain.Fund.UnitTests/FundEventProjectorTests.cs`;
- `TomasAI.IFM.Domain.Fund.UnitTests/EventProjectorRecoveryCoordinatorTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventSourceActorSnapshotRangeTests.cs`;
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventProjectorStatePersistenceTests.cs`; and
- `TomasAI.IFM.Domain.Fund.IntegrationTests/FundEventProjectionIntegrationTests.cs`.

## Next tranche

Tranche D adds atomic PostgreSQL state-plus-outbox persistence, bounded outbox dispatch, deterministic publication IDs,
typed terminal failure publication, and operator retry/skip queries. Until that tranche is complete, processing and
completion/failure publications remain at-least-once and their consumers must remain idempotent.
