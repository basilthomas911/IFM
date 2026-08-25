# Intrinsic Time Strategy Workflow Implementation

## Implementation Specification v1.0

- **Status:** Initial skeleton implementation specification
- **Date:** 2026-08-24
- **Companion design:** [Intrinsic-Time-Strategy-Workflow-Design-v0.2.md](./Intrinsic-Time-Strategy-Workflow-Design-v0.2.md)
- **Implementation target:** .NET 10, MessagePack, NATS Core/JetStream, PostgreSQL EventSourceDb, and ScyllaDB
- **Root domain:** `TomasAI.IFM.Domain.Trade`

---

## 1. Purpose

This document converts the Intrinsic Time Strategy Workflow design into a repository-specific implementation plan.

The first implementation creates the workflow skeleton only. It provides:

- workflow Command, Event, Realtime, and Query actors;
- a `FuturesItiSignalGeneratedEvent` trigger routed through the existing realtime router;
- one active workflow execution per workflow entity;
- immutable workflow snapshots passed to strategy pipeline actors;
- opaque, versioned pipeline result envelopes;
- pipeline start commands and pipeline Completed/Failed realtime event contracts;
- PostgreSQL event-sourced workflow state;
- unversioned ScyllaDB operational projections;
- workflow queries, startup registration, recovery, idempotency, observability, and tests.

The implementation deliberately does not calculate regimes, market conditions, trade selections, order compositions, or risk decisions. Those capabilities will be added one pipeline actor at a time.

---

## 2. Required Architectural Outcome

The runtime flow is:

```text
FuturesItiSignalGeneratedEvent
    -> realtime router
    -> IntrinsicTimeStrategyWorkflowRealtimeActor
    -> StartIntrinsicTimeStrategyWorkflowCommand
    -> IntrinsicTimeStrategyWorkflowCommandActor
    -> persisted StartAccepted + RegimeDiscoveryStarted events
    -> IntrinsicTimeStrategyWorkflowEventActor
    -> StartRegimeDiscoveryPipelineCommand
    -> Regime Discovery pipeline actors
    -> RegimeDiscoveryPipelineCompletedEvent or FailedEvent
    -> realtime router
    -> IntrinsicTimeStrategyWorkflowRealtimeActor
    -> CompleteRegimeDiscoveryCommand or FailRegimeDiscoveryCommand
    -> IntrinsicTimeStrategyWorkflowCommandActor
```

This pattern repeats for all five stages.

The workflow actor family is the hub. Pipeline actors are isolated workers:

```text
                         Regime Discovery
                              ^   |
                              |   v
                         Market Condition
                              ^   |
                              |   v
ITI trigger -> Workflow actor family <-> Trade Selection
                              ^   |
                              |   v
                         Order Composition
                              ^   |
                              |   v
                          Risk Management
```

Pipeline actors never address or invoke one another.

---

## 3. Fixed v1 Decisions

1. `FuturesItiSignalGeneratedEvent` is the only initial workflow trigger.
2. Only `Daily`, `Weekly`, and `Monthly` ITI timeframes are eligible.
3. The workflow routing entity is the workflow definition plus the complete `FuturesItiSignalEntityId`.
4. The unique UUIDv7 `StrategyWorkflowId` identifies an execution but is not the actor routing boundary.
5. Only one workflow may be Running for one workflow entity.
6. A distinct trigger received while that entity is Running is recorded as Rejected.
7. Duplicate delivery of the same trigger event is a no-op, not another start attempt.
8. The workflow Command actor is the sole state writer and continuation authority.
9. The workflow Event actor performs post-commit dispatch of pipeline start commands.
10. The workflow Realtime actor consumes the ITI trigger and pipeline Completed/Failed realtime events.
11. The workflow Query actor is side-effect free.
12. Each pipeline start command carries a readonly workflow snapshot and the original ITI event.
13. Each pipeline completion returns only that stage's complete opaque result and workflow metadata.
14. Each pipeline failure uses the standard application failure-event shape plus workflow metadata.
15. Pipeline result events are routed back through lifecycle-owned realtime routes. Pipeline actors do not hard-code the workflow mailbox.
16. The skeleton continuation rule is `Proceed` after a structurally valid Completed result.
17. Failed, invalid, conflicting, cancelled, or timed-out processing stops the workflow.
18. PostgreSQL EventSourceDb is authoritative. ScyllaDB is rebuildable.
19. ScyllaDB table names are unversioned because this is a development schema.
20. No Order Execution command is sent by the skeleton until its final contract is implemented explicitly.

---

## 4. Scope

### 4.1 Included

- shared identifiers, enums, state records, result envelopes, commands, events, queries, and read models;
- actor contexts using the repository's closed-generic context pattern;
- workflow aggregate/state reducer;
- event-source repository and conventional event projector;
- workflow Command, Event, Realtime, and Query actors;
- pipeline address catalog owned by the workflow module;
- startup and shutdown realtime-route lifecycle;
- TradeDb schema, CQL, parameters, read/write APIs, and implementation;
- immutable active-workflow projection cache;
- API/NATS query mapping required to expose the workflow read model;
- XML comments on public classes, methods, properties, and contracts;
- BDD, unit, actor integration, and storage integration tests.

### 4.2 Excluded

- actual pipeline actor calculations;
- stage-specific business parameters;
- typed stage result properties;
- real continuation rules;
- automatic business retries;
- Order Execution dispatch;
- broker operations;
- position monitoring;
- LLM-controlled continuation or risk approval;
- changes to the underlying base actor hierarchy;
- production multi-host event-store compare-and-swap support.

---

## 5. Alignment With Current Repository Conventions

The implementation must follow these existing conventions:

- actors implement the discovered `IActor<TActor>` contracts through the existing base actor classes;
- `TradeActorAssembly.Current` already participates in Simple Injector assembly discovery;
- contexts implement the relevant closed-generic interface such as `ICommandActorContext<TActor>`;
- context dependencies are constructor-injected and assigned with `IsArgumentNull.Set`;
- command actors derive from `BaseEventSourceCommandActor<TActor>`;
- event and realtime actors derive from `BaseEventActor<TActor>`;
- query actors derive from `BaseQueryActor<TActor>`;
- state derives from `BaseEventSourceActorState<TState>`;
- repositories implement `IEventSourceActorStateRepository<TState>` and derive from `BaseEventSourceActorRepository`;
- persisted event projection uses `ConventionalEventProjector<TActor>`;
- MessagePack contracts use explicit sequential integer keys and serialization constructors;
- commands use base keys `0..5`, events use base keys `0..7`, and queries begin with keys `0..1`;
- storage commands call `.Use(commandName, commandText)` with a globally clear name such as `$"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)}"`;
- Scylla parameter records implement `IBindValue`;
- schema objects are registered through `TradeSchemaDb` and `SchemaObjectDefinition`;
- realtime routes are added during `OnStartup` and removed during `OnShutdown`.

No individual concrete actor registration is added to `Startup.cs`. Existing open-generic assembly registration discovers the new actors and contexts from `TradeActorAssembly.Current`.

---

## 6. Repository Structure

### 6.1 Shared contracts

Create under `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime`:

```text
Identity/
    IntrinsicTimeStrategyWorkflowEntityId.cs
    StrategyWorkflowId.cs

Model/
    StrategyWorkflowStartDecision.cs
    StrategyWorkflowStage.cs
    StrategyWorkflowStatus.cs
    StrategyWorkflowOutcome.cs
    StrategyActorProcessingStatus.cs
    StrategyWorkflowContinuationDecision.cs
    StrategyStageResultEnvelope.cs
    StrategyPipelineFailure.cs
    StrategyWorkflowStageState.cs
    IntrinsicTimeStrategyWorkflowState.cs

Commands/
    StartIntrinsicTimeStrategyWorkflowCommand.cs
    CompleteRegimeDiscoveryCommand.cs
    FailRegimeDiscoveryCommand.cs
    TimeoutRegimeDiscoveryCommand.cs
    CompleteMarketConditionCommand.cs
    FailMarketConditionCommand.cs
    TimeoutMarketConditionCommand.cs
    CompleteTradeSelectionCommand.cs
    FailTradeSelectionCommand.cs
    TimeoutTradeSelectionCommand.cs
    CompleteOrderCompositionCommand.cs
    FailOrderCompositionCommand.cs
    TimeoutOrderCompositionCommand.cs
    CompleteRiskManagementCommand.cs
    FailRiskManagementCommand.cs
    TimeoutRiskManagementCommand.cs
    CancelIntrinsicTimeStrategyWorkflowCommand.cs

Pipeline/Commands/
    StartRegimeDiscoveryPipelineCommand.cs
    StartMarketConditionPipelineCommand.cs
    StartTradeSelectionPipelineCommand.cs
    StartOrderCompositionPipelineCommand.cs
    StartRiskManagementPipelineCommand.cs

Pipeline/Events/
    RegimeDiscoveryPipelineCompletedEvent.cs
    RegimeDiscoveryPipelineFailedEvent.cs
    MarketConditionPipelineCompletedEvent.cs
    MarketConditionPipelineFailedEvent.cs
    TradeSelectionPipelineCompletedEvent.cs
    TradeSelectionPipelineFailedEvent.cs
    OrderCompositionPipelineCompletedEvent.cs
    OrderCompositionPipelineFailedEvent.cs
    RiskManagementPipelineCompletedEvent.cs
    RiskManagementPipelineFailedEvent.cs

Events/
    StrategyWorkflowStartAcceptedEvent.cs
    StrategyWorkflowStartRejectedEvent.cs
    RegimeDiscoveryStartedEvent.cs
    StrategyWorkflowRegimeDiscoveryResultRecordedEvent.cs
    StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent.cs
    StrategyWorkflowRegimeDiscoveryFailedEvent.cs
    StrategyWorkflowRegimeDiscoveryTimedOutEvent.cs
    ...same five-event family for each later stage...
    IntrinsicTimeStrategyWorkflowCompletedEvent.cs
    IntrinsicTimeStrategyWorkflowStoppedEvent.cs

Queries/
    GetIntrinsicTimeStrategyWorkflowByIdQuery.cs
    GetActiveIntrinsicTimeStrategyWorkflowQuery.cs
    GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery.cs
    GetIntrinsicTimeStrategyWorkflowStageStateQuery.cs
    GetIntrinsicTimeStrategyWorkflowTimelineQuery.cs
    GetRecentIntrinsicTimeStrategyWorkflowsQuery.cs
    GetCompletedIntrinsicTimeStrategyWorkflowsQuery.cs
    GetStoppedIntrinsicTimeStrategyWorkflowsQuery.cs

QueryParameters/
    ...one typed parameter record per query...

ViewModels/
    IntrinsicTimeStrategyWorkflowReadModel.cs
    IntrinsicTimeStrategyWorkflowStartAttemptReadModel.cs
    IntrinsicTimeStrategyWorkflowTimelineReadModel.cs
```

### 6.2 Domain implementation

Create under `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime`:

```text
Command/Actor/
    IntrinsicTimeStrategyWorkflowCommandActor.cs
    IntrinsicTimeStrategyWorkflowCommandContext.cs

Command/State/
    IntrinsicTimeStrategyWorkflowCommandState.cs
    IntrinsicTimeStrategyWorkflowStateRepository.cs

Command/EventProjector/
    IntrinsicTimeStrategyWorkflowEventProjector.cs

Command/Validation/
    IntrinsicTimeStrategyWorkflowValidation.cs

Command/Extensions/
    IntrinsicTimeStrategyWorkflowCommandContextExtensions.cs

Event/Actor/
    IntrinsicTimeStrategyWorkflowEventActor.cs
    IntrinsicTimeStrategyWorkflowEventContext.cs

Event/Extensions/
    IntrinsicTimeStrategyWorkflowEventExtensions.cs

Realtime/Actor/
    IntrinsicTimeStrategyWorkflowRealtimeActor.cs
    IntrinsicTimeStrategyWorkflowRealtimeContext.cs

Realtime/Extensions/
    IntrinsicTimeStrategyWorkflowRealtimeExtensions.cs

Query/Actor/
    IntrinsicTimeStrategyWorkflowQueryActor.cs
    IntrinsicTimeStrategyWorkflowQueryContext.cs

Query/Extensions/
    IntrinsicTimeStrategyWorkflowQueryContextExtensions.cs

Routing/
    IntrinsicTimeStrategyPipelineRoutes.cs

Projection/
    IIntrinsicTimeStrategyWorkflowProjectionCache.cs
    IntrinsicTimeStrategyWorkflowProjectionCache.cs
```

### 6.3 Tests

Add matching tests to:

```text
TomasAI.IFM.Domain.Trade.UnitTests/
TomasAI.IFM.Domain.Trade.BDDTests/
TomasAI.IFM.Domain.Trade.IntegratedTests/
TomasAI.IFM.Application.Actor.IntegrationTests/
TomasAI.IFM.Application.Storage.IntegrationTests/
```

---

## 7. Identity Model

### 7.1 Workflow definition identity

The skeleton has one workflow definition:

```csharp
public static class IntrinsicTimeStrategyWorkflowDefinition
{
    public const string Id = "IntrinsicTimeStrategy";
    public const int Version = 1;
}
```

This stable definition ID is not an execution ID.

### 7.2 Workflow actor entity identity

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowEntityId : IActorEntityId
{
    [Key(0)] public string WorkflowDefinitionId { get; init; } = string.Empty;
    [Key(1)] public FuturesItiSignalEntityId ItiSignalEntityId { get; init; } = new();

    public IntrinsicTimeStrategyWorkflowEntityId() { }

    public IntrinsicTimeStrategyWorkflowEntityId(
        string workflowDefinitionId,
        FuturesItiSignalEntityId itiSignalEntityId)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        ItiSignalEntityId = itiSignalEntityId;
    }

    public string Format() =>
        $"{WorkflowDefinitionId}.{ItiSignalEntityId.Format()}";
}
```

Example keys:

```text
IntrinsicTimeStrategy.ES-202609.20260824.Daily
IntrinsicTimeStrategy.ES-202609.20260818.Weekly
IntrinsicTimeStrategy.ES-202609.20260801.Monthly
```

The exact formatted contract text comes from `FuturesItiSignalEntityId.Format()`.

For one contract and one current Daily/Weekly/Monthly timeframe set, there are at most three independently routed workflow entities. Because the ITI entity contains `TimeFrameStartValueDate`, a later timeframe creates a later entity identity.

### 7.3 Execution identity

```csharp
[MessagePackObject]
public readonly record struct StrategyWorkflowId([property: Key(0)] Guid Value)
{
    public static StrategyWorkflowId New(TimeProvider timeProvider) =>
        new(Guid.CreateVersion7(timeProvider.GetUtcNow()));

    public override string ToString() => Value.ToString("N");
}
```

`StrategyWorkflowId` is generated for a proposed start and becomes authoritative only when the start is accepted.

### 7.4 Command stream identity

Every workflow command targets:

```text
ActorType.Command
IntrinsicTimeStrategyWorkflowCommand
{IntrinsicTimeStrategyWorkflowEntityId.Format()}
```

The existing `ActorSubject.StreamId` therefore provides one event stream per workflow entity:

```text
Command.IntrinsicTimeStrategyWorkflowCommand.{workflowEntityId}
```

Do not create a new stream for every UUIDv7 execution. Sequential executions and rejected attempts for the same workflow entity must share the stream so single-flight state can be reconstructed.

---

## 8. Immutable Workflow State

### 8.1 Opaque result envelope

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyStageResultEnvelope
{
    [Key(0)] public Guid ResultId { get; init; }
    [Key(1)] public string ResultType { get; init; } = string.Empty;
    [Key(2)] public int SchemaVersion { get; init; }
    [Key(3)] public string ContentType { get; init; } = "application/x-msgpack";
    [Key(4)] public ReadOnlyMemory<byte> Payload { get; init; }
    [Key(5)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(6)] public DateTime MarketDataAsOfUtc { get; init; }
    [Key(7)] public DateTime ProducedAtUtc { get; init; }
}
```

