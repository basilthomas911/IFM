# Intrinsic Time Strategy Workflow Implementation

## Implementation Specification v1.0

- **Status:** Initial skeleton implementation specification
- **Date:** 2026-08-25
- **Companion design:** [Intrinsic-Time-Strategy-Workflow-Design-v0.2.md](./Intrinsic-Time-Strategy-Workflow-Design-v0.2.md)
- **Implementation target:** .NET 10, MessagePack, NATS Core/JetStream, PostgreSQL EventSourceDb, and ScyllaDB
- **Root domain:** `TomasAI.IFM.Domain.Trade`

---

## 1. Purpose

This document converts the Intrinsic Time Strategy Workflow design into a repository-specific implementation plan.

The first implementation creates the workflow skeleton only. It provides:

- workflow Command, Realtime, and Query actors, with no workflow or pipeline Event actors in this version;
- a `FuturesItiSignalGeneratedEvent` trigger routed through the existing realtime router;
- one active workflow execution per workflow entity;
- immutable workflow snapshots passed to strategy pipeline actors;
- opaque, versioned pipeline result envelopes;
- pipeline start commands and pipeline Processing/Completed/Failed realtime event contracts;
- ACID transactional PostgreSQL event-sourced state for every workflow and pipeline Command actor;
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
    -> commit StartAccepted + IntrinsicTimeStrategyWorkflowStartedEvent
    -> Workflow EventProjector updates ScyllaDB and publishes Started realtime
    -> IntrinsicTimeStrategyWorkflowRealtimeActor
    -> StartRegimeDiscoveryPipelineCommand
    -> Regime Discovery pipeline actors
    -> RegimeDiscoveryPipelineProcessingEvent
    -> RegimeDiscoveryPipelineCompletedEvent or FailedEvent
    -> realtime router
    -> IntrinsicTimeStrategyWorkflowRealtimeActor
    -> CompleteRegimeDiscoveryCommand or FailRegimeDiscoveryCommand
    -> IntrinsicTimeStrategyWorkflowCommandActor
    -> commit IntrinsicTimeStrategyWorkflowContinuedEvent when another stage is selected
    -> Workflow EventProjector updates ScyllaDB and publishes Continued realtime
    -> IntrinsicTimeStrategyWorkflowRealtimeActor sends the selected StartXXXPipelineCommand
```

This pattern repeats for all five stages.

All realtime inputs are one-way. The Workflow Realtime actor does not reply to a source event. It translates trigger and terminal pipeline events into workflow commands. For a projector-published Started or Continued lifecycle event, it executes the already committed dispatch instruction by sending the selected pipeline start command.

The Workflow Command actor is the orchestration authority: it owns state, continuation, pipeline selection, and the deterministic next command identity. The Workflow Realtime actor is its one-way live-ingress and committed-dispatch adapter, and the Query actor exposes read models. There is no workflow or pipeline Event actor in this version. Pipeline actors are isolated stateful workers:

```text
                         Regime Discovery
                              ^   |
                              |   v
                         Market Condition
                              ^   |
                              |   v
ITI trigger -> Realtime -> Workflow Command <-> Trade Selection
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
8. The workflow Command actor is the sole workflow-state writer, pipeline-selection authority, and continuation authority. The Workflow Realtime actor performs the actual pipeline send only from a projector-published Started or Continued event that contains the committed target.
9. Workflow and pipeline Event actors are not applicable in this version. Command actors reconstruct their own durable state directly from their PostgreSQL event streams.
10. The workflow Realtime actor consumes the ITI trigger, workflow Started/Continued lifecycle events, and pipeline Processing/Completed/Failed realtime events as one-way inputs and sends no realtime reply.
11. The workflow Query actor is side-effect free.
12. Each pipeline start command carries a readonly workflow snapshot and the original ITI event.
13. Each pipeline actor retains its own private durable calculation state; that state is never part of workflow state.
14. Each pipeline completion returns only that stage's complete opaque result and workflow metadata.
15. Each pipeline failure uses the standard application failure-event shape plus workflow metadata.
16. Pipeline result events are routed back through lifecycle-owned realtime routes. Pipeline actors do not hard-code the workflow mailbox.
17. The skeleton continuation rule is `Proceed` after a structurally valid Completed result.
18. Failed, invalid, conflicting, cancelled, or timed-out processing stops the workflow.
19. Every workflow and pipeline Command actor persists its private authoritative state as an ACID transactional event batch in PostgreSQL EventSourceDb and reconstructs that state by replaying its own event log.
20. ScyllaDB table names are unversioned because this is a development schema.
21. Pipeline results before Risk Management are internal strategy calculations and have no authority to affect an external system.
22. A Risk Manager approval result is the only critical strategy output. A future Order Execution handoff may occur only after that approval and the resulting Completed workflow transition have been durably committed.
23. No Order Execution command is sent by the skeleton until its final durable contract is implemented explicitly.
24. Workflow parameters remain immutable for one v1 execution. Portfolio Manager or Advisor actors cannot modify workflow state, continuation, or an in-flight pipeline in this version.

---

## 4. Scope

### 4.1 Included

- shared identifiers, enums, state records, result envelopes, commands, events, queries, and read models;
- actor contexts using the repository's closed-generic context pattern;
- workflow aggregate/state reducer;
- ACID event-source repositories for workflow and pipeline Command actors;
- conventional EventProjector processing of committed workflow events into rebuildable ScyllaDB read models;
- workflow Command, Realtime, and Query actors;
- pipeline address catalog owned by the workflow module;
- startup and shutdown realtime-route lifecycle;
- TradeDb schema, CQL, parameters, read/write APIs, and implementation;
- immutable active-workflow projection cache;
- API/NATS query mapping required to expose the workflow read model;
- XML comments on public classes, methods, properties, and contracts;
- BDD, unit, actor integration, and storage integration tests.

### 4.2 Excluded

- actual pipeline actor calculations;
- concrete pipeline-private state schemas and reducers, which are implemented with their respective pipeline actors;
- stage-specific business parameters;
- typed stage result properties;
- real continuation rules;
- automatic business retries;
- Order Execution dispatch;
- broker operations;
- position monitoring;
- LLM-controlled continuation or risk approval;
- changes to the underlying base actor hierarchy;
- production multi-host event-store compare-and-swap support;
- workflow or pipeline durable Event actors;
- JetStream or other durable `ActorType.Event` replay consumers for strategy workflow or pipeline events;
- Portfolio Manager or Advisor control of workflow parameters, progression, or in-flight pipeline execution;
- a system-wide TraceId architecture or retrofitting TraceId into existing workflow contracts;
- any strategy-stage side effect against an external system before a future Order Execution boundary.

