# Actor Event Streaming and Paged Query Contracts

**Document type:** System-wide messaging and query design specification  
**Status:** Proposed design; no implementation is authorized or implied by this document  
**Created:** 2026-08-13  
**Last updated:** 2026-08-13  
**Applies to:** Actors, WinForms and future WPF clients, console applications, hosted services, Docker services, projectors, gateways, and other non-actor components

## 1. Purpose

This document defines the future system-wide contracts for:

- consuming Core NATS and NATS JetStream actor events through `IAsyncEnumerable`;
- retaining the current callback-handler event-listener model;
- preserving Core NATS and JetStream delivery guarantees across both APIs;
- routing subscriptions from `ActorType` rather than a caller-selected transport flag;
- presenting high-rate realtime state in UI and console grids without overwhelming the UI thread;
- reconciling non-durable realtime changes with queryable projection snapshots;
- paging static or slowly changing projection queries; and
- sharing the same contracts across desktop clients, console processes, and Docker-hosted services.

This is a design document only. It records contracts and constraints so they can be implemented consistently during later messaging, query, and UI refactoring. It does not change the existing listeners, query APIs, WinForms UI, or Docker services.

Implementation is deferred until Milestone A, legacy operational restoration, is accepted under the [`IFM Operational Restoration and Trading Capability Roadmap`](IFM-Operational-Restoration-and-Trading-Capability-Roadmap.md). The existing callback listener contracts remain active during restoration. This design must not be introduced merely to complete the current WinForms readiness gates.

This document must be read with [Actor Message Types and Delivery Conventions](Actor-Message-Types-and-Delivery-Conventions.md). That document remains authoritative for message meaning and transport selection. In particular:

> Message semantics select the `ActorType`, and the `ActorType` selects exactly one transport.

## 2. Architectural decisions

The following decisions are normative for the future implementation.

1. Core NATS and JetStream listeners will support both callback-handler and pull-based `IAsyncEnumerable` consumption.
2. Both consumption styles will use one internal transport-specific subscription engine. They must not contain duplicate NATS subscription implementations.
3. One subscription selects exactly one consumption frontend. A delivery must not be sent to both a callback and an enumerable consumer.
4. The public streaming contract will expose a transport-neutral event envelope, not a raw `NatsMsg<byte[]>`.
5. JetStream acknowledgement will remain tied to successful consumer processing. Merely yielding or buffering a delivery is not automatically successful processing.
6. An event-delivery lease will expose transport-neutral completion and abandonment operations. Completion is a no-op at the broker level for Core NATS and an ACK for JetStream.
7. Actor type determines the concrete listener through the existing actor delivery mapping. Callers will not pass `useJetStream`, `durable`, or a similar transport-selection flag.
8. Every subscription consumes one actor type only, consistent with the `[ActorType].>` consumer convention.
9. Bounded `Channel<T>` storage will remain the buffering and backpressure boundary. `IAsyncEnumerable` is a consumption surface, not a replacement for bounded admission control.
10. Event subscriptions and paged queries are separate contracts. Live event delivery must not be used as a paging protocol.
11. Projection paging uses explicit page results as its primary contract. `IAsyncEnumerable<T>` may be offered as a convenience that repeatedly requests pages.
12. High-rate UI streams update background-owned state first and cross the UI dispatcher only at a controlled render cadence.

## 3. Scope and non-goals

### 3.1 In scope

- Core NATS `Notify` and externally observed `Realtime` streams;
- JetStream `Event` streams used by durable workflow participants;
- handler and enumerable listener APIs;
- typed and untyped event envelopes;
- payload ownership and delivery acknowledgement;
- bounded capacity, ordering, concurrency, shutdown, retry, and metrics;
- snapshot-plus-live synchronization for realtime grids;
- cursor-based projection paging;
- WinForms, WPF, console, and service integration guidance; and
- unit, integration, performance, and system-test requirements.