Rules:

- `ResultId` must be non-empty;
- `ResultType` must be a stable logical contract name;
- `SchemaVersion` must be positive;
- `Payload` may be empty only when that stage contract later explicitly permits it;
- `PayloadSha256` is calculated over the exact serialized payload bytes;
- the skeleton default maximum payload is 64 KiB per stage and is configurable;
- the workflow does not deserialize or interpret stage payloads in v1.

### 8.2 Standard pipeline failure

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyPipelineFailure
{
    [Key(0)] public int ErrorCode { get; init; }
    [Key(1)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(2)] public string ErrorType { get; init; } = string.Empty;
    [Key(3)] public string ErrorData { get; init; } = string.Empty;
    [Key(4)] public DateTime FailedAtUtc { get; init; }
}
```

Pipeline Failed events also implement `IErrorEvent<IntrinsicTimeStrategyWorkflowEntityId>` so they retain the standard application failure metadata.

### 8.3 Stage state

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record StrategyWorkflowStageState
{
    [Key(0)] public StrategyActorProcessingStatus ProcessingStatus { get; init; }
    [Key(1)] public StrategyWorkflowContinuationDecision ContinuationDecision { get; init; }
    [Key(2)] public DateTime? StartedAtUtc { get; init; }
    [Key(3)] public DateTime? CompletedAtUtc { get; init; }
    [Key(4)] public DateTime? FailedAtUtc { get; init; }
    [Key(5)] public StrategyStageResultEnvelope? Result { get; init; }
    [Key(6)] public string ContinuationRuleSetId { get; init; } = string.Empty;
    [Key(7)] public int ContinuationRuleSetVersion { get; init; }
    [Key(8)] public string[] ContinuationReasonCodes { get; init; } = [];
    [Key(9)] public StrategyPipelineFailure? Failure { get; init; }
}
```

Arrays must be created as new arrays when state is revised. No mutable collection owned by the command aggregate may cross an actor boundary.

### 8.4 Workflow state

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowState
{
    [Key(0)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; } = new();
    [Key(1)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(2)] public Guid TriggerEventId { get; init; }
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public int WorkflowDefinitionVersion { get; init; }
    [Key(5)] public StrategyWorkflowStatus Status { get; init; }
    [Key(6)] public StrategyWorkflowOutcome Outcome { get; init; }
    [Key(7)] public StrategyWorkflowStage CurrentStage { get; init; }
    [Key(8)] public long WorkflowRevision { get; init; }
    [Key(9)] public DateTime StartedAtUtc { get; init; }
    [Key(10)] public DateTime? TerminalAtUtc { get; init; }
    [Key(11)] public StrategyWorkflowStageState RegimeDiscovery { get; init; } = new();
    [Key(12)] public StrategyWorkflowStageState MarketCondition { get; init; } = new();
    [Key(13)] public StrategyWorkflowStageState TradeSelection { get; init; } = new();
    [Key(14)] public StrategyWorkflowStageState OrderComposition { get; init; } = new();
    [Key(15)] public StrategyWorkflowStageState RiskManagement { get; init; } = new();
    [Key(16)] public string StopReasonCode { get; init; } = string.Empty;
}
```

The full original `FuturesItiSignalGeneratedEvent` is not duplicated inside workflow state. The state keeps its identity. Every pipeline start command separately carries the original trigger event.

### 8.5 Revision semantics

`WorkflowRevision` advances once per accepted logical workflow transition, not once per event in an atomic event batch.

All workflow events produced by one transition carry the same resulting revision:

```text
revision 3:
    StrategyWorkflowMarketConditionResultRecordedEvent
    StrategyWorkflowMarketConditionContinuationEvaluatedEvent
    TradeSelectionStartedEvent
```

A rejected start does not change `WorkflowRevision`.

---

## 9. Trigger and Realtime Router Lifecycle

### 9.1 Required route set

`IntrinsicTimeStrategyWorkflowRealtimeActor` owns these eleven routes:

| Source event | Source actor/verb |
| --- | --- |
| `FuturesItiSignalGeneratedEvent` | `FuturesItiSignalGeneratedEvent.Actor` / `.Verb` |
| `RegimeDiscoveryPipelineCompletedEvent` | its `Actor` / `Verb` |
| `RegimeDiscoveryPipelineFailedEvent` | its `Actor` / `Verb` |
| `MarketConditionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `MarketConditionPipelineFailedEvent` | its `Actor` / `Verb` |
| `TradeSelectionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `TradeSelectionPipelineFailedEvent` | its `Actor` / `Verb` |
| `OrderCompositionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `OrderCompositionPipelineFailedEvent` | its `Actor` / `Verb` |
| `RiskManagementPipelineCompletedEvent` | its `Actor` / `Verb` |
| `RiskManagementPipelineFailedEvent` | its `Actor` / `Verb` |

Each source route uses `ActorType.Realtime` because `AddRealtimeRouter` rejects non-realtime endpoints.

### 9.2 Route declaration

```csharp
static readonly ActorTypeId[] RealtimeRoutes =
[
    new(ActorType.Realtime,
        FuturesItiSignalGeneratedEvent.Actor,
        FuturesItiSignalGeneratedEvent.Verb),
    new(ActorType.Realtime,
        RegimeDiscoveryPipelineCompletedEvent.Actor,
        RegimeDiscoveryPipelineCompletedEvent.Verb),
    new(ActorType.Realtime,
        RegimeDiscoveryPipelineFailedEvent.Actor,
        RegimeDiscoveryPipelineFailedEvent.Verb),
    // remaining stage routes...
];
```

### 9.3 Startup

```csharp
protected override ValueTask OnStartup(
    IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
{
    ArgumentNullException.ThrowIfNull(context);

    foreach (var route in RealtimeRoutes)
        context.AddRealtimeRouter(route, Id);

    return ValueTask.CompletedTask;
}
```

If additional startup work can fail after routes are registered, the actor must remove the routes in a catch block before rethrowing.

### 9.4 Shutdown

```csharp
protected override ValueTask OnShutdown(
    IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
{
    ArgumentNullException.ThrowIfNull(context);

    foreach (var route in RealtimeRoutes)
        context.RemoveRealtimeRouter(route, Id);

    return ValueTask.CompletedTask;
}
```

Routes are removed before other shutdown work so new workflow traffic cannot enter a stopping mailbox.

The supervisor deduplicates identical source/destination registrations. Removing this workflow destination must not remove another realtime actor's destination for the same source event.