---

## 5. Alignment With Current Repository Conventions

The implementation must follow these existing conventions:

- actors implement the discovered `IActor<TActor>` contracts through the existing base actor classes;
- `TradeActorAssembly.Current` already participates in Simple Injector assembly discovery;
- contexts implement the relevant closed-generic interface such as `ICommandActorContext<TActor>`;
- context dependencies are constructor-injected and assigned with `IsArgumentNull.Set`;
- command actors derive from `BaseEventSourceCommandActor<TActor>`;
- Realtime actors derive from `BaseEventActor<TActor>` and process only non-replayable live inputs;
- query actors derive from `BaseQueryActor<TActor>`;
- state derives from `BaseEventSourceActorState<TState>`;
- repositories implement `IEventSourceActorStateRepository<TState>` and derive from `BaseEventSourceActorRepository`;
- Command actor state is persisted as an ACID transactional PostgreSQL event batch before any next command or realtime result is emitted;
- committed workflow events are passed to `ConventionalEventProjector<TActor>` for idempotent ScyllaDB read-model updates without `ActorType.Event` or JetStream durable replay;
- MessagePack contracts use explicit sequential integer keys and serialization constructors;
- commands use base keys `0..5`, events use base keys `0..7`, and queries begin with keys `0..1`;
- storage commands call `.Use(commandName, commandText)` with a globally clear name such as `$"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)}"`;
- Scylla parameter records implement `IBindValue`;
- schema objects are registered through `TradeSchemaDb` and `SchemaObjectDefinition`;
- realtime routes are added during `OnStartup` and removed during `OnShutdown`.

`ActorType.Event` is not instantiated by the strategy workflow or its pipeline workers in this version. `ActorType.Realtime` is reserved for non-replayable live inputs and sends no reply to its incoming event. Only the Workflow Command actor loads and mutates workflow state; each pipeline Command actor independently owns and reconstructs its private calculation state from its own event log.

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
    RedispatchCurrentStrategyPipelineCommand.cs

Pipeline/Commands/
    StartRegimeDiscoveryPipelineCommand.cs
    StartMarketConditionPipelineCommand.cs
    StartTradeSelectionPipelineCommand.cs
    StartOrderCompositionPipelineCommand.cs
    StartRiskManagementPipelineCommand.cs

Pipeline/Events/
    RegimeDiscoveryPipelineProcessingEvent.cs
    RegimeDiscoveryPipelineCompletedEvent.cs
    RegimeDiscoveryPipelineFailedEvent.cs
    MarketConditionPipelineProcessingEvent.cs
    MarketConditionPipelineCompletedEvent.cs
    MarketConditionPipelineFailedEvent.cs
    TradeSelectionPipelineProcessingEvent.cs
    TradeSelectionPipelineCompletedEvent.cs
    TradeSelectionPipelineFailedEvent.cs
    OrderCompositionPipelineProcessingEvent.cs
    OrderCompositionPipelineCompletedEvent.cs
    OrderCompositionPipelineFailedEvent.cs
    RiskManagementPipelineProcessingEvent.cs
    RiskManagementPipelineCompletedEvent.cs
    RiskManagementPipelineFailedEvent.cs

Events/
    StrategyWorkflowStartAcceptedEvent.cs
    StrategyWorkflowStartRejectedEvent.cs
    IntrinsicTimeStrategyWorkflowStartedEvent.cs
    IntrinsicTimeStrategyWorkflowContinuedEvent.cs
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
public readonly record struct IntrinsicTimeStrategyWorkflowEntityId : IActorEntityId
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

New strategy-workflow entity identities use readonly record structs by convention when their fields can preserve value semantics. This convention applies prospectively and does not require a mass conversion of existing entity-ID record classes. Validators must reject an invalid default struct value before routing or persistence.

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

### 7.5 Stable enum values

The shared workflow enums use explicit numeric values so MessagePack contracts remain stable:

| Enum | Values |
| --- | --- |
| `StrategyWorkflowStartDecision` | `None = 0`, `Accepted = 1`, `Rejected = 2` |
| `StrategyWorkflowStage` | `None = 0`, `RegimeDiscovery = 1`, `MarketCondition = 2`, `TradeSelection = 3`, `OrderComposition = 4`, `RiskManagement = 5` |
| `StrategyWorkflowStatus` | `None = 0`, `Running = 1`, `Completed = 2`, `Stopped = 3` |
| `StrategyWorkflowOutcome` | `None = 0`, `Completed = 1`, `PipelineFailed = 2`, `InvalidResult = 3`, `TimedOut = 4`, `Cancelled = 5`, `ConsistencyFault = 6` |
| `StrategyActorProcessingStatus` | `NotStarted = 0`, `Processing = 1`, `Completed = 2`, `Failed = 3`, `TimedOut = 4`, `Cancelled = 5` |
| `StrategyWorkflowContinuationDecision` | `None = 0`, `Proceed = 1`, `Stop = 2` |

Never renumber or reuse these values after publication. Append new values only.

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

The full original `FuturesItiSignalGeneratedEvent` is not duplicated inside the public workflow snapshot. The private `IntrinsicTimeStrategyWorkflowCommandState` retains the original trigger event from `StrategyWorkflowStartAcceptedEvent` so it is available after workflow replay. Every pipeline start command separately carries that retained original trigger event.

### 8.5 State ownership boundaries

The workflow and every pipeline are separate state owners:

```text
IntrinsicTimeStrategyWorkflowCommandActor
    owns IntrinsicTimeStrategyWorkflowCommandState
    owns the authoritative workflow stage and revision
    retains the original ITI trigger for later pipeline commands
    stores only accepted opaque pipeline results

Each strategy pipeline Command actor
    owns its own private event-sourced calculation state
    owns its stage-specific reducer, repository, and durable events
    receives WorkflowState only as readonly input context
    never exposes its private state to the workflow or another pipeline
```

The `WorkflowState` included in a `StartXXXPipelineCommand` is a deep immutable snapshot, not a shared state object and not a state-transfer mechanism. A pipeline may read it but cannot revise the workflow. The original `FuturesItiSignalGeneratedEvent` is also input context, not pipeline-owned workflow state.