### 3.2 Not in scope

- changing the existing `ActorType` values or transport mapping;
- adding a message-level durability flag;
- dual-writing one message to Core NATS and JetStream;
- allowing one consumer to combine actor types;
- changing NATS credentials or authorization policy;
- implementing CommunityToolkit.Mvvm, R3, WPF, or grid controls;
- selecting a specific grid vendor;
- changing domain event contracts before their individual workflows are reviewed; or
- authorizing implementation from this design alone.

## 4. Current implementation baseline

The existing event-listener contract exposes lifecycle state, message count, `StartAsync`, and `StopAsync`. `StartAsync` receives a callback of this general form:

```csharp
Func<string, NatsMsg<byte[]>, ValueTask>
```

The current implementations already establish important behavior that must be retained:

- `NatsActorEventListener` consumes Core NATS subscriptions through the NATS client's bounded subscription channel.
- `NatsJetStreamEventListener` uses explicit acknowledgements, bounded dispatcher channels, configurable concurrency, negative acknowledgement, and redelivery.
- the JetStream listener acknowledges only after its configured handler succeeds;
- handler failure causes a negative acknowledgement or eventual ACK-timeout redelivery; and
- listener cancellation and stop behavior are explicitly owned.

The enumerable design extends these behaviors. It does not define a less reliable alternate implementation.

## 5. Terminology

| Term | Meaning |
| --- | --- |
| Event source | Factory or facade that creates an actor-type-specific event subscription |
| Subscription | One owned Core NATS or JetStream consumer lifecycle |
| Delivery | One received message plus its completion/abandonment state |
| Envelope | Transport-neutral event metadata and payload |
| Completion | Consumer declaration that processing succeeded |
| Abandonment | Consumer declaration that processing failed or should be retried |
| Local admission | Successful insertion into a process-owned bounded queue; not necessarily completed business processing |
| Snapshot | Query result representing current projection state at a known revision |
| Delta | Live event representing a change after or around a snapshot revision |
| Render cadence | Maximum frequency at which accumulated display state crosses to the UI thread |
| Page cursor | Opaque token identifying the stable continuation point for a projection query |

## 6. Proposed event-streaming contracts

The names below are design names and may be refined during implementation, but their responsibilities must be preserved.

### 6.1 Subscription factory

```csharp
public interface IActorEventSubscriptionFactory
{
    ValueTask<IActorEventSubscription> SubscribeAsync(
        string listenerId,
        Dictionary<ActorMailboxId, List<string>> eventMap,
        ActorEventSubscriptionOptions options,
        CancellationToken cancellationToken = default);
}
```

The factory must:

- validate that the event map is non-empty;
- validate that every mailbox has the same `ActorType`;
- derive the transport from `ActorType.GetDeliveryType()`;
- reject `Unknown`, `Command`, and `Query` for publish/subscribe event streaming;
- select the Core listener for `Notify` and `Realtime`;
- select the JetStream listener for `Event`;
- enforce a distinct listener identity where the transport requires it; and
- return an owned subscription that must be disposed.

No overload may allow a caller to override the mapped transport.

### 6.2 Owned subscription

```csharp
public interface IActorEventSubscription : IAsyncDisposable
{
    EventListenerState State { get; }

    ActorEventSubscriptionMetrics Metrics { get; }

    IAsyncEnumerable<IActorEventDelivery> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
```

A subscription represents one active consumption lifecycle. Its rules are:

- `ReadAllAsync` may be enumerated once only;
- the subscription owns its connection reference, NATS consumer, local buffers, cancellation source, and receive tasks;
- multiple screens or services must not enumerate one subscription concurrently;
- independent consumers use independent subscriptions;
- in-process fanout, if required, occurs after one owned subscription and must have its own explicit loss/backpressure rules; and
- disposing the subscription terminates intake and applies the configured stop behavior.

### 6.3 Delivery lease