### 9.5 Trigger eligibility

The Realtime actor ignores a trigger when:

- `EntityId` is invalid;
- `FuturesItiSignal` is null;
- `TimePeriod` is not `Daily`, `Weekly`, or `Monthly`;
- `Id` is empty.

An eligible trigger becomes `StartIntrinsicTimeStrategyWorkflowCommand`.

---

## 10. MessagePack Contract Rules

### 10.1 Commands

All workflow and pipeline commands implement `ICommand<IntrinsicTimeStrategyWorkflowEntityId>`.

Base keys follow the current command convention:

| Key | Property |
| ---: | --- |
| 0 | `CommandId` |
| 1 | `Subject` |
| 2 | `PostEvents` |
| 3 | `EntityId` |
| 4 | `ErrorCode` |
| 5 | `RouteTo` |

Custom keys start at 6. Constructors must preserve the exact key order.

Add `IntrinsicTimeStrategyWorkflowBoundedContext` and the five pipeline bounded-context names to `BoundedContextName`. Do not use `Undefined` for implemented contracts.

### 10.2 Events

All workflow-owned and pipeline result events use:

| Key | Property |
| ---: | --- |
| 0 | `Subject` |
| 1 | `Id` |
| 2 | `EntityId` |
| 3 | `EventId` |
| 4 | `CommandId` |
| 5 | `AggregateId` |
| 6 | `EventSource` |
| 7 | `ReceivedOn` |

Payload keys start at 8.

### 10.3 Queries

Queries use:

| Key | Property |
| ---: | --- |
| 0 | `Subject` |
| 1 | typed query `EntityId`/parameter |
| 2+ | query-specific values |

### 10.4 Evolution

- Never renumber an established key.
- Never reuse a removed key.
- Append newly designed pipeline parameters at new keys.
- Increment the relevant schema version when payload semantics change.
- Add serialization round-trip tests for every public message contract.

---

## 11. Workflow Command Contracts

### 11.1 Start command

`StartIntrinsicTimeStrategyWorkflowCommand` contains:

```text
base command keys 0..5
6  ProposedWorkflowId
7  TriggerEventId
8  FuturesItiSignalGeneratedEvent TriggerEvent
9  CorrelationId
10 CausationId
11 RequestedAtUtc
12 WorkflowDefinitionVersion
```

The Realtime actor constructs:

```text
EntityId      = IntrinsicTimeStrategy + source.EntityId
TriggerEventId = source.Id
CausationId   = source.Id
CorrelationId = proposed WorkflowId.Value
PostEvents    = true
```

The start command is always routed to `IntrinsicTimeStrategyWorkflowCommandActor.ActorName`.

### 11.2 Complete-stage commands

Each `CompleteXXXCommand` contains:

```text
WorkflowId
InputWorkflowRevision
SourceEventId
StrategyStageResultEnvelope Result
CorrelationId
CausationId
CompletedAtUtc
```

It does not contain a continuation decision or a returned workflow state.

### 11.3 Fail-stage commands

Each `FailXXXCommand` contains:

```text
WorkflowId
InputWorkflowRevision
SourceEventId
StrategyPipelineFailure Failure
CorrelationId
CausationId
FailedAtUtc
```

### 11.4 Timeout commands

Each `TimeoutXXXCommand` contains:

```text
WorkflowId
ExpectedWorkflowRevision
ExpectedStage
TimeoutId
TimedOutAtUtc
```

### 11.5 Cancellation

`CancelIntrinsicTimeStrategyWorkflowCommand` contains:

```text
WorkflowId
ExpectedWorkflowRevision
ReasonCode
RequestedAtUtc
RequestedBy
```

Cancellation is terminal and idempotent.

---

## 12. Pipeline Start Commands

Each stage has a separate command type so actor-specific parameters can be appended later without changing unrelated contracts.

The common v1 payload is:

```text
WorkflowId
InputWorkflowRevision
IntrinsicTimeStrategyWorkflowState WorkflowState
FuturesItiSignalGeneratedEvent TriggerEvent
CorrelationId
CausationId
RequestedAtUtc
ExpectedCompletionAtUtc
```

Example:

```csharp
[MessagePackObject(AllowPrivate = true)]
public sealed record StartMarketConditionPipelineCommand
    : ICommand<IntrinsicTimeStrategyWorkflowEntityId>
{
    // Base command keys 0..5.

    [Key(6)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(7)] public long InputWorkflowRevision { get; init; }
    [Key(8)] public IntrinsicTimeStrategyWorkflowState WorkflowState { get; init; } = new();
    [Key(9)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    [Key(10)] public Guid CorrelationId { get; init; }
    [Key(11)] public Guid CausationId { get; init; }
    [Key(12)] public DateTime RequestedAtUtc { get; init; }
    [Key(13)] public DateTime? ExpectedCompletionAtUtc { get; init; }
}
```

The workflow state revision in the command already contains all accepted previous results:

| Started pipeline | Available accepted results |
| --- | --- |
| Regime Discovery | none |
| Market Condition | Regime Discovery |
| Trade Selection | Regime Discovery, Market Condition |
| Order Composition | Regime Discovery, Market Condition, Trade Selection |
| Risk Management | Regime Discovery, Market Condition, Trade Selection, Order Composition |

The pipeline command does not include the workflow actor address. The pipeline publishes its own Completed/Failed realtime event; the workflow owns the return route through `AddRealtimeRouter`.

---

## 13. Pipeline Output Events

### 13.1 Completed event

Each Completed event implements `ICompleteEvent<IntrinsicTimeStrategyWorkflowEntityId>` and contains:

```text
base event keys 0..7
8  WorkflowId
9  InputWorkflowRevision
10 CorrelationId
11 CausationId
12 StrategyStageResultEnvelope Result
13 CompletedAtUtc
```

The event's `Id` is the stable logical result event identity. Redelivery reuses the same ID.

The event means only that the pipeline actor completed its calculation. It does not contain `Proceed`, `Stop`, or a next-stage address.

### 13.2 Failed event

Each Failed event implements `IErrorEvent<IntrinsicTimeStrategyWorkflowEntityId>` and includes all standard failure properties plus:

```text
WorkflowId
InputWorkflowRevision
CorrelationId
CausationId
PipelineStage
```

The existing standard fields carry:

- `ErrorDate`;
- `ErrorCode`;
- `ErrorMessage`;
- `ErrorType`;
- `ErrorData`;
- `CommandName`;
- `CommandData`.

The workflow Realtime actor converts this event into the corresponding workflow Fail command.

### 13.3 Routing

Pipeline output events keep their pipeline source identity:

```text
ActorType.Realtime
{PipelineRealtimeActorName}
{CompletedOrFailedVerb}
{WorkflowEntityId.Format()}
```

The workflow Realtime actor receives an additional routed copy because it registered that source `ActorTypeId` during startup.

---

## 14. Workflow-Owned Events

### 14.1 Start events

```text
StrategyWorkflowStartAcceptedEvent
StrategyWorkflowStartRejectedEvent
```

Accepted and the first `RegimeDiscoveryStartedEvent` are added to the state in one command transition and persisted in one PostgreSQL transaction.

Rejected records:

- requested workflow ID;
- active workflow ID;
- trigger and command IDs;
- active stage;
- reason `WorkflowAlreadyExecuting`;
- rejection timestamp.

It does not change the active workflow revision.

### 14.2 Stage event family

Each stage has:

```text
XXXStartedEvent
StrategyWorkflowXXXResultRecordedEvent
StrategyWorkflowXXXContinuationEvaluatedEvent
StrategyWorkflowXXXFailedEvent
StrategyWorkflowXXXTimedOutEvent
```

