# Event Projector Implementation Details

## Purpose

`TomasAI.IFM.Application.EventProjector` provides the reusable application-layer workflow for projecting persisted domain events into read-side or integration-side state. It combines:

- a durable queue for asynchronous delivery and retry;
- a persisted, per-event/per-projector checkpoint;
- a Blackboard cache of active checkpoints;
- a resumable projection state machine; and
- actor-context publication of processing, completion, and failure events.

Concrete projectors inherit `BaseEventProjector<TActor>`, declare the event types they support, and route each event to `EventProjectorBuilder.RunAsync` with the projection operation that updates the target store.

## Project folder map

The following tree documents every directory currently present beneath the project root, from the root to each leaf. `bin` and `obj` are generated build trees; their contents can change with build configuration, target framework, SDK, and runtime assets.

```text
TomasAI.IFM.Application.EventProjector/       Project root
├── Contracts/                                Public projector interfaces
├── Docs/                                     Maintained project documentation
├── bin/                                      Generated build output
│   ├── Debug/
│   │   └── net10.0/
│   │       └── runtimes/
│   │           └── win-x64/
│   │               └── native/               Debug native runtime dependencies
│   └── Release/
│       └── net10.0/
│           └── runtimes/
│               └── win-x64/
│                   └── native/               Release native runtime dependencies
└── obj/                                      Generated intermediate build state
    ├── Debug/
    │   └── net10.0/
    │       ├── ref/                          Debug reference assembly
    │       └── refint/                       Debug internal reference assembly
    └── Release/
        └── net10.0/
            ├── ref/                          Release reference assembly
            └── refint/                       Release internal reference assembly
```

### Folder responsibilities

| Folder | Kind | Responsibility |
| --- | --- | --- |
| `TomasAI.IFM.Application.EventProjector/` | Source | Owns the base projector, execution builder, state definition, project file, and its child folders. |
| `Contracts/` | Source leaf | Defines `IEventProjector` and the actor-specific marker interface `IEventProjector<TActor>`. |
| `Docs/` | Documentation leaf | Contains this implementation and project-structure reference. |
| `bin/` | Generated | Root of compiled output. Do not edit or commit its contents. |
| `bin/Debug/` | Generated | Debug-configuration output. |
| `bin/Debug/net10.0/` | Generated | Debug assemblies, symbols, dependency metadata, and runtime assets for .NET 10. |
| `bin/Debug/net10.0/runtimes/` | Generated | Runtime-specific Debug assets. |
| `bin/Debug/net10.0/runtimes/win-x64/` | Generated | Windows x64 Debug assets. |
| `bin/Debug/net10.0/runtimes/win-x64/native/` | Generated leaf | Native Windows x64 libraries copied from transitive dependencies. |
| `bin/Release/` | Generated | Release-configuration output. |
| `bin/Release/net10.0/` | Generated | Release assemblies, symbols, dependency metadata, and runtime assets for .NET 10. |
| `bin/Release/net10.0/runtimes/` | Generated | Runtime-specific Release assets. |
| `bin/Release/net10.0/runtimes/win-x64/` | Generated | Windows x64 Release assets. |
| `bin/Release/net10.0/runtimes/win-x64/native/` | Generated leaf | Native Windows x64 libraries copied from transitive dependencies. |
| `obj/` | Generated | Root of NuGet restore data and compiler/MSBuild intermediates. Do not edit or commit its contents. |
| `obj/Debug/` | Generated | Debug compiler/MSBuild intermediates. |
| `obj/Debug/net10.0/` | Generated | .NET 10 Debug generated sources, caches, assemblies, and reference folders. |
| `obj/Debug/net10.0/ref/` | Generated leaf | Debug reference assembly consumed by other projects at compile time. |
| `obj/Debug/net10.0/refint/` | Generated leaf | Debug internal reference assembly used during compilation. |
| `obj/Release/` | Generated | Release compiler/MSBuild intermediates. |
| `obj/Release/net10.0/` | Generated | .NET 10 Release generated sources, caches, assemblies, and reference folders. |
| `obj/Release/net10.0/ref/` | Generated leaf | Release reference assembly consumed by other projects at compile time. |
| `obj/Release/net10.0/refint/` | Generated leaf | Release internal reference assembly used during compilation. |