```csharp
public interface IActorEventDelivery : IAsyncDisposable
{
    ActorEventEnvelope Event { get; }

    ActorEventDeliveryState State { get; }

    ValueTask CompleteAsync(
        CancellationToken cancellationToken = default);

    ValueTask AbandonAsync(
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default);
}
```

The delivery is a lease over a payload and, for JetStream, its broker acknowledgement responsibility.

Rules:

- completion and abandonment are mutually exclusive and idempotent;
- completing or abandoning an already terminal delivery performs no second broker operation;
- disposing an incomplete Core delivery releases its local payload;
- disposing an incomplete JetStream delivery abandons it according to the configured retry policy;
- payload memory remains valid until the delivery reaches a terminal state or is disposed;
- consumer code must not retain the payload after disposal unless it first creates an owned copy or deserializes it; and
- implementation code must never return pooled memory while a delivery still owns it.

The normal durable consumption pattern is:

```csharp
await foreach (var delivery in subscription.ReadAllAsync(cancellationToken))
{
    await using (delivery)
    {
        await ProcessAsync(delivery.Event, cancellationToken);
        await delivery.CompleteAsync(cancellationToken);
    }
}
```

If `ProcessAsync` throws, asynchronous disposal abandons the incomplete delivery. A JetStream implementation then negatively acknowledges it or leaves it unacknowledged for ACK-timeout redelivery, according to policy.

### 6.4 Event envelope

```csharp
public sealed record ActorEventEnvelope(
    ActorMailboxId MailboxId,
    string Verb,
    ActorSubject Subject,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset ReceivedAt,
    ActorDeliveryType DeliveryType,
    ulong? StreamSequence,
    int DeliveryAttempt,
    string? EventId,
    string? CorrelationId,
    string? CausationId);
```

The envelope separates application consumption from NATS client types. Fields unavailable on Core NATS, such as stream sequence and delivery attempt, remain null or use their documented neutral value.

The base contract remains untyped because one actor mailbox subscription may accept several verbs with different event types. Typed adapters should be supplied for common one-event and event-family cases:

```csharp
public interface IActorEventDelivery<out TEvent> : IAsyncDisposable
{
    TEvent Event { get; }
    ActorEventEnvelope Envelope { get; }
    ActorEventDeliveryState State { get; }
    ValueTask CompleteAsync(CancellationToken cancellationToken = default);
    ValueTask AbandonAsync(
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default);
}
```

Deserialization failure is a processing failure. Durable poison-message handling must respect `MaxDeliver`, diagnostics, and the separately approved dead-letter or terminal-failure policy. It must not silently ACK malformed workflow events.

## 7. Callback compatibility contract

The current callback API remains supported during migration. It should eventually be implemented as an adapter over the same subscription and delivery engine:

```csharp
await foreach (var delivery in subscription.ReadAllAsync(cancellationToken))
{
    await using (delivery)
    {
        await handler(delivery.Event);
        await delivery.CompleteAsync(cancellationToken);
    }
}
```

This establishes one definition of successful handling for both APIs.

Compatibility requirements:

- existing consumers do not need to migrate in one release;
- callback and enumerable consumers receive equivalent filtering and delivery guarantees;
- metrics use the same names and meanings;
- callback success completes the delivery;
- callback failure abandons the delivery;
- callback cancellation during shutdown is distinguished from processing failure; and
- raw `NatsMsg<byte[]>` compatibility may remain temporarily, but new system-wide consumers should use the transport-neutral envelope.

## 8. Delivery and acknowledgement semantics

