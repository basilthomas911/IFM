# EventProjector Optional Durable Replay Design

## Status

Implemented after design review. Durable behavior remains the compatibility default; no existing production
descriptor was changed to non-durable as part of this work.

## Purpose

Allow each event projection descriptor to choose between the existing durable JetStream process/replay workflow and a
non-durable queued workflow. Durable replay remains the default, so existing projectors retain their current behavior
unless an event descriptor explicitly opts out.

The requested public concept is:

```csharp
UseDurableReplay = true // default
```

When `UseDurableReplay` is `false`, the event must still run asynchronously through the projector's normal action
contract, but it must not be published to a JetStream process or replay stream, persisted as recoverable projector
state, included in startup recovery, or exposed through operator retry/skip controls.

## Scope of the flag

The system-wide adoption convention is that every command actor should eventually own an EventProjector. Migration
can be incremental, but new command actors should define their projector and immutable event descriptors from the
start. `UseDurableReplay` then chooses the delivery guarantee for each of that command actor's projected event types;
it does not decide whether the command actor has a projector.

`UseDurableReplay` belongs to `EventProjectionDescriptor`, not to `BaseEventProjector` or the global reliability
configuration. This permits a single projector to contain both kinds of event:

```csharp
Describe<PositionChangedEvent, PositionChangedCompleteEvent, PositionChangedFailEvent>(
    applyAsync,
    useDurableReplay: false);

Describe<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent>(
    applyAsync); // UseDurableReplay defaults to true
```

The flag is immutable after descriptor construction and is frozen with the existing descriptor table at startup. It
is an event-contract decision, not a runtime switch and not a per-message override.

The current descriptor contains three registered functions:

1. `ApplyAsync`, which applies the target projection;
2. `CompletedEventFactory`, which creates the success event; and
3. `FailedEventFactory`, which creates the failure event.

All three remain available in both delivery modes. For one execution, `ApplyAsync` runs first and exactly one outcome
branch is selected: the completion factory after a successful apply, or the failure factory after a failed result or
exception. The existing optional processing-event publication remains a separate descriptor policy.

## Delivery model

```text
DomainEventsProjectionAsync
             |
             +-- descriptor.UseDurableReplay == true
             |        |
             |        +-- JetStream process queue
             |             -> fenced/legacy stage execution
             |             -> JetStream replay on failure
             |             -> startup recovery and operator controls
             |
             +-- descriptor.UseDurableReplay == false
                      |
                      +-- bounded in-memory projector queue
                           -> one best-effort execution
                           -> completion or failure publication
                           -> no persistence and no replay
```

The flag controls the entire work-delivery lane, despite its `UseDurableReplay` name. A `false` value does not mean
"durable process once, but do not retry." It means that neither initial processing nor retry delivery uses
JetStream. This distinction is necessary to satisfy the non-durable queue requirement.

### Behavior matrix

| Capability | `UseDurableReplay = true` | `UseDurableReplay = false` |
| --- | --- | --- |
| Initial work queue | NATS JetStream process stream | Bounded in-memory channel |
| Survives process restart | Yes | No |
| Automatic replay | Yes | No |
| Projector execution-state row | Yes | No |
| Blackboard execution-state entry | Yes | No |
| Startup recovery candidate | Yes | No |
| Fenced stage transitions | Yes | No |
| Transactional publication outbox | Available when enabled | Never used |
| `RetryExactAsync` / `SkipAsync` | Available | Not applicable |
| Processing publication | As configured | As configured, best effort |
| Target projection action | Durable staged execution | One queued attempt |
| Completion/failure publication | Durable path rules | One best-effort publication |
| Queue backpressure | JetStream publish acknowledgement | Await bounded channel capacity |
| Crash loss | Recoverable | Accepted by design |

## Non-durable queue design

The implementation introduces an internal `IEventProjectorTransientQueue` abstraction with the channel-backed
`EventProjectorTransientQueue`. Each instance is owned by one projector and one application process. Its lifecycle
operations are:

```csharp
ValueTask StartAsync(
    Func<IEvent, CancellationToken, ValueTask> handler,
    CancellationToken cancellationToken);

ValueTask EnqueueAsync(
    IEvent domainEvent,
    CancellationToken cancellationToken = default);

ValueTask StopAsync(
    CancellationToken cancellationToken = default);
```

The initial implementation should use a bounded `Channel<IEvent>` with multiple writers, one reader, and
`BoundedChannelFullMode.Wait`:

- awaiting capacity applies backpressure instead of silently dropping accepted work;
- one reader preserves enqueue order within one projector process;
- the projector action never runs inline on the command actor call path;
- a host crash may lose queued or executing work, which is the explicit cost of opting out of durability; and
- queue capacity is the validated `EventProjectorReliabilityOptions.NonDurableQueueCapacity` setting, whose default
  is 8,192.

This queue does not use NATS Core. Projection work is internal application execution, not an Event, Notify, or
Realtime actor delivery contract. A local channel avoids serialization, network transit, accidental external
observation, and cross-instance execution of delegates tied to the local projector context. The processing,
completion, and failure actor events produced by the actions still follow their normal actor delivery convention. If
distributed best-effort projection becomes a separate requirement, a NATS Core implementation can later be added
behind the same interface without changing descriptor contracts.

## Non-durable execution semantics

The transient worker executes one event as follows:

1. Resolve the already-frozen descriptor. A missing descriptor is logged as a contract violation and dropped; it does
   not enter durable terminal-state handling.
2. If `PublishProcessingEvent` is enabled, publish the processing event once. A publication exception is logged, but
   does not prevent the target projection from running.
3. Invoke `ApplyAsync` exactly once with a transient `ProjectionExecutionContext`. The context retains projector and
   event identity, but its execution token is process-local and it does not imply a durable claim or receipt.
4. For `Applied` or `AlreadyApplied`, create and publish the completion event once.
5. For `Superseded`, record the outcome and do not publish completion, matching the durable engine's current behavior.
6. For a `Failed` result or an exception from `ApplyAsync`, create and publish the failure event once.
7. Log and meter the final outcome, then release the queue slot. No exception is returned to a replay mechanism.

Completion conversion/publication failure is logged as a completion-notification failure; it must not run the failure
factory because the target projection already succeeded. Failure conversion/publication failure is also logged and
dropped. These notification failures are not retried when durability is disabled.

The non-durable path does not call `EventProjectorExecutionEngine`. Reusing that engine would create durable state,
claims, stage transitions, outbox rows, and recovery candidates, contradicting the descriptor's contract.

## BaseEventProjector routing and lifecycle

`BaseEventProjector` remains the single entry point and owns both lanes:

- startup freezes and validates descriptors before either worker starts;
- the transient worker starts only when at least one descriptor has `UseDurableReplay = false`;
- JetStream preparation, replay callbacks, recovery, and durable workers start only when at least one descriptor has
  `UseDurableReplay = true`;
- readiness becomes true only after every required lane is ready;
- recovery receives only source types whose descriptors use durable replay; and
- `DomainEventsProjectionAsync` looks up each descriptor and enqueues the event into exactly one lane.

There is no dual write and no fallback from the transient lane to the durable lane. If transient enqueueing fails, the
caller receives that enqueue failure. Once enqueueing succeeds, later process termination may lose the work by design.

Graceful `StopAsync` first stops new intake, then drains the transient queue within the supplied cancellation budget.
Cancellation or process termination may abandon remaining transient work. The number abandoned during a controlled
shutdown should be logged and metered.

## Durable behavior remains unchanged

Descriptors with `UseDurableReplay = true` continue to use the current behavior:

- initialize recoverable state before JetStream publication;
- consume through the existing process worker;
- use legacy or fenced stage execution according to the existing rollout options;
- route genuine processing failures to the replay stream;
- use bounded startup recovery;
- use the transactional outbox when enabled; and
- support maximum-attempt handling and operator retry/skip.