A pipeline Completed event returns the complete opaque result required by the workflow. It does not return the pipeline's private state or a modified workflow snapshot. A Failed event returns failure information only. The Workflow Command actor decides whether either event changes workflow state.

Pipeline-private state contracts are added with each concrete pipeline implementation. They must follow the same command-state, event-source repository, replay, and query/projection conventions without being placed in `IntrinsicTimeStrategyWorkflowState`.

### 8.6 Revision semantics

`WorkflowRevision` advances once per accepted logical workflow transition, not once per event in an atomic event batch.

All workflow events produced by one transition carry the same resulting revision:

```text
revision 3:
    StrategyWorkflowMarketConditionResultRecordedEvent
    StrategyWorkflowMarketConditionContinuationEvaluatedEvent
    IntrinsicTimeStrategyWorkflowContinuedEvent (next stage: Trade Selection)
```

A rejected start does not change `WorkflowRevision`.

---

## 9. Trigger and Realtime Router Lifecycle

### 9.1 Required route set

`IntrinsicTimeStrategyWorkflowRealtimeActor` owns these eighteen routes:

| Source event | Source actor/verb |
| --- | --- |
| `FuturesItiSignalGeneratedEvent` | `FuturesItiSignalGeneratedEvent.Actor` / `.Verb` |
| `IntrinsicTimeStrategyWorkflowStartedEvent` | its `Actor` / `Verb` |
| `IntrinsicTimeStrategyWorkflowContinuedEvent` | its `Actor` / `Verb` |
| `RegimeDiscoveryPipelineProcessingEvent` | its `Actor` / `Verb` |
| `RegimeDiscoveryPipelineCompletedEvent` | its `Actor` / `Verb` |
| `RegimeDiscoveryPipelineFailedEvent` | its `Actor` / `Verb` |
| `MarketConditionPipelineProcessingEvent` | its `Actor` / `Verb` |
| `MarketConditionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `MarketConditionPipelineFailedEvent` | its `Actor` / `Verb` |
| `TradeSelectionPipelineProcessingEvent` | its `Actor` / `Verb` |
| `TradeSelectionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `TradeSelectionPipelineFailedEvent` | its `Actor` / `Verb` |
| `OrderCompositionPipelineProcessingEvent` | its `Actor` / `Verb` |
| `OrderCompositionPipelineCompletedEvent` | its `Actor` / `Verb` |
| `OrderCompositionPipelineFailedEvent` | its `Actor` / `Verb` |
| `RiskManagementPipelineProcessingEvent` | its `Actor` / `Verb` |
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
        IntrinsicTimeStrategyWorkflowStartedEvent.Actor,
        IntrinsicTimeStrategyWorkflowStartedEvent.Verb),
    new(ActorType.Realtime,
        IntrinsicTimeStrategyWorkflowContinuedEvent.Actor,
        IntrinsicTimeStrategyWorkflowContinuedEvent.Verb),
    new(ActorType.Realtime,
        RegimeDiscoveryPipelineProcessingEvent.Actor,
        RegimeDiscoveryPipelineProcessingEvent.Verb),
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

### 9.6 One-way realtime contract

The realtime router delivers events without request/reply semantics. `IntrinsicTimeStrategyWorkflowRealtimeActor.ReceiveAsync` returns `ValueTask` and must never call `ReplyAsync`, create a service reply for the source event, or wait for a realtime response.

Forwarding a translated workflow command does not turn the original event into a request. Any command-level acknowledgment remains internal to the command transport and is not returned to the realtime publisher. Business completion arrives later only as a new Completed or Failed realtime event.

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

The Workflow Realtime actor forwards this command without replying to `FuturesItiSignalGeneratedEvent`.

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

### 11.6 Recovery redispatch

`RedispatchCurrentStrategyPipelineCommand` contains:

```text
WorkflowId
ExpectedWorkflowRevision
ExpectedStage
RequestedAtUtc
RequestedBy
```

The Workflow Command actor accepts this command only for the currently Running stage. It reconstructs and republishes the last committed Started/Continued dispatch instruction with the same deterministic pipeline command ID, without creating a new workflow transition or revision. The Workflow Realtime actor then reissues the same `StartXXXPipelineCommand`. This is the explicit recovery path for an interruption after workflow persistence/projection but before pipeline delivery. It is a command operation and realtime re-publication, not Event-actor replay.

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

The pipeline command does not include the workflow actor address. The pipeline publishes its own Processing event and then one Completed or Failed realtime event; the workflow owns the observation and return routes through `AddRealtimeRouter`.

Each concrete pipeline implementation owns a private command-state type and event-source repository. The readonly `WorkflowState` and `TriggerEvent` fields are inputs to that private state machine; they do not replace it. Pipeline-private intermediate calculations, caches, checkpoints, and durable events are not added to the workflow snapshot.

---

## 13. Pipeline Output Events

### 13.1 Processing event

Each Processing event implements `IEvent<IntrinsicTimeStrategyWorkflowEntityId>` and contains:

```text
base event keys 0..7
8  WorkflowId
9  InputWorkflowRevision
10 CorrelationId
11 CausationId
12 PipelineStage
13 ProcessingAtUtc
```

The future pipeline Command actor commits this event when it accepts its `StartXXXPipelineCommand`. Its conventional EventProjector updates the pipeline's ScyllaDB read model before publishing the Processing event through realtime transport. Processing is an observation of accepted pipeline work; it is not a completion/failure response to the Start command and it does not authorize workflow continuation.

### 13.2 Completed event

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

### 13.3 Failed event

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

### 13.4 Routing

Pipeline output events keep their pipeline source identity:

```text
ActorType.Realtime
{PipelineRealtimeActorName}
{ProcessingCompletedOrFailedVerb}
{WorkflowEntityId.Format()}
```

The workflow Realtime actor receives an additional routed copy because it registered that source `ActorTypeId` during startup.

Processing, Completed, and Failed realtime events are one-way publications. The pipeline Realtime actor does not wait for or receive a workflow reply. Processing is observed without a workflow-state transition. Acceptance, rejection, staleness, and continuation for Completed/Failed results are recorded internally by the Workflow Command actor.

---

## 14. Workflow-Owned Events

### 14.1 Start events

```text
StrategyWorkflowStartAcceptedEvent
StrategyWorkflowStartRejectedEvent
```

Accepted and `IntrinsicTimeStrategyWorkflowStartedEvent` are added to the state in one command transition and persisted in one PostgreSQL transaction. There is no `RegimeDiscoveryStartedEvent`; Regime Discovery is the first pipeline selected by the workflow Started event.