| Concern | Core NATS | JetStream |
| --- | --- | --- |
| Assigned actor types | `Notify`, `Realtime` | `Event` |
| Durability | None | Durable stream and consumer |
| Offline replay | No | Yes, subject to stream and consumer policy |
| Broker acknowledgement | None | Explicit ACK/NAK |
| `CompleteAsync` | Releases local ownership | ACKs after successful processing |
| `AbandonAsync` | Releases local ownership; delivery is lost | NAKs or leaves unacknowledged for redelivery |
| Duplicate delivery | Possible at application boundaries; no broker guarantee | Expected under at-least-once delivery |
| Consumer requirement | Loss-tolerant observer | Idempotent durable participant |

### 8.1 What constitutes successful processing

Success depends on consumer role:

- a realtime UI observer may complete after the event has been admitted into its owned, bounded presentation-state processor;
- a console observer may complete after the log or metric has been accepted by its owned output pipeline;
- a projector completes only after the projection mutation or idempotent no-op is durable;
- a service workflow participant completes only after its required local state transition is safely committed; and
- posting an action to a UI dispatcher without observing its admission or completion is not sufficient when the consumer contract requires processing completion.

Rendering a durable event on screen must never be a prerequisite for an actor workflow to make progress. A business workflow must not depend on a desktop process being open.

### 8.2 Delivery state machine

```text
Received
   |
   v
Leased ---- CompleteAsync ----> Completed
   |
   +------ AbandonAsync ------> Abandoned
   |
   +------ Dispose incomplete -> Abandoned/Released
```

Only `Completed`, `Abandoned`, or released Core deliveries may relinquish payload ownership.

## 9. Buffering, backpressure, and overload

`IAsyncEnumerable` is pull-oriented from the consumer's perspective, but NATS continues to deliver independently. Every implementation therefore requires bounded admission between NATS and the consumer.

```csharp
public sealed record ActorEventSubscriptionOptions
{
    public int Capacity { get; init; } = 256;
    public ActorEventOrdering Ordering { get; init; }
        = ActorEventOrdering.Partition;
    public ActorEventStopMode StopMode { get; init; }
        = ActorEventStopMode.Graceful;
    public TimeSpan ShutdownTimeout { get; init; }
        = TimeSpan.FromSeconds(30);
}
```

Normative rules:

- capacity must be finite and validated;
- Core NATS backpressure waits at the bounded local boundary; if the connection or library cannot retain delivery, loss remains permitted by the Core contract;
- JetStream must stop admitting or pulling when capacity is full and must keep outstanding delivery within `MaxAckPending`;
- no durable `Event` path may use drop-oldest, drop-newest, latest-value, or silent loss;
- `Notify` and externally observed `Realtime` may use a downstream latest-value or keyed-latest presentation channel when intermediate states have no lasting meaning;
- loss policy belongs to the consumer's semantic processing stage, not to an unbounded enumerable queue; and
- overload, coalescing, retry, and pending counts must be observable.

The shared `LatestValueAsyncChannel<T>`, `KeyedLatestValueAsyncChannel<TKey,T>`, and `OrderedBatchAsyncChannel<T>` remain valid downstream processors. R3 may compose or rate-limit presentation state after bounded admission, but it does not replace these backpressure guarantees.

## 10. Ordering and concurrency

The contract must state the ordering guarantee explicitly.

| Mode | Meaning | Typical use |
| --- | --- | --- |
| Sequential | One delivery at a time in receive order | Low-volume durable consumers, strict single stream |
| Partition | Ordering preserved per actor thread/entity partition; partitions may interleave | Actors, strategy monitors, keyed market state |
| Unordered | Bounded concurrent delivery with no processing-order guarantee | Independent observational telemetry |

Rules:

- the default is partition ordering using the existing subject/thread identity;
- a single `await foreach` consumer is sequential unless it explicitly starts concurrent work;
- concurrency must be bounded and owned;
- increasing JetStream dispatcher count must not be represented as global ordering;
- completion order may differ across independent partitions;
- a consumer requiring global ordering must select sequential mode and accept its throughput limit; and
- UI grids normally preserve revision order per row key rather than imposing global order across all rows.

## 11. Lifecycle and shutdown