## Source-file inventory

| File | Responsibility |
| --- | --- |
| `BaseEventProjector.cs` | Implements projector startup, shutdown, durable intake, queued-event dispatch, startup recovery, terminal-state filtering, actor publication support, and exception-state recording. |
| `EventProjectorBuilder.cs` | Configures the four workflow actions and executes the persisted projection state machine. |
| `EventProjectorState.cs` | Defines a stream-oriented projector checkpoint record. It is not currently used by `BaseEventProjector` or `EventProjectorBuilder`, which use `EventProjectorStateReadModel` from the Shared project. |
| `Contracts/IEventProjector.cs` | Defines the projector identity, lifecycle, intake, processing, infrastructure, cache, actor context, and logging contract. |
| `TomasAI.IFM.Application.EventProjector.csproj` | Targets .NET 10 with nullable reference types and implicit usings enabled, and declares the project dependencies. |
| `Docs/EventProjector-Implementation-Details.md` | This implementation and folder reference. |

## Project dependencies

The project directly references:

| Project | Role in this implementation |
| --- | --- |
| `TomasAI.IFM.Application.Storage` | Supplies `IEventSourceActorDbContext`, including persisted projector state and event-log recovery queries. |
| `TomasAI.IFM.Framework.Messaging.NatsJetStream` | Supplies the durable replay-queue implementation through the `TomasAI.IFM.Framework.Messaging.Nats` assembly/namespace. |
| `TomasAI.IFM.Shared` | Supplies event contracts, actor abstractions, queue contracts, projector enums/read models, conversion helpers, and event-sourcing types. |

The implementation also works with `IBlackboardService` to cache active `EventProjectorStateReadModel` values. Cache entries are isolated by projector and event using a key equivalent to `EventProjectorState:{projectorName}:{eventId}`.

## Public contract

`IEventProjector` exposes four groups of members:

1. **Identity and routing**
   - `ActorName`
   - `ProjectorName`
   - `DurableProcessQueueName`
   - `DurableReplayQueueName`
   - `ProjectedEventTypes`
2. **Lifecycle**
   - `StartAsync(context, cancellationToken)`
   - `StopAsync(cancellationToken)`
3. **Data plane**
   - `DomainEventsProjectionAsync(domainEvents)`
   - `ProcessDomainEventAsync(domainEvent)`
4. **Infrastructure**
   - `DbEventSource`
   - `DurableReplayQueue`
   - `BlackboardService`
   - `Context`
   - `Logger`

`IEventProjector<TActor>` adds the constraint that the projector belongs to an `ICommandActor<TActor>` without adding more members.

`DurableProcessQueueName` and `DurableReplayQueueName` are required identity properties and are available to derived projectors for logging or integration naming. The current base implementation calls `IDurableReplayQueue` with `ProjectorName` as its registration key; it does not pass either queue-name property to that abstraction.

## Runtime lifecycle

### Startup

The owning command actor calls `StartAsync` once during its lifecycle:

1. Store and validate the actor `ICommandActorContext`.
2. Register `ProcessQueuedDomainEventAsync` as the durable queue handler for `ProjectorName`.
3. Query and enqueue recoverable event-log entries through `RecoverUncompletedEventsAsync`.
4. Start the durable queue worker with a 30-second replay interval.

Handler registration happens before recovery and worker startup, so recovered messages cannot be consumed before a handler exists. Accessing `Context` before startup throws `InvalidOperationException`.

### Shutdown

`StopAsync` stops the durable worker registered for `ProjectorName`. The projector retains its context, queue configuration, and handler registration so the same instance can be started again.

## Event intake and durable dispatch