Every event carries:

- workflow entity ID;
- workflow ID;
- resulting workflow revision;
- correlation and causation IDs;
- stage;
- event-specific payload and timestamp.

### 14.3 Terminal events

```text
IntrinsicTimeStrategyWorkflowCompletedEvent
IntrinsicTimeStrategyWorkflowStoppedEvent
```

Only the workflow Command actor creates terminal workflow events.

The skeleton records `Completed` after a valid Risk Management result passes `SkeletonProceedOnValidResult/v1`, but it does not yet issue an Order Execution command.

---

## 15. Workflow Command State

`IntrinsicTimeStrategyWorkflowCommandState` derives from `BaseEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>`.

It retains:

```text
ActorThreadId Id
IntrinsicTimeStrategyWorkflowEntityId EntityId
IntrinsicTimeStrategyWorkflowState? ActiveWorkflow
long TotalStartRequests
long AcceptedStartRequests
long RejectedStartRequests
Guid? LastStartCommandId
Guid? LastTriggerEventId
StrategyWorkflowId? LastRequestedWorkflowId
StrategyWorkflowStartDecision LastStartDecision
DateTime? LastStartRequestedAtUtc
long ReplayedEntityEventCount
long LastPersistedEventId
```

The reducer must handle every workflow-owned event explicitly. Unknown event types return `false` during normal application and must be surfaced by replay tests.

The live state must not retain an unbounded list of historical workflows or start attempts. Complete history belongs in PostgreSQL and ScyllaDB projections.

### 15.1 Snapshot production

The command state exposes an internal method that returns the active immutable workflow snapshot. It must create a new record graph and must not expose the aggregate's mutable collections.

### 15.2 Terminal state

When Completed or Stopped is applied:

- the workflow snapshot becomes terminal and immutable;
- entity-level `ActiveWorkflow` is cleared after retaining the terminal summary needed for validation;
- the next distinct eligible trigger may be accepted.

---

## 16. Workflow Command Actor

### 16.1 Base and context

```csharp
public sealed class IntrinsicTimeStrategyWorkflowCommandActor(
    ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> actorContext)
    : BaseEventSourceCommandActor<IntrinsicTimeStrategyWorkflowCommandActor>(
        actorContext,
        actorContext.Logger)
```

`IIntrinsicTimeStrategyWorkflowCommandContext` extends the closed-generic context and exposes readonly:

```text
IActorSupervisor Supervisor
IEventSourceActorDbContext DbEventSource
IDbContextFactory DbFactory
IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> EventProjector
TimeProvider TimeProvider
ILogger<IntrinsicTimeStrategyWorkflowCommandActor> Logger
```

The concrete context derives from `CommandActorContext` and assigns every dependency through `IsArgumentNull.Set`.

### 16.2 Startup and shutdown

Startup resolves:

```text
IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
```

and starts the conventional event projector. Shutdown stops the projector.

### 16.3 Parsing and dispatch

Use static parse, receive, and validation maps matching current actors.

The actor supports every workflow command listed in section 11. Unknown subjects, verbs, or command types fail with a typed error response.

### 16.4 Start algorithm

```text
validate command and trigger
load entity stream state

if command/trigger is an already processed duplicate:
    no state events
    return success

increment distinct start summary

if an active workflow is Running:
    append StartRejected
    do not change WorkflowRevision
    return success

create immutable workflow revision 1
append StartAccepted
append RegimeDiscoveryStarted
save both in one transaction
```

### 16.5 Complete-stage algorithm

```text
validate active workflow ID
validate current stage
validate InputWorkflowRevision
validate source event identity
validate result envelope and SHA-256

if identical duplicate:
    no-op

if same result identity has different hash:
    append consistency-fault stop

otherwise:
    create next WorkflowRevision
    append ResultRecorded
    evaluate SkeletonProceedOnValidResult/v1
    append ContinuationEvaluated
    append NextStageStarted or WorkflowCompleted
```

### 16.6 Fail-stage algorithm

```text
validate active workflow ID, current stage, and input revision

if identical duplicate:
    no-op

create next WorkflowRevision
append StrategyWorkflowXXXFailedEvent
append IntrinsicTimeStrategyWorkflowStoppedEvent
```

No continuation evaluation or business retry occurs.

### 16.7 Timeout and cancel

Timeout or cancellation applies only when the workflow ID, stage, and expected revision still match. A stale timeout/cancel command is an idempotent no-op with a structured log and metric.

---

## 17. Workflow Event Actor

The current repository publishes committed event-sourced events through the conventional projector and Event actor path. A dedicated workflow Event actor is therefore required for reliable post-commit pipeline dispatch.

This actor is a repository-specific implementation refinement of the three-role conceptual design. It owns no workflow state and makes no continuation decisions.

### 17.1 Responsibilities

- consume committed `RegimeDiscoveryStartedEvent` through `RiskManagementStartedEvent`;
- construct the corresponding `StartXXXPipelineCommand`;
- copy the persisted immutable workflow snapshot and original ITI trigger reference/input;
- use `IntrinsicTimeStrategyPipelineRoutes` to address the pipeline Command actor;
- preserve workflow ID, revision, correlation, causation, and deadlines;
- rely on stable event/command IDs for idempotent redelivery.

### 17.2 Trigger-event availability

The stage-started workflow event must contain the original `FuturesItiSignalGeneratedEvent` or a durable serialized trigger copy required to construct the pipeline command after restart. It is not sufficient for the Event actor to depend on a transient in-memory reference.

To avoid duplicating the trigger inside `WorkflowState`, the stage-started event carries:

```text
WorkflowState
TriggerEvent
```

The resulting pipeline command carries the same two values.

### 17.3 Address ownership

Only `IntrinsicTimeStrategyPipelineRoutes` contains pipeline actor names and verbs. Pipeline actors do not contain workflow topology.

---

## 18. Workflow Realtime Actor

### 18.1 Context

`IIntrinsicTimeStrategyWorkflowRealtimeContext` extends `IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>` and exposes:

```text
TimeProvider
ILogger<IntrinsicTimeStrategyWorkflowRealtimeActor>
```

The existing context already supplies supervisor and send APIs through its base interface.

### 18.2 ITI handler

The ITI handler:

1. validates Daily/Weekly/Monthly eligibility;
2. creates the workflow entity ID;
3. generates the proposed UUIDv7 execution ID;
4. constructs the start command;
5. sends it to the workflow Command actor.

It does not inspect whether another workflow is active. That decision belongs to the command aggregate.

### 18.3 Pipeline-result handlers

Each Completed handler maps one pipeline event to one `CompleteXXXCommand`. Each Failed handler maps to one `FailXXXCommand`.

The Realtime actor copies data without adding a continuation decision.

### 18.4 Exception handling

Use the standard `EventExceptionEvent` path and structured workflow fields. An exception in translation must not be converted into a strategy actor failure because the strategy calculation may already have completed. Transport redelivery remains responsible for another delivery attempt.

---

## 19. Workflow Query Actor

### 19.1 Context

`IIntrinsicTimeStrategyWorkflowQueryContext` extends `IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>` and exposes readonly:

```text
IDbContextFactory DbFactory
IIntrinsicTimeStrategyWorkflowProjectionCache ProjectionCache
ILogger<IntrinsicTimeStrategyWorkflowQueryActor> Logger
```

### 19.2 Query behavior