Enumeration cancellation and subscription disposal are distinct but coordinated operations.

### 11.1 Graceful stop

1. Stop new intake or cancel the active NATS pull/subscription.
2. Complete the local channel writer.
3. Allow already admitted deliveries to reach their configured terminal outcome.
4. ACK completed JetStream deliveries.
5. Abandon incomplete JetStream deliveries.
6. Release all payload owners.
7. Dispose consumer and connection references owned by the subscription.
8. Mark the subscription stopped.

### 11.2 Immediate stop

Immediate stop is explicitly lossy for Core NATS. For JetStream it stops intake and abandons incomplete work so it remains eligible for redelivery. It must be separately named and must not be the default normal shutdown path.

### 11.3 Consumer responsibilities

- retain and observe the enumeration task;
- pass a lifetime cancellation token;
- dispose subscriptions on screen, command, or host shutdown;
- do not start detached `await foreach` loops;
- report terminal loop failure; and
- ensure repeated start/stop cycles do not leak subscriptions, consumers, channels, tasks, or payloads.

## 12. Realtime strategy and monitor grids

A strategy/monitor grid is a primary use case for the enumerable Core NATS listener. The UI or console is an external observer of `Realtime` messages; it does not become a realtime actor and no workflow depends on it receiving every sample.

### 12.1 Processing pipeline

```text
Core NATS Realtime subscription
              |
              v
bounded event-delivery channel
              |
              v
background deserialization and validation
              |
              v
state store keyed by StrategyMonitorId
              |
              v
per-key revision check and coalescing
              |
              v
controlled render cadence (for example 20-30 Hz)
              |
              v
IUiDispatcher
              |
              v
virtualized WinForms/WPF grid
```

### 12.2 UI-thread rules

- do not dispatch one UI operation per incoming event;
- deserialize, validate, calculate, sort, filter, and merge off the UI thread where safe;
- keep background-owned mutable state isolated from UI-bound collections;
- cross the dispatcher with immutable snapshots or bounded row-change batches;
- coalesce multiple changes for the same row before rendering;
- remove unchanged values before dispatch;
- cap render cadence independently from feed cadence;
- use grid row/column virtualization where supported;
- cancel superseded sorts, filters, searches, and page requests; and
- measure dispatcher queue delay and render duration.

CommunityToolkit.Mvvm may expose the final observable properties and commands. R3 may compose derived presentation state, apply `DistinctUntilChanged`, and control render cadence. Neither toolkit changes the NATS delivery or bounded-channel requirements in this document.

### 12.3 Event contract requirements

Realtime grid deltas should contain or identify:

- stable row/entity identifier;
- entity revision or monotonically increasing sequence;
- event time and, when different, observation time;
- change type, including removal/closure where applicable;
- correlation identifiers useful for diagnostics; and
- enough state to update the row deterministically, or a clear rule requiring a projection refresh.

Older or duplicate revisions for a row are ignored. A detected revision gap triggers a projection refresh rather than an attempt to reconstruct authoritative state from non-durable Core traffic.

## 13. Snapshot-plus-live synchronization

Core NATS cannot replay messages missed while a UI, console, or service observer is offline. Realtime displays therefore use projection state as the source of truth and live messages as deltas.

The safe startup sequence is:

1. Create the realtime subscription and begin buffering bounded deltas.
2. Issue a query for the current projection snapshot.
3. Receive the snapshot with its projection revision or watermark.
4. Replace the local grid state with the snapshot.
5. Apply buffered deltas newer than the snapshot revision.
6. Continue applying live deltas.
7. Refresh the snapshot on detected gaps, buffer overflow, reconnect, or invalid revision.

Subscribing only after the snapshot query creates a race in which changes can be missed. Querying only after beginning live mutation can allow an older snapshot to overwrite newer rows. Revision-aware reconciliation is therefore required.