`StrategyWorkflowStartAcceptedEvent` durably contains the original `FuturesItiSignalGeneratedEvent`. The Workflow Command actor replays that field into its private `ActiveTriggerEvent`; the public workflow snapshot does not duplicate it.

Rejected records:

- requested workflow ID;
- active workflow ID;
- trigger and command IDs;
- active stage;
- reason `WorkflowAlreadyExecuting`;
- rejection timestamp.

It does not change the active workflow revision.

### 14.2 Workflow lifecycle dispatch events

```text
IntrinsicTimeStrategyWorkflowStartedEvent
IntrinsicTimeStrategyWorkflowContinuedEvent
```

Started carries the committed Regime Discovery target for a newly accepted workflow. Continued carries the completed stage and the committed next pipeline target after an accepted stage result. Both include the deterministic next command ID, actor type/name, bounded context, immutable workflow snapshot, original ITI trigger, correlation/causation metadata, and requested/deadline timestamps.

The Workflow Command actor selects the target and commits the event but does not perform the normal send. The conventional Workflow EventProjector first updates the ScyllaDB read model and then publishes the committed lifecycle event through realtime transport. The Workflow Realtime actor receives that event and sends the corresponding `StartXXXPipelineCommand`. This preserves Command-actor authority while preventing an uncommitted or unprojected continuation from dispatching work.

### 14.3 Stage event family

Each stage has:

```text
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

There is no stage-specific Started event. The workflow-level Started or Continued event is the authoritative event-log instruction for pipeline selection and dispatch. Pipeline lifecycle names always include `Pipeline`: `XXXPipelineProcessingEvent`, `XXXPipelineCompletedEvent`, and `XXXPipelineFailedEvent`.

### 14.4 Terminal events

```text
IntrinsicTimeStrategyWorkflowCompletedEvent
IntrinsicTimeStrategyWorkflowStoppedEvent
```

Only the workflow Command actor creates terminal workflow events.

The skeleton records `Completed` after a valid Risk Management approval result passes `SkeletonProceedOnValidResult/v1`. That approved result is the only strategy output that may later authorize an external effect. This version durably records the approval and Completed transition but does not issue an Order Execution command.

After successful live projection, the Workflow EventProjector publishes Completed and Stopped as terminal realtime lifecycle observations. They are available to future observers but are not routed back to the Workflow Realtime actor because they contain no next-pipeline instruction.

---

## 15. Workflow Command State

`IntrinsicTimeStrategyWorkflowCommandState` derives from `BaseEventSourceActorState<IntrinsicTimeStrategyWorkflowCommandState>`.

It retains:

```text
ActorThreadId Id
IntrinsicTimeStrategyWorkflowEntityId EntityId
IntrinsicTimeStrategyWorkflowState? ActiveWorkflow
FuturesItiSignalGeneratedEvent? ActiveTriggerEvent
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

`ActiveTriggerEvent` is reconstructed from the accepted start event and retained only by the Workflow Command actor while the workflow is active. It is copied into each pipeline start command and cleared with `ActiveWorkflow` after a terminal transition.

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

Startup resolves the Command actor's state repository and other command-owned dependencies:

```text
IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
```

Startup also starts the conventional `IntrinsicTimeStrategyWorkflowEventProjector`; shutdown stops it. This projector receives committed workflow events for ScyllaDB read-model updates. It does not register an `ActorType.Event` actor or a JetStream durable replay consumer.

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
append IntrinsicTimeStrategyWorkflowStartedEvent with the Regime Discovery target
save both in one transaction
EventProjector updates ScyllaDB and publishes Started realtime
Workflow Realtime sends StartRegimeDiscoveryPipelineCommand from that committed instruction
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
    append IntrinsicTimeStrategyWorkflowContinuedEvent or WorkflowCompleted
    when Continued commits, EventProjector updates ScyllaDB and publishes it realtime
    Workflow Realtime sends StartNextStagePipelineCommand from that committed instruction
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

### 16.8 Pipeline dispatch boundary

The Workflow Command actor is the only actor allowed to select a pipeline and commit its addressing instruction. It creates `IntrinsicTimeStrategyWorkflowStartedEvent` or `IntrinsicTimeStrategyWorkflowContinuedEvent` from the new workflow snapshot, its privately retained original trigger event, and `IntrinsicTimeStrategyPipelineRoutes`.

Dispatch occurs only after the workflow transition is saved, projected to ScyllaDB, and published by the EventProjector. The Workflow Realtime actor converts that committed lifecycle event into the corresponding `StartXXXPipelineCommand`; it does not choose or revise the target. A failed save or projection sends no pipeline command. Stable deterministic pipeline command IDs make reissue safe and allow the receiving pipeline Command actor to deduplicate delivery. The pipeline command response, when required by the command transport, is only an internal delivery acknowledgment; the workflow advances only after a Completed or Failed realtime event is received.

---

## 17. Workflow Event Actor - Not Applicable

No workflow Event actor and no pipeline-worker Event actor is designed or implemented in this version. `ActorType.Event` durable transport replay is unnecessary because these actors do not publish replayable integration events for downstream external effects.

### 17.1 Durable state replay belongs to Command actors

This decision does not remove event sourcing. Every workflow and pipeline Command actor:

1. appends each accepted logical state transition to its own PostgreSQL event log as one ACID transaction;
2. treats that committed event stream as its authoritative durable state;
3. reconstructs its state by replaying the stream through its own reducer; and
4. allows lifecycle/result realtime publication only after the state transaction succeeds; normal pipeline dispatch is then performed by the Workflow Realtime actor from the committed Started/Continued event.

Repository replay into a Command actor state reducer is not `ActorType.Event` message replay and does not require an Event actor.

### 17.2 Realtime processing boundary

`FuturesItiSignalGeneratedEvent`, workflow Started/Continued lifecycle events, and pipeline Processing/Completed/Failed events are one-way realtime inputs processed only by Realtime actors. Trigger and Completed/Failed inputs are translated into commands for the state-owning Command actor; lifecycle events execute committed dispatch instructions; Processing is observed without advancing workflow state. A recovered workflow may safely republish its deterministic lifecycle dispatch instruction, and a pipeline worker that has already committed its result can idempotently re-emit the corresponding realtime result from its durable Command state.

### 17.3 External-effect boundary