| Query | Primary source |
| --- | --- |
| Active workflow by entity | immutable cache, then Scylla fallback |
| Workflow by ID | Scylla |
| Start attempts | Scylla |
| Stage state | workflow read model from cache/Scylla |
| Timeline | Scylla |
| Recent workflows | Scylla by entity/day bucket |
| Completed workflows | Scylla status/day bucket |
| Stopped workflows | Scylla status/day bucket |

The cache stores immutable snapshots only. A terminal event removes the active entity entry.

If a caller supplies a minimum workflow revision and neither cache nor Scylla has reached it, return a typed `SnapshotNotReady` service result. Never return a lower revision as current.

### 19.3 No orchestration queries

The command actor does not query pipeline actors to recover data required for continuation. Pipeline Completed events must contain the complete accepted result. Pipeline queries, when added later, are diagnostic only and are addressed through `IntrinsicTimeStrategyPipelineRoutes` by workflow-owned components.

---

## 20. Pipeline Route Catalog

```csharp
public static class IntrinsicTimeStrategyPipelineRoutes
{
    public static ActorMailboxId CommandActor(StrategyWorkflowStage stage) =>
        stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery =>
                new(ActorType.Command, StartRegimeDiscoveryPipelineCommand.Actor),
            StrategyWorkflowStage.MarketCondition =>
                new(ActorType.Command, StartMarketConditionPipelineCommand.Actor),
            StrategyWorkflowStage.TradeSelection =>
                new(ActorType.Command, StartTradeSelectionPipelineCommand.Actor),
            StrategyWorkflowStage.OrderComposition =>
                new(ActorType.Command, StartOrderCompositionPipelineCommand.Actor),
            StrategyWorkflowStage.RiskManagement =>
                new(ActorType.Command, StartRiskManagementPipelineCommand.Actor),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
}
```

Add Query and Realtime source-address methods when concrete pipeline query contracts are introduced. Do not duplicate actor-name strings across workflow handlers.

---

## 21. Event-Source Repository and Projection

### 21.1 State repository

`IntrinsicTimeStrategyWorkflowStateRepository`:

- derives from `BaseEventSourceActorRepository`;
- implements `IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>`;
- loads the complete workflow-entity stream through `LoadStateAsync<TState>`;
- saves through `SaveStateAndDenormalizeEventsAsync`;
- delegates committed events to `IntrinsicTimeStrategyWorkflowEventProjector`.

The initial skeleton does not require snapshots because one timeframe workflow stream is expected to be small during development. Add a snapshot contract only after replay measurements justify it.

### 21.2 Conventional projector

`IntrinsicTimeStrategyWorkflowEventProjector` derives from:

```text
ConventionalEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>
```

Descriptors must:

- publish workflow notification events required by the workflow Event actor;
- upsert the workflow detail projection;
- maintain the active-by-entity projection;
- insert accepted/rejected start decisions;
- append timeline rows;
- maintain by-entity and by-status/day query tables;
- update the immutable projection cache only after the Scylla action succeeds;
- delete active state after Completed or Stopped.

Projection handlers must be idempotent by primary key and source `EventId`.

### 21.3 Existing concurrency limitation

The current `IEventSourceActorDbContext.SaveEventsAsync` API persists a transactional event batch but does not accept an expected stream revision. The skeleton therefore receives process-level single-flight protection from the actor mailbox plus logical `WorkflowRevision` checks.

The implementation must not claim multi-host compare-and-swap safety. Before horizontally scaling the same workflow command actor across hosts, add an expected-revision append API and a PostgreSQL uniqueness/CAS constraint. This platform enhancement is outside the initial workflow-only skeleton.

---

## 22. ScyllaDB Schema

All tables are added to `TradeSchemaCql` and registered in `TradeSchemaDb`. Names are intentionally unversioned.

### 22.1 Workflow detail

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow (
    workflowId uuid,
    workflowEntityId text,
    workflowDefinitionId text,
    workflowDefinitionVersion int,
    contractId text,
    timeFrameStartValueDate date,
    timePeriod text,
    triggerEventId uuid,
    correlationId uuid,
    status text,
    outcome text,
    currentStage text,
    workflowRevision bigint,
    lastEventId bigint,
    stateSchemaVersion int,
    statePayload blob,
    stopReasonCode text,
    startedAtUtc timestamp,
    terminalAtUtc timestamp,
    updatedAtUtc timestamp,
    PRIMARY KEY (workflowId)
);
```

`statePayload` is the MessagePack-serialized `IntrinsicTimeStrategyWorkflowState`. Queryable operational fields remain separate columns.

### 22.2 Active workflow by entity

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_active_by_entity (
    workflowEntityId text,
    workflowId uuid,
    contractId text,
    timeFrameStartValueDate date,
    timePeriod text,
    currentStage text,
    workflowRevision bigint,
    lastEventId bigint,
    stateSchemaVersion int,
    statePayload blob,
    startedAtUtc timestamp,
    updatedAtUtc timestamp,
    PRIMARY KEY (workflowEntityId)
);
```

There is at most one row for one workflow entity. Delete it after a terminal event.

### 22.3 Start attempts by entity

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_start_attempt_by_entity (
    workflowEntityId text,
    requestedAtUtc timestamp,
    requestedWorkflowId uuid,
    decision text,
    activeWorkflowId uuid,
    startCommandId uuid,
    triggerEventId uuid,
    activeStage text,
    reasonCode text,
    sourceEventId bigint,
    PRIMARY KEY ((workflowEntityId), requestedAtUtc, requestedWorkflowId)
) WITH CLUSTERING ORDER BY (requestedAtUtc DESC, requestedWorkflowId DESC);
```

### 22.4 Timeline by workflow

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_timeline_by_workflow (
    workflowId uuid,
    eventId bigint,
    workflowEntityId text,
    workflowRevision bigint,
    stage text,
    eventName text,
    eventSchemaVersion int,
    eventPayload blob,
    occurredAtUtc timestamp,
    PRIMARY KEY ((workflowId), eventId)
) WITH CLUSTERING ORDER BY (eventId ASC);
```

### 22.5 Workflow history by entity

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_by_entity (
    workflowEntityId text,
    startedAtUtc timestamp,
    workflowId uuid,
    status text,
    outcome text,
    currentStage text,
    workflowRevision bigint,
    terminalAtUtc timestamp,
    stopReasonCode text,
    PRIMARY KEY ((workflowEntityId), startedAtUtc, workflowId)
) WITH CLUSTERING ORDER BY (startedAtUtc DESC, workflowId DESC);
```

### 22.6 Workflow history by status/day

```sql
CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_by_status_day (
    status text,
    startedDate date,
    startedAtUtc timestamp,
    workflowId uuid,
    workflowEntityId text,
    outcome text,
    currentStage text,
    workflowRevision bigint,
    terminalAtUtc timestamp,
    stopReasonCode text,
    PRIMARY KEY ((status, startedDate), startedAtUtc, workflowId)
) WITH CLUSTERING ORDER BY (startedAtUtc DESC, workflowId DESC);
```

Status queries require an explicit bounded date range. Do not use `ALLOW FILTERING` or an all-history partition.

---

## 23. TradeDb Changes

### 23.1 Interfaces

Add to `ITradeDbReadContext`:

```text
GetIntrinsicTimeStrategyWorkflowAsync(workflowId)
GetActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId)
GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(workflowEntityId, beforeUtc, pageSize)
GetIntrinsicTimeStrategyWorkflowTimelineAsync(workflowId, afterEventId, pageSize)
GetIntrinsicTimeStrategyWorkflowsByEntityAsync(workflowEntityId, beforeUtc, pageSize)
GetIntrinsicTimeStrategyWorkflowsByStatusAsync(status, startDate, endDate, pageSize)
```

Every method receives a `CancellationToken` overload.

Add to `ITradeDbWriteContext`:

```text
UpsertIntrinsicTimeStrategyWorkflowAsync(readModel)
UpsertActiveIntrinsicTimeStrategyWorkflowAsync(readModel)
DeleteActiveIntrinsicTimeStrategyWorkflowAsync(workflowEntityId)
InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(readModel)
InsertIntrinsicTimeStrategyWorkflowTimelineAsync(readModel)
UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(readModel)
UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(readModel)
```

### 23.2 CQL and parameters

Add named constants to `TradeDbCql` and one `IBindValue` record per parameter set to `TradeDbParameters`.

Every provider call uses a named command:

```csharp
db.Use(
    $"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetIntrinsicTimeStrategyWorkflow)}",
    TradeDbCql.GetIntrinsicTimeStrategyWorkflow)