If the projection cannot provide a suitable revision, the initial implementation must document a weaker consistency model and prefer a final refresh after subscription startup. It must not claim gap-free synchronization.

## 14. Paged projection query contract

Static and slowly changing grids use query request/reply, not event-listener paging.

### 14.1 Page request and result

```csharp
public sealed record PageRequest(
    int PageSize,
    string? ContinuationToken,
    IReadOnlyList<QuerySort> Sort,
    QueryFilter? Filter);

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    string? NextContinuationToken,
    string? PreviousContinuationToken,
    bool HasMore,
    long? TotalCount,
    long ProjectionRevision);
```

`TotalCount` is optional because an exact count may be materially more expensive than retrieving a page. A UI must not require an exact count unless the use case justifies its cost.

### 14.2 Query APIs

```csharp
public interface IPagedQuery<T>
{
    ValueTask<PageResult<T>> QueryPageAsync(
        PageRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> EnumerateAsync(
        PageRequest initialRequest,
        CancellationToken cancellationToken = default);
}
```

`QueryPageAsync` is the primary grid and service API. `EnumerateAsync` is a convenience that repeatedly calls the paged query and is intended for sequential export, reports, diagnostics, and background processing. It is not the wire-level response shape and should not force a grid to load all rows.

### 14.3 Cursor rules

- continuation tokens are opaque to clients;
- tokens include or protect the stable sort continuation key;
- page size is bounded server-side regardless of the requested value;
- sort order has a deterministic unique tie-breaker, normally the entity identifier;
- filters and sort criteria are validated and restricted to supported projection indexes;
- token use with incompatible filters or sort order fails explicitly;
- token format may be versioned;
- sensitive data must not be exposed in a plaintext token;
- expired or invalid tokens return a typed query failure; and
- services should prefer keyset/cursor paging over large offset scans.

A typical stable order is `(UpdatedAt, Id)` or another projection-specific indexed key plus a unique identifier.

### 14.4 Changing data during paging

The query contract must declare one of these consistency policies:

- snapshot revision: all pages belong to one retained projection revision;
- cursor consistency: each page continues after its last stable key while accepting concurrent changes; or
- best effort: pages may reflect current state independently.

The selected policy must be visible in `ProjectionRevision` and documented per query. Trading or operational screens must not assume snapshot consistency unless the backend provides it.

## 15. UI and service usage matrix

| Use case | Primary contract | Transport | Local processing |
| --- | --- | --- | --- |
| Live strategy/monitor grid | Event subscription `ReadAllAsync` | Core `Realtime` | Keyed latest state and controlled render cadence |
| Best-effort status console | Event subscription or callback | Core `Notify` | Ordered or batched display queue according to log semantics |
| Durable service workflow | Event subscription or callback | JetStream `Event` | Idempotent processing, complete only after durable success |
| Projector | Event subscription or callback | JetStream `Event` | Ordered/idempotent projection mutation |
| Initial/current grid state | `QueryPageAsync` or snapshot query | Core `Query` request/reply | Replace local projection state |
| Virtual scrolling | `QueryPageAsync` | Core `Query` request/reply | Page cache with request cancellation |
| Export all matching rows | `EnumerateAsync` over pages | Core `Query` request/reply | Sequential bounded processing |
| Ask an actor to act | Existing command API | Core `Command` | Separate command; never performed through listener completion |

## 16. Error, reconnect, and idempotency rules

### 16.1 Core observers

- treat disconnection as a gap;
- report disconnected/recovering state to the application;
- recreate the subscription using the host lifecycle policy;
- query a fresh snapshot after reconnect;
- never claim that all realtime or notification messages were observed; and
- prevent an unbounded reconnect loop through delay, jitter, and cancellation.

### 16.2 JetStream participants