Regime Discovery, Market Condition, Trade Selection, and Order Composition results are internal calculations with no authority to change an external system. Risk Manager approval is the sole critical strategy output. A future Order Execution implementation may consume that approval only after the workflow Command actor has durably committed both the approved result and the Completed workflow transition.

Durable Event actors and durable replay processing belong to that future Order Execution design, where order submission, acknowledgement, fill, rejection, cancellation, and reconciliation can affect external systems. They are intentionally outside the current workflow and pipeline-worker architecture.

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

Each Completed handler maps one pipeline event to one `CompleteXXXCommand`. Each Failed handler maps to one `FailXXXCommand`. Each Processing handler validates/logs the accepted pipeline lifecycle observation but does not advance authoritative workflow state.

The Realtime actor copies data without adding a continuation decision.

It does not load workflow command state, does not retain durable workflow state, and does not reply to the pipeline realtime publisher.

### 18.4 Committed-dispatch handlers

The Started and Continued handlers validate the committed target fields, resolve the matching entry from `IntrinsicTimeStrategyPipelineRoutes`, and send the corresponding `StartXXXPipelineCommand` using the event's deterministic command ID, immutable workflow state, and original trigger. They do not choose the pipeline, evaluate continuation, or mutate workflow state.

### 18.5 Exception handling

Use the standard `EventExceptionEvent` path and structured workflow fields. An exception in translation must not be converted into a strategy actor failure because the strategy calculation may already have completed. Transport redelivery remains responsible for another delivery attempt.

### 18.6 One-way receive behavior

All eighteen routed handlers are one-way handlers. Their observable output is a newly addressed workflow command, a committed pipeline-command send, structured logging, or an error event. None calls `ReplyAsync`, and none returns workflow acceptance or continuation information to the realtime source.

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

## 21. Event-Source Repository and ScyllaDB EventProjector

### 21.1 State repository

`IntrinsicTimeStrategyWorkflowStateRepository`:

- derives from `BaseEventSourceActorRepository`;
- implements `IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>`;
- loads the complete workflow-entity stream through `LoadStateAsync<TState>`;
- saves each accepted logical transition as one ACID transactional PostgreSQL event batch;
- returns success only after the authoritative event batch commits; and
- passes committed events to `IntrinsicTimeStrategyWorkflowEventProjector` for ScyllaDB read-model updates and live workflow lifecycle realtime publication without publishing them to an `ActorType.Event` transport.

The initial skeleton does not require snapshots because one timeframe workflow stream is expected to be small during development. Add a snapshot contract only after replay measurements justify it.

### 21.2 Conventional EventProjector without durable message replay

`IntrinsicTimeStrategyWorkflowEventProjector` derives from:

```text
ConventionalEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>
```

The projector processes committed Command event batches and updates the rebuildable ScyllaDB read models. It is an `IEventProjector<TActor>` implementation, but it is not an Event actor and does not consume JetStream or another durable `ActorType.Event` subscription.

Projector descriptors must:

- upsert the workflow detail projection;
- maintain the active-by-entity projection;
- insert accepted and rejected start decisions;
- append timeline rows;
- maintain by-entity and by-status/date query tables;
- update the immutable projection cache only after the ScyllaDB action succeeds; and
- delete active state after Completed or Stopped; and
- after a successful live projection, publish Started/Continued through `ActorType.Realtime` so the Workflow Realtime actor can execute the committed pipeline instruction; and
- publish Completed/Stopped through realtime transport as terminal lifecycle observations without routing them back to the workflow itself.

The projector never publishes `ActorType.Event` messages, selects a strategy pipeline, sends a pipeline command, advances workflow state, or authorizes an external effect. It publishes only workflow lifecycle facts already committed by the Workflow Command actor. Projection handlers remain idempotent by primary key and source `EventId`.

PostgreSQL EventSourceDb remains authoritative. If the process stops after the event transaction commits but before ScyllaDB is updated, there is no automatic durable message replay. An explicit projection catch-up or rebuild rereads the authoritative PostgreSQL event log and reapplies the same idempotent projector handlers. Catch-up/rebuild mode never republishes historical Started/Continued dispatch instructions; after projection recovery, `RedispatchCurrentStrategyPipelineCommand` explicitly republishes only the current deterministic instruction. This event-log projection rebuild is distinct from durable Event-actor transport replay.

Queries must either use a sufficiently current ScyllaDB projection, reconstruct the requested state from EventSourceDb, or report that the rebuildable snapshot is not ready. They must never treat a stale projection as authoritative workflow state.

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

### 24.4 Pipeline command dispatch

Each workflow Started/Continued transition records one deterministic pipeline command ID derived from the workflow ID, stage, and input workflow revision. Initial dispatch and any explicit recovery re-publication use that same ID and identical immutable inputs. The Workflow Realtime actor sends the command from the committed instruction, and the pipeline Command actor deduplicates it before changing private state.

No Event actor participates in reissue. Recovery is initiated through the Workflow Command actor so pipeline selection and addressing remain under the same authority; the Workflow Realtime actor remains the only component that performs the send.

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
- a routed `FuturesItiSignalGeneratedEvent` is intentionally ignored before command creation, with no reply;
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
- the original trigger is retained privately by command state and is restored by replay;
- pipeline-private state never appears in workflow state or pipeline output events;
- stage failure, timeout, cancel, and consistency fault become terminal;
- recovery redispatch creates no state event or workflow revision;
- replay reconstructs the exact final state.

Actors:

- parse maps accept every supported verb;
- invalid subject/name/verb is rejected appropriately;
- context constructors use and expose required readonly dependencies;
- public classes and public members have XML comments.

Realtime routing:

- startup registers the ITI route, two workflow dispatch-lifecycle routes, and fifteen pipeline lifecycle/result routes exactly once;
- shutdown removes the exact routes;
- route removal preserves other destinations;
- unsupported ITI timeframes are ignored;
- eligible ITI events create correctly addressed start commands;
- Started/Continued events send the committed matching pipeline command;
- each Completed/Failed event creates the matching workflow command;
- each Processing event is observed without advancing workflow state;
- no routed realtime handler sends a reply.

Command dispatch:

- the Workflow Command actor selects and persists every pipeline target in a Started/Continued event;
- the EventProjector projects and publishes that committed instruction before the Workflow Realtime actor sends the pipeline command;
- commands contain the immutable state, original trigger event, and correct input revision;
- stable pipeline command IDs make reissue safe;
- no pipeline actor address appears in another pipeline contract.

Event-actor exclusion:

- no workflow or pipeline-worker `ActorType.Event` actor is registered;
- the conventional EventProjector updates ScyllaDB without an Event actor or durable message subscription;
- Command-state reconstruction replays the ACID PostgreSQL event log directly; and
- all live ITI and pipeline-result events enter only through Realtime actors.

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
13. Recovery redispatch after a simulated post-save interruption reuses the same pipeline command ID and creates no workflow revision.

### 30.3 Actor integration tests

- actual NATS subject serialization and routing;
- ITI realtime fan-out reaches both existing consumers and workflow Realtime actor;
- workflow commands partition by workflow entity mailbox;
- a successfully persisted/projected Started/Continued transition is followed by Workflow Realtime actor pipeline dispatch;
- pipeline Processing/Completed/Failed realtime fan-in reaches the workflow;
- realtime ingress sends no reply to the source event;
- actor restart reloads PostgreSQL state;
- explicit recovery redispatch republishes the committed instruction and reissues the current pipeline command without Event-actor involvement;
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

#### ITSW-0 completion record - 2026-08-25

**Status:** Completed

Baseline environment:

| Item | Value |
| --- | --- |
| Repository revision | `55059f3d1e1d5eb872102d105d40ab1e029b96ea` plus the uncommitted ITSW-0 documentation revision |
| .NET SDK | `10.0.302` |
| Solution | `TomasAI.IFM.sln` |
| Configuration | `Debug` |

Build baseline:

```powershell
dotnet build TomasAI.IFM.sln --configuration Debug --no-restore --verbosity minimal --nologo
```

Result: succeeded in `00:01:23.23` with `0` warnings and `0` errors.

Relevant test discovery baseline:

| Suite | Project | Discovered tests |
| --- | --- | ---: |
| Trade Unit | `TomasAI.IFM.Domain.Trade.UnitTests` | 45 |
| Trade BDD | `TomasAI.IFM.Domain.Trade.BDDTests` | 0 |
| Trade Integrated | `TomasAI.IFM.Domain.Trade.IntegratedTests` | 39 |
| Application Actor Integration | `TomasAI.IFM.Application.Actor.IntegrationTests` | 0 |
| Application Storage Integration | `TomasAI.IFM.Application.Storage.IntegrationTests` | 369 |
| **Total** |  | **453** |

Counts were collected with `dotnet test <project> --configuration Debug --no-build --no-restore --list-tests --verbosity quiet --nologo`. Discovery succeeded for all five projects. The two zero-test projects are existing baseline gaps: Trade BDD contains empty test shells, while Application Actor Integration currently contains runtime-host infrastructure without xUnit test methods. Later workflow gates must add executable coverage rather than treating those zeros as qualification.

Reserved-name verification searched non-generated C# and project files for:

```text
IntrinsicTimeStrategyWorkflowCommandActor
IntrinsicTimeStrategyWorkflowRealtimeActor
IntrinsicTimeStrategyWorkflowQueryActor
RegimeDiscoveryPipeline
MarketConditionPipeline
TradeSelectionPipeline
OrderCompositionPipeline
RiskManagementPipeline
```

No implemented actor, pipeline type, or conflicting contract uses a reserved name. The existing `IntrinsicTimeStrategyWorkflowCommandActor.cs` file is an intentional empty placeholder and contains no declaration.

The dashed design document and this implementation specification are both present in `TomasAI.IFM.Domain.Trade/Strategy/Docs`. These results satisfy ITSW-0; no workflow runtime code was introduced by this gate.

### ITSW-1 - Shared identity and enums

- add workflow definition, entity ID, UUIDv7 execution ID, statuses, stages, outcomes, and decisions;
- add validation and MessagePack tests.

#### ITSW-1 completion record - 2026-08-25

**Status:** Completed

Implemented in `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime`:

- stable `IntrinsicTimeStrategyWorkflowDefinition` identity and version;
- readonly record-struct MessagePack `IntrinsicTimeStrategyWorkflowEntityId` using the complete `FuturesItiSignalEntityId` routing identity;
- entity validation for the fixed definition, contract ID, timeframe start date, and Daily/Weekly/Monthly eligibility;
- readonly MessagePack `StrategyWorkflowId` with UUIDv7 generation, parsing, compact formatting, and validation;
- explicit stable numeric values for all six workflow enums listed in section 7.5;
- XML comments on every new public type and member.

Validation evidence:

| Check | Result |
| --- | --- |
| Trade unit tests | `64` passed, `0` failed, `0` skipped |
| New ITSW-1 test cases | `19` |
| Full solution build | succeeded with `0` warnings and `0` errors |
| `git diff --check` | passed |

Both value-type identity validators implement `IValidationStructRules<T>` and return the standard `ValidationError[]` shape through `BaseValidationRules`. The existing `IValidationRules<T>` remains unchanged for reference-type contracts.

### ITSW-2 - Immutable state and opaque envelopes

- add result, failure, stage-state, and workflow-state records;
- add deep immutability, hashing, payload-limit, and serialization tests.

#### ITSW-2 completion record - 2026-08-25

**Status:** Completed

Implemented in `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Model`:

- MessagePack `StrategyStageResultEnvelope` with stable keyed metadata, an opaque payload, canonical SHA-256 calculation, digest verification, and a default configurable 64-KiB payload limit;
- `StrategyStageResultEnvelopeValidationRules` covering required metadata, positive schema versions, non-empty payloads, configured payload limits, and payload digest integrity;
- MessagePack `StrategyPipelineFailure`, `StrategyWorkflowStageState`, and `IntrinsicTimeStrategyWorkflowState` records with XML comments on all public types and members;
- defensive copies on both assignment and access for opaque payload bytes and continuation reason-code arrays;
- a public workflow snapshot that contains only workflow-owned stage state and accepted opaque results, without the original ITI trigger event or pipeline-private state.

Validation evidence:

| Check | Result |
| --- | --- |
| Trade unit tests | `75` passed, `0` failed, `0` skipped |
| New ITSW-2 test cases | `11` |
| Full solution build | succeeded with `0` warnings and `0` errors |
| MessagePack coverage | result envelope, pipeline failure, stage state through the full snapshot, and complete workflow snapshot round trips passed |
| Immutability coverage | source-buffer and exposed-buffer mutation attempts did not change stored state |

The full solution build was serialized with `--maxcpucount:1` to avoid the repository's existing parallel native CMake generation race. No workflow or pipeline actor behavior was introduced by this gate.

### ITSW-3 - Workflow messages