`DomainEventsProjectionAsync` handles each event in order:

1. Create an initial `EventProjectorStateReadModel`:
   - `IsReplay = false`
   - `AttemptNumber = 0`
   - `Stage = PublishProcessingEvent`
   - `Outcome = Processing`
2. Persist the state through `InsertEventProjectorStateAsync`.
3. Cache the state in the Blackboard under `(EventId, ProjectorName)`.
4. Enqueue the domain event under `ProjectorName`.

Persisting the recovery marker before enqueueing closes the failure window in which the source event has committed but queue publication fails. A later startup can recover the event from the event log because its nonterminal projector state already exists.

When a queued event is delivered, `ProcessQueuedDomainEventAsync` loads its state from the Blackboard first and then the database. If neither contains a state, it creates and persists one. Terminal entries are acknowledged without reapplying the projection; nonterminal entries are cached and delegated to the derived `ProcessDomainEventAsync` implementation.

## Projection workflow

A derived projector normally creates a fresh builder and calls:

```csharp
await CreateProjectionBuilder()
    .RunAsync<TEvent, TComplete, TFail, TEntityId>(
        sourceEvent,
        e => targetStore.ApplyAsync(e));
```

`RunAsync` configures four actions:

| Action | Behavior |
| --- | --- |
| Processing-event action | Validates that `CommandId` is populated and, unless `postProjectionEvent` is `false`, publishes the source event through the actor context after normalizing its subject. |
| Projection action | Invokes the supplied `Func<TEvent, Task>` and converts normal completion into a successful `ServiceResult`. |
| Completed-event action | Converts the source event to `TComplete` and publishes it through the actor context when conversion succeeds. |
| Failed-event action | Converts the source event and error into `TFail` and publishes it through the actor context when conversion succeeds. |

The builder then loads the existing persisted state and resumes at its current stage.

```text
PublishProcessingEvent
        │ publish source/processing event (optional)
        ▼
ApplyProjection
        │
        ├── successful result ──► PublishCompletedEvent ──► Completed / Completed
        │
        └── failed result ──────► PublishFailedEvent ─────► Completed / Failed

Any thrown exception
        └── persist current stage with Retrying, then rethrow to the durable queue
```

### State transitions

| Current stage | Operation | Next stage | Outcome after transition | Cache behavior |
| --- | --- | --- | --- | --- |
| `PublishProcessingEvent` | Optionally publish the processing/source event. | `ApplyProjection` | Unchanged | Persist and update cache. |
| `ApplyProjection` | Apply the supplied target-store operation. | `PublishCompletedEvent` on success | `Processing` | Persist and update cache. |
| `ApplyProjection` | Receive an unsuccessful `ServiceResult`. | `PublishFailedEvent` | `Retrying` with error text | Persist and update cache. |
| `PublishCompletedEvent` | Publish the typed completion event. | `Completed` | `Completed` | Persist and clear cache. |
| `PublishFailedEvent` | Publish the typed failure event. | `Completed` | `Failed` | Persist and clear cache. |
| Any active stage | Catch a thrown exception. | Current stage is retained. | `Retrying` with exception message | Persist, update cache, and rethrow. |

The shared stage enum also defines `None`, `ValidateSourceEvent`, and `PersistCompletion`, but the current builder does not enter or handle those stages. Loading one of them as an active stage causes `InvalidOperationException`, which is persisted as a retrying failure and rethrown.

## Delivery, retry, and idempotency semantics

The workflow provides **at-least-once** side-effect delivery. At each stage, the external action runs before the next stage is persisted. A process failure between those two operations causes the same stage to run again. Consequently:

- processing events may be published more than once;
- the projection operation may be applied more than once;
- completion or failure events may be published more than once; and
- target-store writes and downstream event consumers must be idempotent, normally using the source event identity or an equivalent unique key.

If an action throws, the builder records `Outcome = Retrying`, retains the current stage, stores the exception message, and rethrows. The durable queue owns retry scheduling and attempt accounting.