- use stable durable-consumer identity appropriate to the service instance and logical responsibility;
- expect redelivery and duplicates;
- make processing idempotent using event identity, aggregate/projection revision, or an inbox record;
- complete only after required durable processing;
- abandon transient failures with the configured delay;
- distinguish poison messages from transient dependency failures;
- expose maximum-delivery exhaustion; and
- do not hide terminal consumer-loop failure.

### 16.3 Query callers

- honor cancellation for superseded UI requests;
- apply timeouts appropriate to interactive versus batch use;
- distinguish unavailable, invalid cursor, invalid filter, and internal failure;
- ignore late results belonging to an older UI request generation; and
- retain the last valid page or display state only when the screen's UX explicitly permits it.

## 17. Observability

Every subscription should expose OpenTelemetry-compatible metrics or equivalent measurements for:

- received deliveries;
- admitted deliveries;
- completed deliveries;
- abandoned deliveries;
- JetStream redeliveries and delivery attempts;
- pending and peak-pending count;
- backpressured write count and duration;
- deserialization and handler failures;
- consumer-loop restarts;
- reconnect count and disconnected duration;
- receive-to-admission latency;
- admission-to-completion latency;
- payload bytes and configured capacity; and
- graceful and forced shutdown outcomes.

Realtime UI processors should additionally measure:

- input event rate;
- per-key coalesced count;
- stale/duplicate revision count;
- detected revision gaps;
- snapshot refresh count and duration;
- rows changed per render;
- render cadence;
- UI-dispatch queue latency;
- UI update duration; and
- dropped observational updates, if a documented downstream loss policy permits them.

Paged queries should measure page size, response time, result count, continuation depth, count-query cost, cancellations, invalid tokens, and backend rows examined where available.

## 18. Security and isolation

The current system uses conventions rather than full credential enforcement, but future implementations must not weaken these boundaries:

- a subscription remains limited to one actor type;
- the factory validates subjects against that actor type;
- `Notify` observers do not become workflow participants;
- external observation of `Realtime` remains non-participating;
- durable external participants use `Event` through JetStream;
- a listener cannot mutate actor state; it sends a separate `Command` when behavior is required;
- query filters, sort fields, page sizes, and cursors are validated server-side; and
- diagnostic envelopes and cursor tokens must not expose secrets or broker credentials.

NATS permissions and credentials may later enforce these rules, but that is a separate implementation decision.

## 19. Testing requirements

### 19.1 Contract unit tests

- actor type routes to the expected listener and rejects an incompatible type;
- mixed-actor-type event maps are rejected;
- a subscription can be enumerated once only;
- completion and abandonment are idempotent and mutually exclusive;
- incomplete disposal releases Core payloads;
- incomplete disposal abandons JetStream deliveries;
- payload ownership remains valid until terminal delivery state;
- cancellation stops enumeration and releases resources;
- handler and enumerable adapters produce equivalent completion results; and
- no event is delivered to both consumption frontends.

### 19.2 Core NATS integration tests

- subject and verb filtering;
- bounded slow-consumer behavior;
- disconnect and reconnect signaling;
- cancellation during receive and processing;
- expected non-replay after downtime;
- latest/keyed-latest downstream convergence; and
- repeated startup and shutdown without leaked tasks or subscriptions.

### 19.3 JetStream integration tests

- ACK only after completion;
- NAK or ACK-timeout redelivery after abandonment;
- redelivery after process interruption;
- bounded `MaxAckPending` behavior;
- dispatcher/partition ordering;
- duplicate/idempotent processing;
- poison-message maximum-delivery behavior;
- graceful drain and forced stop; and
- durable resume using stable consumer identity.

### 19.4 Realtime grid tests

- burst load does not produce one UI dispatch per event;
- final row values converge to the latest accepted revisions;
- stale revisions cannot overwrite newer state;
- snapshot-plus-live startup does not lose deltas in the tested consistency model;
- reconnect triggers snapshot reconciliation;
- sorting/filtering does not block the UI thread under target load;
- screen closure cancels and disposes its subscription; and
- CPU, allocation, GC, and UI-dispatch latency remain within approved budgets.