- add all workflow commands and workflow-owned events;
- add standard metadata, keys, constructors, XML comments, and round-trip tests.

#### ITSW-3 completion record - 2026-08-25

**Status:** Completed

Implemented in `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime`:

- all `18` workflow commands: start, five stage completions, five stage failures, five stage timeouts, cancellation, and deterministic recovery redispatch;
- all `26` workflow-owned event-log contracts: two start decisions, two workflow dispatch-lifecycle events, five four-event stage families, and two terminal events;
- established command metadata at MessagePack keys `0..5`, event metadata at keys `0..7`, and sequential custom payload keys;
- full keyed serialization constructors in exact key order and XML comments on every new public type, constant, property, and constructor;
- six appended strategy bounded-context routes for workflow orchestration and the five future pipeline workers;
- defensive copying of continuation reason-code arrays; and
- explicit contract remarks that workflow-owned events are persisted for Command-state reconstruction and ScyllaDB EventProjector processing, not routed to a durable Event actor.

Validation evidence:

| Check | Result |
| --- | --- |
| Contract inventory | `18` commands and `26` workflow-owned events |
| New ITSW-3 test cases | `9` |
| Trade unit tests | `84` passed, `0` failed, `0` skipped |
| MessagePack coverage | all `44` populated contracts round-tripped with stable serialized bytes |
| Key and constructor coverage | all contracts have unique sequential keys and serialization parameters in exact key order |
| Full solution build | succeeded with `0` warnings and `0` errors |
| `git diff --check` | passed |

The full solution build was serialized with `--maxcpucount:1` to avoid the repository's existing parallel native CMake generation race. This gate adds only contracts and tests; it does not implement the Command actor, EventProjector, Realtime actor, Query actor, or any durable Event actor.

### ITSW-4 - Pipeline boundary contracts

- add five start commands and fifteen Processing/Completed/Failed events;
- add pipeline route catalog;
- verify pipeline contracts contain no next-stage knowledge or private pipeline state.

#### ITSW-4 completion record - 2026-08-25

**Status:** Completed

Implemented in `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime`:

- five actor-specific `StartXXXPipelineCommand` contracts carrying the immutable workflow state, original ITI trigger, input revision, correlation/causation identities, and timing metadata;
- fifteen pipeline lifecycle contracts: one Processing, Completed, and Failed event for each of the five stages;
- an ordered `IntrinsicTimeStrategyPipelineRoutes` catalog containing the Command and Realtime actor identities and bounded context for every stage;
- workflow-level Started and Continued dispatch contracts in place of incorrect stage-level `XXXStartedEvent` contracts; and
- XML comments, stable MessagePack keys, full serialization constructors, and boundary tests proving that pipeline contracts contain neither next-stage knowledge nor private pipeline state.

The contracts intentionally add no `TraceId`; ITSW-13 governs that system-wide design before any concrete pipeline actor is implemented. This gate implements only the workflow/pipeline boundary and does not implement a pipeline actor, pipeline state repository, or pipeline EventProjector.

Validation evidence:

| Check | Result |
| --- | --- |
| Contract inventory | `5` pipeline start commands and `15` Processing/Completed/Failed events |
| New ITSW-4 test cases | `7` |
| Trade unit tests | `91` passed, `0` failed, `0` skipped |
| Route coverage | all five stages are present once and map to non-Undefined bounded contexts |
| Serialization and interface coverage | sequential keys, constructor order, populated round trips, and event interface shapes passed |
| Full solution build | succeeded with `0` warnings and `0` errors |
| `git diff --check` | passed |

### ITSW-5 - Event-sourced state and repository

- implement the reducer and state repository;
- test replay, single-flight, duplicate triggers, and terminal behavior.

### ITSW-6 - TradeDb schema and storage

- add unversioned CQL tables, schema registration, named CQL commands, bind parameters, interfaces, and context methods;
- run storage unit and integration tests.

### ITSW-7 - Conventional EventProjector and cache without durable message replay

- implement `IntrinsicTimeStrategyWorkflowEventProjector` for committed-event projection into all rebuildable ScyllaDB query tables;
- publish live Started/Continued dispatch instructions and Completed/Stopped terminal observations only after their ScyllaDB projection succeeds;
- add the immutable active cache and EventSourceDb fallback/catch-up/rebuild path;
- verify idempotent projection, failed-write behavior, explicit event-log rebuild without historical dispatch publication, and terminal active-row removal; and
- verify that the projector creates no `ActorType.Event` actor, JetStream durable consumer, or pipeline dispatch path.

### ITSW-8 - Workflow Command actor

- implement closed-generic context, startup/shutdown, parse/receive/validation maps, load/save, transitions, committed Started/Continued dispatch instructions, and standard exceptions;
- retain the original trigger in private command state and generate immutable pipeline inputs;
- implement deterministic recovery redispatch without a new state transition;
- run command actor unit and BDD tests.

### ITSW-9 - Workflow durable Event actor - Not applicable

**Status:** Not applicable for the strategy workflow and its pipeline workers in this version.

All workflow and pipeline Command actors already obtain durable state persistence and reconstruction from their own ACID transactional PostgreSQL event logs. The strategy stages produce internal calculations only; they do not perform external side effects and therefore do not require replayable `ActorType.Event` delivery or a durable message consumer. The conventional EventProjector remains required solely to update rebuildable ScyllaDB read models from committed Command events.

Risk Manager approval is the only critical strategy output. It becomes eligible for a future Order Execution handoff only after the approval result and Completed workflow transition commit successfully. Durable Event actors are deferred to the separate Order Execution design, where replayable processing is required for externally consequential order lifecycle operations.

ITSW-9 therefore introduces no Event actor, Event-actor context, Event extension, route, or durable consumer. It does not remove or replace the conventional ScyllaDB EventProjector implemented by ITSW-7. Qualification confirms that Command-state replay, ScyllaDB projection, and realtime event ingress remain three separate concerns.

### ITSW-10 - Workflow Realtime actor

- implement eighteen lifecycle-owned realtime routes;
- translate ITI triggers and pipeline Completed/Failed results into workflow commands, observe Processing events, and send pipeline commands from projector-published Started/Continued instructions;
- verify startup rollback, shutdown release, stateless handling, and no replies.

### ITSW-11 - Workflow Query actor and APIs

- implement query contracts, cache/Scylla handlers, paging, minimum revision, API client/server/NATS maps;
- run query unit and integration tests.

### ITSW-12 - End-to-end skeleton qualification