Each `RunAsync` call registers a maximum-attempt callback. When the queue declares attempts exhausted, the callback:

1. loads the current state from cache or storage, or creates a replay fallback state;
2. sets `Stage = Completed` and `Outcome = Failed`;
3. records a maximum-attempt error message;
4. persists the terminal state; and
5. clears the Blackboard entry.

The maximum-attempt callback does not publish the typed `TFail` event.

The public `RunAsync` overload accepts `Func<TEvent, Task>`. Its wrapper returns success whenever that task completes and represents failure by a thrown exception. Therefore the explicit unsuccessful-`ServiceResult` branch and `PublishFailedEvent` stage are present in the internal state machine but are not directly reachable through this public overload unless another processing-action API is introduced.

## Startup recovery

`RecoverUncompletedEventsAsync` performs recovery before the queue worker starts:

1. Convert `ProjectedEventTypes` to distinct CLR type names.
2. Query event-log entries eligible for recovery for this `ProjectorName` and those event names.
3. Deserialize each log entry.
4. If deserialization yields `UnknownEvent`, persist a terminal failed state and log an error.
5. Otherwise load the explicit projector state for `(event version, projector name)`.
6. Skip an entry with no explicit state, logging a warning. This prevents a projector from claiming historical events that were never marked for it.
7. Skip terminal states.
8. Re-persist and cache each nonterminal state, then enqueue the reconstructed event.

Recovery is projector-specific: multiple projectors can independently checkpoint the same source event because both persistence and cache lookup include `ProjectorName`.

The cancellation token is checked between recovered entries and is also passed to queue enqueueing.

## Terminal-state rules

The base class treats an event as terminal when either:

- `Stage == Completed`; or
- `Outcome` is `Completed`, `Failed`, `Cancelled`, `Superseded`, or `AlreadyCompleted`.

`Processing` and `Retrying` remain eligible for dispatch or startup recovery.

## Extension pattern

A concrete projector must:

1. Inherit `BaseEventProjector<TActor>`.
2. Provide actor, projector, and queue names.
3. Return every supported source event type in `ProjectedEventTypes`; startup recovery depends on this list.
4. Implement `ProcessDomainEventAsync` and dispatch supported runtime event types.
5. For each supported event, create a fresh `EventProjectorBuilder` and call `RunAsync` with its completion type, failure type, entity-id type, and idempotent projection operation.
6. Allow processing exceptions to propagate so the durable queue can retry them.

The repository's `FundEventProjector` is the reference implementation: it switches over supported fund events and maps each one to a database insert, update, delete, or no-op projection.

## Operational cautions

- Start the projector before accepting event batches; builder event publication requires `Context`.
- Keep `ProjectedEventTypes` synchronized with the derived dispatch switch, or startup recovery will omit supported events.
- Use a new builder per projection. Builder delegates are mutable and only initialize when null unless explicitly overwritten; sharing one builder across unrelated event types can retain actions from a prior run.
- Treat `ProjectorName` as stable persisted identity. Renaming it creates a new checkpoint/cache namespace and prevents the renamed projector from finding old state under the previous name.
- Make projection writes and downstream event handlers idempotent because state advances only after side effects complete.
- Ensure event conversion metadata supports `ToCompleteEvent` and `ToFailEvent`; a null conversion silently suppresses the corresponding publication.
- Do not use `EventProjectorState` as though it were the active checkpoint without an explicit migration. The running implementation persists `EventProjectorStateReadModel`, keyed by numeric event ID and projector name.

## Verification locations

Behavior is exercised outside this project by:

- `TomasAI.IFM.Domain.Fund.UnitTests/FundEventProjectorTests.cs` for base projector behavior;
- `TomasAI.IFM.Domain.Fund.IntegrationTests/FundEventProjectionIntegrationTests.cs` for integrated projection flows; and
- `TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/EventProjectorStatePersistenceTests.cs` for projector-state persistence.