```

### 23.3 Projection serialization

Use the repository's configured MessagePack serializer to store `statePayload` and timeline `eventPayload`. Do not use runtime type-name serialization or `object` payloads.

On deserialization failure, return a typed projection error and preserve the row for diagnosis/rebuild.

---

## 24. Idempotency

### 24.1 Trigger delivery

The authoritative duplicate key is:

```text
WorkflowDefinitionId + WorkflowEntityId + TriggerEventId
```

The Realtime actor may receive the same event repeatedly. The command aggregate records at most one Accepted/Rejected decision for that trigger ID.

Where compatible with command-audit behavior, use the source trigger event ID as the stable start-command ID. If command and event ID namespaces must remain distinct, add a deterministic trigger-to-command identity helper and test it across restarts.

### 24.2 Pipeline results

```text
same SourceEventId + same ResultId + same PayloadSha256
    -> no-op

same ResultId + different PayloadSha256
    -> ConsistencyFault + workflow stop

different workflow ID, stage, or revision
    -> stale/invalid result; no state advance

result after terminal state
    -> stale no-op with metric and structured log
```

### 24.3 Projection idempotency

Projection rows are keyed so replay overwrites the same detail row or inserts the same timeline/start-attempt key. Projection rebuild must produce the same final state.

---

## 25. Temporary Skeleton Continuation Policy

The skeleton uses:

```text
RuleSetId:      SkeletonProceedOnValidResult
RuleSetVersion: 1
```

Decision:

```text
valid Completed result envelope -> Proceed
invalid result envelope          -> Stop / InvalidStrategyActorResult
Failed event                     -> Stop / StrategyActorFailed
timeout                          -> Stop / StageTimedOut
conflicting duplicate            -> Stop / ConsistencyFault
```

This rule is workflow-owned and versioned. It is not encoded in a pipeline Completed event.

Each future stage implementation replaces only its own continuation evaluator. Removing the skeleton rule does not change the message-routing or persistence architecture.

---

## 26. Feature Availability During Skeleton Development

Until a real Regime Discovery pipeline actor is registered, automatic live workflow creation must be disabled by configuration to avoid creating permanently Running workflows. Route lifecycle remains active independently of this execution switch.

```json
{
  "IntrinsicTimeStrategyWorkflow": {
    "Enabled": false,
    "SkeletonContinuationRuleEnabled": true,
    "MaximumOpaqueResultBytes": 65536
  }
}
```

When `Enabled` is false:

- the workflow actors may start for queries and recovery;
- the workflow Realtime actor still registers the ITI trigger route and all pipeline result routes during startup;
- the workflow Realtime actor still releases those routes during shutdown;
- routed pipeline results remain available for recovery of workflows that were already in flight;
- a routed `FuturesItiSignalGeneratedEvent` is acknowledged and intentionally ignored before command creation;
- no new workflow starts automatically.

BDD and integration tests enable the feature and use scripted test pipeline responders. Do not add production fake strategy actors.

Before enabling the feature in a development runtime, at least the Regime Discovery pipeline actor or a deliberate development harness must be available.

---

## 27. Timeouts

The skeleton defines timeout commands and event contracts but does not invent production durations.

The stage-started event and pipeline start command may contain `ExpectedCompletionAtUtc`.

A durable timeout dispatcher later sends the matching timeout command. The workflow applies it only when:

- the same workflow is active;
- the same stage is current;
- the expected revision matches;
- no Completed/Failed result has been accepted.

The skeleton tests timeout behavior with `TimeProvider` and explicit commands. Do not use wall-clock sleeps.

---

## 28. Observability

### 28.1 Structured log fields

Every workflow log includes when available:

```text
WorkflowDefinitionId
WorkflowEntityId
WorkflowId
WorkflowRevision
Stage
TriggerEventId
MessageId
CommandId
CorrelationId
CausationId
ResultId
ResultPayloadSha256
```

Do not log opaque result payload bytes or the full ITI event payload.

### 28.2 Metrics

```text
strategy_workflow_start_requests_total
strategy_workflow_starts_accepted_total
strategy_workflow_starts_rejected_total
strategy_workflow_active
strategy_workflow_completed_total
strategy_workflow_stopped_total
strategy_workflow_stage_completed_total
strategy_workflow_stage_failed_total
strategy_workflow_stage_timed_out_total
strategy_workflow_duplicate_messages_total
strategy_workflow_stale_results_total
strategy_workflow_consistency_faults_total
strategy_workflow_stage_duration_ms
strategy_workflow_duration_ms
strategy_workflow_projection_lag_ms
```

Never use `WorkflowId`, `EntityId`, contract ID, or trigger ID as metric dimensions.

### 28.3 Tracing

Propagate W3C `traceparent`/`tracestate` through message headers. `StrategyWorkflowId` is a business attribute, not a replacement trace ID.

---

## 29. Validation Rules

### 29.1 Workflow entity

- definition ID equals `IntrinsicTimeStrategyWorkflowDefinition.Id`;
- ITI contract ID is non-empty;
- timeframe start value date is valid;
- timeframe is Daily, Weekly, or Monthly.

### 29.2 Start

- command, trigger, correlation, and causation IDs are non-empty;
- proposed workflow ID is non-empty UUIDv7;
- trigger event entity equals the workflow entity's ITI entity;
- trigger event contains a signal;
- definition version is supported.

### 29.3 Completion

- active workflow exists and is Running;
- workflow ID, stage, and input revision match;
- result identity/type/schema/hash are valid;
- payload size is within configuration;
- payload hash matches the exact payload.

### 29.4 Failure

- active workflow, workflow ID, stage, and input revision match;
- failure code is non-zero;
- failure message is non-empty;
- failure timestamp is valid.

---

## 30. Test Specification

### 30.1 Unit tests

Identity and serialization:

- workflow entity formats deterministically;
- Daily, Weekly, and Monthly produce distinct entity IDs;
- UUIDv7 workflow IDs are non-empty and time ordered;
- all commands/events/queries round-trip through MessagePack;
- established MessagePack keys remain stable;
- result hash validation detects changed payloads.

State reducer:

- Accepted creates revision 1 and starts Regime Discovery;
- Rejected changes attempt summaries but not active workflow revision;
- each stage result creates a new immutable state graph;
- previous stage snapshots remain unchanged;
- stage failure, timeout, cancel, and consistency fault become terminal;
- replay reconstructs the exact final state.

Actors:

- parse maps accept every supported verb;
- invalid subject/name/verb is rejected appropriately;
- context constructors use and expose required readonly dependencies;
- public classes and public members have XML comments.

Realtime routing:

- startup registers the ITI route and ten pipeline-result routes exactly once;
- shutdown removes the exact routes;
- route removal preserves other destinations;
- unsupported ITI timeframes are ignored;
- eligible ITI events create correctly addressed start commands;
- each pipeline event creates the matching workflow command.

Event dispatch:

- each persisted Started event creates exactly one correctly addressed pipeline command;
- commands contain the immutable state, original trigger event, and correct input revision;
- no pipeline actor address appears in another pipeline contract.

Query actor:

- active cache hit;
- Scylla fallback and cache warm;
- minimum-revision `SnapshotNotReady` behavior;
- paged start attempts and timeline;
- completed/stopped date-bucket queries.

### 30.2 BDD scenarios

1. Daily ITI trigger completes all five scripted stages and reaches Completed.
2. Weekly and Monthly workflows run concurrently with Daily for the same contract.
3. A second distinct Daily trigger is rejected while Daily is Running.
4. The same trigger redelivery creates no second decision.
5. Completed Regime Discovery with valid opaque data proceeds.
6. Regime Discovery Failed stops without evaluating continuation.
7. Completed processing plus a Stop continuation remains processing `Completed` and workflow `Stopped`.
8. Stale revision result does not advance state.
9. Conflicting duplicate result stops with `ConsistencyFault`.
10. Timeout stops and a later result becomes a stale no-op.
11. A new trigger is accepted after Completed.
12. A new trigger is accepted after Stopped.

### 30.3 Actor integration tests

- actual NATS subject serialization and routing;
- ITI realtime fan-out reaches both existing consumers and workflow Realtime actor;
- workflow commands partition by workflow entity mailbox;
- committed Started event dispatches a pipeline start command;
- pipeline Completed/Failed realtime fan-in reaches the workflow;
- actor restart reloads PostgreSQL state;
- route registration is released during orderly shutdown.

### 30.4 Storage integration tests

- all six Scylla tables are created through `TradeSchemaDb`;
- workflow detail round-trip preserves opaque state payload;
- active row is upserted and deleted on terminal transition;
- start attempts cluster newest first;
- timeline clusters by ascending event ID;
- entity history and status/day queries are bounded and ordered;
- replaying the same events is idempotent;
- no workflow query requires `ALLOW FILTERING`.

### 30.5 Replay tests

- rebuild an accepted-to-completed five-stage workflow;
- rebuild every stopped outcome;
- rebuild with rejected starts interleaved while active;
- rebuild Scylla projections from PostgreSQL events;
- reconstructed state and projections match the original logical revisions and result hashes.

---

## 31. Implementation Gates

### ITSW-0 - Documentation and baseline

- add this specification and dashed companion design filename;
- capture baseline solution build and relevant test counts;
- verify no existing actor uses the reserved names.

### ITSW-1 - Shared identity and enums

- add workflow definition, entity ID, UUIDv7 execution ID, statuses, stages, outcomes, and decisions;
- add validation and MessagePack tests.

### ITSW-2 - Immutable state and opaque envelopes

- add result, failure, stage-state, and workflow-state records;
- add deep immutability, hashing, payload-limit, and serialization tests.

### ITSW-3 - Workflow messages

- add all workflow commands and workflow-owned events;
- add standard metadata, keys, constructors, XML comments, and round-trip tests.

### ITSW-4 - Pipeline boundary contracts

- add five start commands and ten Completed/Failed events;
- add pipeline route catalog;
- verify pipeline contracts contain no next-stage knowledge.

### ITSW-5 - Event-sourced state and repository

- implement the reducer and state repository;
- test replay, single-flight, duplicate triggers, and terminal behavior.

### ITSW-6 - TradeDb schema and storage

- add unversioned CQL tables, schema registration, named CQL commands, bind parameters, interfaces, and context methods;
- run storage unit and integration tests.

### ITSW-7 - Conventional projector and cache

- project workflow events into all query tables;
- add immutable active cache;
- verify idempotent replay and terminal active-row removal.

### ITSW-8 - Workflow Command actor

- implement closed-generic context, startup/shutdown, parse/receive/validation maps, load/save, transitions, and standard exceptions;
- run command actor unit and BDD tests.

### ITSW-9 - Workflow Event actor

- consume committed Started events;
- dispatch pipeline start commands through the workflow-owned route catalog;
- verify post-commit-only behavior.

### ITSW-10 - Workflow Realtime actor

- implement eleven lifecycle-owned realtime routes;
- translate ITI triggers and pipeline results into workflow commands;
- verify startup rollback and shutdown release.

### ITSW-11 - Workflow Query actor and APIs

- implement query contracts, cache/Scylla handlers, paging, minimum revision, API client/server/NATS maps;
- run query unit and integration tests.

### ITSW-12 - End-to-end skeleton qualification

- add scripted test pipeline responders;
- execute Daily, Weekly, and Monthly concurrent scenarios;
- execute complete/fail/timeout/duplicate/restart/replay scenarios;
- run Trade BDD/unit/integrated tests, application actor integration tests, storage integration tests, and full solution build;
- keep live feature configuration disabled until a real first-stage actor exists.

---

## 32. Definition of Done

The skeleton is complete when:

1. The renamed design and implementation documents are present in the Strategy docs folder.
2. The solution builds with no new warnings.
3. Daily, Weekly, and Monthly triggers route correctly through startup-owned realtime routes.
4. One workflow per workflow entity is enforced.
5. Duplicate delivery creates no additional start decision.
6. Every pipeline receives a readonly state snapshot with all previous accepted results and the original ITI event.
7. No pipeline actor knows another pipeline actor's address.
8. All pipeline results return through workflow-owned realtime routes.
9. The Command actor alone records results and decides continuation.
10. PostgreSQL replay reconstructs the exact workflow state.
11. ScyllaDB projections rebuild deterministically without `ALLOW FILTERING`.
12. Active, history, start-attempt, stage, and timeline queries pass.
13. Completed, Failed, timeout, cancellation, stale, duplicate, and consistency-fault paths pass.
14. All public contracts have XML documentation and MessagePack compatibility tests.
15. Live automatic triggering remains disabled until a real Regime Discovery pipeline implementation is available.

---

## 33. Deferred Design Inputs

The following are append-only extensions and do not block the skeleton:

1. Regime Discovery typed parameters, typed result schema, continuation rules, and timeout.
2. Market Condition typed parameters, typed result schema, continuation rules, and timeout.
3. Trade Selection typed parameters, typed result schema, continuation rules, and timeout.
4. Order Composition typed parameters, typed result schema, continuation rules, and timeout.
5. Risk Management typed parameters, typed result schema, continuation rules, and timeout.
6. Final Order Execution command and risk-approval handoff.
7. Production expected-revision/CAS event-store append support.
8. Production projection retention and status/day bucket retention.
9. Production payload-size tuning and compression policy.

No implementation agent may infer business properties or continuation behavior for these items without an approved stage specification.

---

## 34. Final Implementation Rule

```text
Workflow actor family:
    knows the entire pipeline topology
    owns authoritative state
    owns all continuation decisions
    addresses pipeline commands and queries
    registers all trigger and result return routes

Pipeline actor:
    receives one addressed start command
    receives readonly workflow state and the original ITI event
    sees accepted results from previous stages
    produces one logical Completed or Failed realtime event
    never addresses another pipeline
    never decides workflow continuation

Workflow Completed event:
    is the only future authority for Order Execution handoff
```

The strategy pipeline actors own their calculations and result payloads. The Intrinsic Time Strategy Workflow owns their ordering, routing, persistence, and authority to continue.