`UseDurableReplay` is independent of `BoundedRecoveryEnabled`, `FencedExecutionEnabled`, and
`TransactionalOutboxEnabled`. Those switches select implementation behavior inside the durable lane only.

## Operational API behavior

Operational state pages naturally contain durable descriptors only because transient execution creates no state row.
`RetryExactAsync(eventId)` must return `false` when the event resolves to a descriptor with
`UseDurableReplay = false`. `SkipAsync` operates on an existing durable state row and therefore also has no transient
work to skip.

Transient metrics should distinguish at least these bounded outcomes without adding event IDs or event type names as
metric dimensions:

- accepted;
- completed;
- superseded;
- apply-failed;
- processing-publication-failed;
- terminal-publication-failed; and
- abandoned-on-shutdown.

The existing readiness snapshot's recovery counts remain durable-only. A transient queue depth and busy-worker gauge
may be added to the existing projector meter.

## Ordering and delivery guarantees

Within one projector instance, the single transient reader preserves enqueue order. It does not provide the durable
same-stream fencing used by the JetStream path, and it does not coordinate ordering across application instances.
Therefore a descriptor may opt out only when all of the following are acceptable:

- loss on crash or restart;
- no automatic or operator replay;
- at-most-one attempted execution after successful local enqueue;
- no cross-instance ordering guarantee; and
- best-effort processing/completion/failure publications.

The target action should still be naturally idempotent where practical. Although the transient queue itself does not
redeliver, upstream command retries or duplicate source events can still cause another projection request.

## Configuration and validation

Descriptor validation adds these rules:

- `UseDurableReplay` defaults to `true`;
- it is immutable after construction;
- every source type still has exactly one descriptor; and
- durable recovery type selection is derived from the frozen descriptor map, preventing a second manually maintained
  list from disagreeing with the flag.

The non-durable queue capacity is host configuration, while the decision to use it remains part of the descriptor.
Changing `UseDurableReplay` requires a deployment and restart; it must not be changed dynamically.

## Migration rule

Changing an existing event type from durable to non-durable can strand process/replay messages or nonterminal
execution state if it is done while durable work remains. The rollout procedure is:

1. deploy the flag support with every descriptor left at its default `true` value;
2. drain and verify the affected event types have no process, replay, execution-state, or outbox backlog;
3. change only the reviewed descriptors to `UseDurableReplay = false`;
4. restart the projector so its frozen descriptor map and worker set are rebuilt; and
5. verify transient queue metrics and expected crash-loss semantics under load.

The first implementation should not silently convert already-durable backlog into transient work. A future reverse
change from `false` to `true` affects new intake only; previously lost transient work cannot be reconstructed unless it
is independently regenerated from the source event log by an explicit migration tool.

## Verification coverage

Required contract and integration coverage:

1. descriptor default is `UseDurableReplay = true`;
2. existing durable projector tests remain unchanged and pass;
3. a false descriptor invokes no `IDurableReplayQueue` prepare, enqueue, replay, or recovery operation when it is the
   only descriptor mode in a projector;
4. mixed descriptor tables route each event to exactly one queue;
5. transient intake creates no projector state, Blackboard state, claim, or outbox row;
6. transient success invokes apply and completion once;
7. transient failed result and thrown apply invoke failure publication once and never replay;
8. processing-publication failure does not suppress the target action;
9. completion/failure publication errors are logged and not replayed;
10. bounded capacity applies backpressure and preserves enqueue order;
11. graceful stop drains within its cancellation budget and reports abandoned work when cancelled;
12. startup readiness reflects the required lane or lanes; and
13. retry/recovery APIs reject or exclude non-durable descriptors.

## Implemented decisions

The reviewed implementation follows these five decisions:

1. `UseDurableReplay` is per event descriptor, with `true` as the compatibility default.
2. `false` bypasses both JetStream process and replay streams, not only the replay stream.
3. The non-durable queue is a bounded local channel rather than NATS Core.
4. Target apply receives one attempt; processing and terminal publications are best effort and never trigger replay.
5. Non-durable events have no projector state, startup recovery, transactional outbox, or operator retry/skip support.