- add scripted test pipeline responders;
- execute Daily, Weekly, and Monthly concurrent scenarios;
- execute complete/fail/timeout/duplicate/restart/replay scenarios;
- run Trade BDD/unit/integrated tests, application actor integration tests, storage integration tests, and full solution build;
- keep live feature configuration disabled until a real first-stage actor exists.

### ITSW-13 - System-wide TraceId architecture design checkpoint

**Timing:** Required after all Strategy Workflow actors and their skeleton qualification are complete, and before implementation of any concrete strategy pipeline actor.

- inventory important workflows that require end-to-end trace identity beyond the Strategy Workflow;
- define TraceId creation, propagation, continuation, storage, logging, telemetry, query, and retention semantics;
- distinguish TraceId from `CorrelationId`, `CausationId`, workflow identity, command identity, event identity, and distributed activity/span identity;
- define MessagePack evolution and compatibility for existing commands, events, event logs, ScyllaDB read models, NATS subjects/envelopes, and external boundaries;
- determine how Portfolio Manager, Advisor, future Order Execution, and other important workflows join or create traces; and
- produce an approved system-wide design and incremental migration plan before pipeline contracts acquire TraceId fields.

This is a design gate, not permission to infer or add TraceId fields during v1 workflow implementation. Strategy Workflow remains the primary use case, but the resulting architecture must be system-wide and reusable.

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
10. The Command actor alone selects and commits the next pipeline target; the Workflow Realtime actor alone sends its command from a successfully projected and published Started/Continued instruction.
11. Every pipeline retains private durable state that is absent from workflow snapshots and output events.
12. No workflow or pipeline Event actor or durable message consumer is registered; the conventional EventProjector updates rebuildable ScyllaDB read models and publishes live committed Started/Continued/Completed/Stopped lifecycle events through realtime transport.
13. Realtime actors process one-way events and send no replies.
14. PostgreSQL replay reconstructs the exact workflow state.
15. ScyllaDB projections rebuild deterministically without `ALLOW FILTERING`.
16. Active, history, start-attempt, stage, and timeline queries pass.
17. Completed, Failed, timeout, cancellation, stale, duplicate, and consistency-fault paths pass.
18. All public contracts have XML documentation and MessagePack compatibility tests.
19. Live automatic triggering remains disabled until a real Regime Discovery pipeline implementation is available.

---

## 33. Deferred Design Inputs

The following are append-only extensions and do not block the skeleton:

1. Regime Discovery typed parameters, typed result schema, continuation rules, and timeout.
2. Market Condition typed parameters, typed result schema, continuation rules, and timeout.
3. Trade Selection typed parameters, typed result schema, continuation rules, and timeout.
4. Order Composition typed parameters, typed result schema, continuation rules, and timeout.
5. Risk Management typed parameters, typed result schema, continuation rules, and timeout.
6. Final Order Execution command and durable handoff of the committed Risk Manager approval after workflow completion.
7. Production expected-revision/CAS event-store append support.
8. Production projection retention and status/day bucket retention.
9. Production payload-size tuning and compression policy.
10. Portfolio Manager observation and versioned workflow parameter or progression commands.
11. Advisor recommendations, constraints, approval authority, and their relationship to Portfolio Manager decisions.
12. System-wide TraceId architecture and migration, governed by the mandatory ITSW-13 design checkpoint before concrete pipeline actors are implemented.

No implementation agent may infer business properties or continuation behavior for these items without an approved stage specification.

### 33.1 Post-v1 Portfolio Manager and Advisor governance

A future Portfolio Manager actor may hold the broad portfolio and changing-market view needed to determine whether an active or future Strategy Workflow should use revised parameters, priority, capital allocation, exposure limits, or progression policy. Advisor input may later provide recommendations, constraints, or approval evidence to that Portfolio Manager decision process. Their exact authority and interaction remain deliberately undefined.

Neither actor may directly mutate workflow state, write workflow projections, or address a strategy pipeline. Any post-v1 influence must enter through an explicit, versioned command to the Workflow Command actor. The Workflow Command actor remains responsible for authorization, expected-revision validation, persistence, continuation, and pipeline selection.

An accepted change must be appended to the authoritative PostgreSQL workflow event log with the requesting actor, reason, correlation and causation identities, parameter-set identity and version, and effective workflow boundary. The default design preference is to apply an accepted change at the next pipeline boundary. A pipeline already processing retains its immutable input unless a separately designed cancel, restart, or update contract explicitly permits otherwise.

Started and Continued workflow lifecycle events may later append the effective parameter-set identity and version. No placeholder MessagePack keys or inferred portfolio/advisor business fields are added in v1; append-only contract evolution will add them after the governance model is approved.

---

## 34. Final Implementation Rule

```text
Workflow Command actor:
    knows the entire pipeline topology
    owns authoritative state
    owns all continuation decisions
    selects and commits every next pipeline target
    publishes no uncommitted dispatch instruction

Workflow Realtime actor:
    registers all trigger, dispatch-lifecycle, and pipeline lifecycle/result routes
    converts trigger and Completed/Failed realtime events into workflow commands
    observes Processing without advancing workflow state
    sends StartXXXPipelineCommand only from a committed Started/Continued instruction
    owns no workflow state
    sends no reply to realtime publishers

Pipeline actor:
    receives one addressed start command
    receives readonly workflow state and the original ITI event
    owns and retains its own private durable state
    sees accepted results from previous stages
    produces one Processing event and one logical Completed or Failed realtime event
    receives no reply to that realtime event
    never addresses another pipeline
    never decides workflow continuation

Risk Manager approval result:
    is the only critical strategy output
    cannot affect an external system until durably committed by the Workflow Command actor
    may authorize Order Execution only after the workflow Completed transition is committed

Workflow and pipeline Command event logs:
    persist accepted state transitions as ACID PostgreSQL transactions
    are authoritative for durable state reconstruction
    do not require an Event actor or durable message replay

Workflow EventProjector:
    receives committed workflow events from the Command repository
    updates rebuildable ScyllaDB read models and the projection cache
    publishes committed Started/Continued lifecycle instructions after projection
    publishes committed Completed/Stopped terminal lifecycle observations after projection
    has no ActorType.Event actor or JetStream durable consumer
    never sends a pipeline command or authorizes an external effect
```

The strategy pipeline actors own their calculations and result payloads. The Intrinsic Time Strategy Workflow owns their ordering, routing, persistence, and authority to continue.