### 19.5 Paging tests

- deterministic page order and tie-break behavior;
- no duplicates or omissions for the declared consistency model;
- cancellation and superseded request handling;
- invalid, expired, or incompatible cursors;
- server-side maximum page size;
- optional total count behavior;
- sequential `EnumerateAsync` termination; and
- query timeout and backend-unavailable failures.

## 20. Proposed implementation sequence

Implementation requires a separate reviewed plan. The expected sequence is:

1. Define transport-neutral envelope, delivery, subscription, options, state, and metrics contracts.
2. Extract or introduce one internal delivery pump per concrete transport without changing existing behavior.
3. Implement the callback compatibility adapter and prove existing listener tests still pass.
4. Add Core NATS enumerable subscriptions and ownership/lifecycle tests.
5. Add JetStream enumerable subscriptions with explicit completion/abandonment and redelivery tests.
6. Add the ActorType-routing subscription factory and mixed-type validation.
7. Add typed deserialization adapters.
8. Define shared page request/result contracts and one pilot query.
9. Pilot a non-critical realtime strategy/monitor grid using snapshot-plus-live reconciliation.
10. Measure and compare callback and enumerable paths before expanding adoption.
11. Update WinForms, WPF, console, and Docker service implementation documents as each consumer migrates.

No step should remove the callback API until all consumers have migrated and a separate compatibility decision is approved.

## 21. Acceptance criteria for future implementation

The design is successfully implemented only when:

- both Core NATS and JetStream use one internal subscription engine per transport;
- callback and enumerable consumers pass equivalent filtering, lifecycle, and delivery tests;
- JetStream never ACKs merely because a delivery was yielded;
- all buffers are bounded and metrics expose overload;
- actor type, not caller preference, selects transport;
- subscriptions accept one actor type only;
- payload lifetime is safe across asynchronous enumeration;
- Core reconnect behavior is reconciled through projection queries;
- at least one paged projection query uses an opaque stable cursor;
- a pilot realtime grid remains responsive at its approved input burst rate;
- graceful shutdown leaves no owned tasks, consumers, channels, or payloads; and
- the system-wide and UI documentation describe the implemented state accurately.

## 22. Decisions reserved for implementation review

The following details remain deliberately open until implementation planning and measurement:

- final public type and namespace names;
- whether raw compatibility handlers remain on the public interface or move to extension adapters;
- the exact payload ownership representation;
- the default local capacity for each consumer class;
- the default JetStream incomplete-disposal policy;
- dead-letter or terminal poison-message policy;
- whether a snapshot revision is global, projection-specific, or per entity;
- cursor signing/encryption mechanism;
- exact realtime grid render cadence;
- selected WinForms and WPF virtualized collection adapters; and
- whether R3 is used in the first grid pilot or introduced after a channel-only baseline.

These decisions may change implementation detail, but they must not weaken the architectural decisions in Section 2.

## 23. Summary

The future IFM listener model will provide two safe ways to consume actor events:

- the existing callback style for compatibility and straightforward hosted consumers; and
- an owned `IAsyncEnumerable` subscription for composable, lifecycle-aware streaming.

Both styles will share the same Core NATS or JetStream engine, bounded admission, metrics, and delivery semantics. Actor type will continue to select transport. JetStream delivery will be explicitly completed only after successful work, while Core NATS will remain non-durable and loss-tolerant.

Realtime UI and console grids will combine a projection snapshot with revisioned Core NATS deltas, maintain keyed state away from the UI thread, and render coalesced changes at a controlled cadence. Static and slowly changing grids will use explicit cursor-paged query contracts, with `IAsyncEnumerable` available only as a convenience for sequential page traversal.

This separation keeps durable workflows correct, realtime displays responsive, paging scalable, and the same contracts usable by desktop, console, and Docker-hosted processes.
