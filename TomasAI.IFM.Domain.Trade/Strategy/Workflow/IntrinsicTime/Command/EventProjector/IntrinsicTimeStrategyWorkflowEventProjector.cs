using System.Collections.Concurrent;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector;

/// <summary>
/// Projects committed workflow Command events into rebuildable Scylla read models without a durable Event actor.
/// </summary>
/// <remarks>
/// Every descriptor uses the process-local projector queue. Started, Continued, Completed, and Stopped are published
/// to the Workflow Realtime actor only after all Scylla mutations and cache changes succeed. Rebuild mode suppresses
/// lifecycle publication so historical dispatch instructions cannot restart pipelines.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowEventProjector
    : ConventionalEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>
{
    const int StateSchemaVersion = 1;
    const int EventSchemaVersion = 1;
    const string RealtimeActorName = "IntrinsicTimeStrategyWorkflowRealtime";

    readonly ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> _actorContext;
    readonly IIntrinsicTimeStrategyWorkflowProjectionCache _cache;
    readonly ConcurrentDictionary<string, IntrinsicTimeStrategyWorkflowCommandState> _states =
        new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, SemaphoreSlim> _entityLocks = new(StringComparer.Ordinal);
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    /// <summary>Initializes the non-durable conventional workflow projector.</summary>
    public IntrinsicTimeStrategyWorkflowEventProjector(
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> actorContext,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(
            actorContext.DurableReplayQueue,
            actorContext.DbEventSource,
            actorContext.BlackboardService,
            actorContext.Logger,
            reliabilityOptions)
    {
        _actorContext = actorContext;
        _cache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        _descriptors =
        [
            Describe<StrategyWorkflowStartAcceptedEvent>(),
            Describe<StrategyWorkflowStartRejectedEvent>(),
            Describe<IntrinsicTimeStrategyWorkflowStartedEvent>(),
            Describe<IntrinsicTimeStrategyWorkflowContinuedEvent>(),
            Describe<StrategyWorkflowRegimeDiscoveryResultRecordedEvent>(),
            Describe<StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent>(),
            Describe<StrategyWorkflowRegimeDiscoveryFailedEvent>(),
            Describe<StrategyWorkflowRegimeDiscoveryTimedOutEvent>(),
            Describe<StrategyWorkflowMarketConditionResultRecordedEvent>(),
            Describe<StrategyWorkflowMarketConditionContinuationEvaluatedEvent>(),
            Describe<StrategyWorkflowMarketConditionFailedEvent>(),
            Describe<StrategyWorkflowMarketConditionTimedOutEvent>(),
            Describe<StrategyWorkflowTradeSelectionResultRecordedEvent>(),
            Describe<StrategyWorkflowTradeSelectionContinuationEvaluatedEvent>(),
            Describe<StrategyWorkflowTradeSelectionFailedEvent>(),
            Describe<StrategyWorkflowTradeSelectionTimedOutEvent>(),
            Describe<StrategyWorkflowOrderCompositionResultRecordedEvent>(),
            Describe<StrategyWorkflowOrderCompositionContinuationEvaluatedEvent>(),
            Describe<StrategyWorkflowOrderCompositionFailedEvent>(),
            Describe<StrategyWorkflowOrderCompositionTimedOutEvent>(),
            Describe<StrategyWorkflowRiskManagementResultRecordedEvent>(),
            Describe<StrategyWorkflowRiskManagementContinuationEvaluatedEvent>(),
            Describe<StrategyWorkflowRiskManagementFailedEvent>(),
            Describe<StrategyWorkflowRiskManagementTimedOutEvent>(),
            Describe<IntrinsicTimeStrategyWorkflowCompletedEvent>(),
            Describe<IntrinsicTimeStrategyWorkflowStoppedEvent>()
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes
        => _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();

    /// <summary>Reapplies authoritative event-log events without publishing historical lifecycle instructions.</summary>
    public async ValueTask RebuildAsync(
        IEnumerable<IEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        _states.Clear();
        _cache.Clear();
        foreach (var domainEvent in events.OrderBy(static value => value.EventId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProjectAsync(domainEvent, publishLifecycle: false, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Republishes the last committed dispatch instruction for explicit recovery without changing state.</summary>
    /// <remarks>
    /// This path does not project or persist a new event. It recreates only the realtime lifecycle envelope around
    /// the immutable dispatch instruction recovered from the authoritative PostgreSQL event stream.
    /// </remarks>
    internal async ValueTask RepublishCommittedDispatchAsync(
        IntrinsicTimeStrategyWorkflowCommandState state,
        RedispatchCurrentStrategyPipelineCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        var instruction = state.ActiveDispatchInstruction
            ?? throw new InvalidOperationException("The active workflow has no committed pipeline dispatch instruction.");
        var workflow = state.ActiveWorkflow
            ?? throw new InvalidOperationException("A terminal workflow cannot redispatch a pipeline command.");

        IEvent lifecycle = instruction.Stage == StrategyWorkflowStage.RegimeDiscovery && workflow.WorkflowRevision == 1
            ? new IntrinsicTimeStrategyWorkflowStartedEvent
            {
                Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowStartedEvent.Verb, state.EntityId),
                Id = Guid.NewGuid(),
                EntityId = state.EntityId,
                CommandId = command.CommandId,
                ReceivedOn = command.RequestedAtUtc,
                WorkflowId = workflow.WorkflowId,
                WorkflowRevision = workflow.WorkflowRevision,
                CorrelationId = workflow.CorrelationId,
                CausationId = instruction.TriggerEvent.Id,
                NextPipelineStage = instruction.Stage,
                NextPipelineActorType = instruction.ActorType,
                NextPipelineActorName = instruction.ActorName,
                NextPipelineBoundedContext = instruction.BoundedContext,
                NextPipelineCommandId = instruction.CommandId,
                WorkflowState = instruction.WorkflowState,
                TriggerEvent = instruction.TriggerEvent,
                RequestedAtUtc = instruction.RequestedAtUtc,
                ExpectedCompletionAtUtc = instruction.ExpectedCompletionAtUtc,
                StartedAtUtc = instruction.RequestedAtUtc
            }
            : new IntrinsicTimeStrategyWorkflowContinuedEvent
            {
                Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowContinuedEvent.Verb, state.EntityId),
                Id = Guid.NewGuid(),
                EntityId = state.EntityId,
                CommandId = command.CommandId,
                ReceivedOn = command.RequestedAtUtc,
                WorkflowId = workflow.WorkflowId,
                WorkflowRevision = workflow.WorkflowRevision,
                CorrelationId = workflow.CorrelationId,
                CausationId = command.CommandId,
                CompletedPipelineStage = PreviousStage(instruction.Stage),
                NextPipelineStage = instruction.Stage,
                NextPipelineActorType = instruction.ActorType,
                NextPipelineActorName = instruction.ActorName,
                NextPipelineBoundedContext = instruction.BoundedContext,
                NextPipelineCommandId = instruction.CommandId,
                WorkflowState = instruction.WorkflowState,
                TriggerEvent = instruction.TriggerEvent,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = [],
                RequestedAtUtc = instruction.RequestedAtUtc,
                ExpectedCompletionAtUtc = instruction.ExpectedCompletionAtUtc,
                ContinuedAtUtc = instruction.RequestedAtUtc
            };

        await PublishLifecycleAsync(lifecycle, cancellationToken).ConfigureAwait(false);
    }

    EventProjectionDescriptor Describe<TEvent>()
        where TEvent : class, IEvent<IntrinsicTimeStrategyWorkflowEntityId>
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await ProjectAsync(domainEvent, publishLifecycle: true, CancellationToken.None).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            publishProcessingEvent: false,
            useDurableReplay: false,
            publishTerminalEvent: false);

    async ValueTask ProjectAsync(
        IEvent domainEvent,
        bool publishLifecycle,
        CancellationToken cancellationToken)
    {
        if (domainEvent is not IEvent<IntrinsicTimeStrategyWorkflowEntityId> workflowEvent)
            throw new InvalidOperationException($"Unsupported workflow projection event {domainEvent.GetType().FullName}.");

        var entityKey = workflowEvent.EntityId.Format();
        var entityLock = _entityLocks.GetOrAdd(entityKey, static _ => new SemaphoreSlim(1, 1));
        await entityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await ResolveStateAsync(entityKey, domainEvent, cancellationToken).ConfigureAwait(false);
            if (!state.Apply(domainEvent, addEvent: false))
                throw new InvalidOperationException($"Workflow projection reducer rejected {domainEvent.GetType().Name}.");

            await InsertTimelineAsync(workflowEvent, cancellationToken).ConfigureAwait(false);
            await InsertStartAttemptAsync(domainEvent, cancellationToken).ConfigureAwait(false);

            var workflow = state.LatestWorkflow;
            if (workflow is not null)
                await UpsertWorkflowAsync(workflow, domainEvent.EventId, domainEvent.ReceivedOn, cancellationToken)
                    .ConfigureAwait(false);

            if (publishLifecycle)
                await PublishLifecycleAsync(domainEvent, cancellationToken).ConfigureAwait(false);

            if (workflow is { Status: not StrategyWorkflowStatus.Running })
                _states.TryRemove(entityKey, out _);
        }
        finally
        {
            entityLock.Release();
        }
    }

    async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> ResolveStateAsync(
        string entityKey,
        IEvent domainEvent,
        CancellationToken cancellationToken)
    {
        if (_states.TryGetValue(entityKey, out var existingState))
            return existingState;

        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        var workflowId = GetProjectionWorkflowId(domainEvent);
        if (workflowId is { } id && id.Value != Guid.Empty)
        {
            var readModel = await _actorContext.DbFactory.TradeDb
                .GetIntrinsicTimeStrategyWorkflowAsync(id, cancellationToken)
                .ConfigureAwait(false);
            if (readModel is not null && !readModel.StatePayload.IsEmpty)
            {
                var workflow = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowState>(
                    readModel.StatePayload);
                state.RestoreProjectionSnapshot(workflow, readModel.LastEventId);
            }
        }

        return _states.GetOrAdd(entityKey, state);
    }

    async ValueTask UpsertWorkflowAsync(
        IntrinsicTimeStrategyWorkflowState workflow,
        long eventId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = MessagePackSerializer.Serialize(workflow);
        var entity = workflow.EntityId;
        var iti = entity.ItiSignalEntityId;
        var updatedAtUtc = occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc;
        var detail = new IntrinsicTimeStrategyWorkflowReadModel(
            workflow.WorkflowId,
            entity.Format(),
            entity.WorkflowDefinitionId,
            workflow.WorkflowDefinitionVersion,
            iti.ContractId,
            iti.TimeFrameStartValueDate,
            iti.TimePeriod,
            workflow.TriggerEventId,
            workflow.CorrelationId,
            workflow.Status,
            workflow.Outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            eventId,
            StateSchemaVersion,
            payload,
            workflow.StopReasonCode,
            workflow.StartedAtUtc,
            workflow.TerminalAtUtc,
            updatedAtUtc);
        var history = new IntrinsicTimeStrategyWorkflowHistoryReadModel(
            entity.Format(),
            workflow.StartedAtUtc,
            workflow.WorkflowId,
            workflow.Status,
            workflow.Outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            workflow.TerminalAtUtc,
            workflow.StopReasonCode);

        await _actorContext.DbFactory.TradeDb
            .UpsertIntrinsicTimeStrategyWorkflowAsync(detail, cancellationToken)
            .ConfigureAwait(false);
        await _actorContext.DbFactory.TradeDb
            .UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(history, cancellationToken)
            .ConfigureAwait(false);
        await _actorContext.DbFactory.TradeDb
            .UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(history, cancellationToken)
            .ConfigureAwait(false);

        if (workflow.Status == StrategyWorkflowStatus.Running)
        {
            var active = new ActiveIntrinsicTimeStrategyWorkflowReadModel(
                entity.Format(),
                workflow.WorkflowId,
                iti.ContractId,
                iti.TimeFrameStartValueDate,
                iti.TimePeriod,
                workflow.CurrentStage,
                workflow.WorkflowRevision,
                eventId,
                StateSchemaVersion,
                payload,
                workflow.StartedAtUtc,
                updatedAtUtc);
            await _actorContext.DbFactory.TradeDb
                .UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active, cancellationToken)
                .ConfigureAwait(false);
            _cache.Set(active);
        }
        else
        {
            await _actorContext.DbFactory.TradeDb
                .DeleteActiveIntrinsicTimeStrategyWorkflowAsync(entity.Format(), cancellationToken)
                .ConfigureAwait(false);
            _cache.Remove(entity.Format());
        }
    }

    async ValueTask InsertTimelineAsync(
        IEvent<IntrinsicTimeStrategyWorkflowEntityId> domainEvent,
        CancellationToken cancellationToken)
    {
        var workflowId = GetProjectionWorkflowId(domainEvent) ?? default;
        var revision = GetLongProperty(domainEvent, nameof(IntrinsicTimeStrategyWorkflowState.WorkflowRevision));
        var stage = GetStage(domainEvent);
        var payload = MessagePackSerializer.Serialize(domainEvent.GetType(), domainEvent);
        await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
            new IntrinsicTimeStrategyWorkflowTimelineReadModel(
                workflowId,
                domainEvent.EventId,
                domainEvent.EntityId.Format(),
                revision,
                stage,
                domainEvent.EventName,
                EventSchemaVersion,
                payload,
                domainEvent.ReceivedOn),
            cancellationToken).ConfigureAwait(false);
    }

    ValueTask InsertStartAttemptAsync(IEvent domainEvent, CancellationToken cancellationToken)
        => domainEvent switch
        {
            StrategyWorkflowStartAcceptedEvent accepted => new ValueTask(
                _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
                    new IntrinsicTimeStrategyWorkflowStartAttemptReadModel(
                        accepted.EntityId.Format(),
                        accepted.StartedAtUtc,
                        accepted.WorkflowId,
                        StrategyWorkflowStartDecision.Accepted,
                        accepted.WorkflowId,
                        accepted.CommandId,
                        accepted.TriggerEventId,
                        accepted.Stage,
                        string.Empty,
                        accepted.EventId),
                    cancellationToken)),
            StrategyWorkflowStartRejectedEvent rejected => new ValueTask(
                _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
                    new IntrinsicTimeStrategyWorkflowStartAttemptReadModel(
                        rejected.EntityId.Format(),
                        rejected.RejectedAtUtc,
                        rejected.RequestedWorkflowId,
                        StrategyWorkflowStartDecision.Rejected,
                        rejected.ActiveWorkflowId,
                        rejected.CommandId,
                        rejected.TriggerEventId,
                        rejected.ActiveStage,
                        rejected.ReasonCode,
                        rejected.EventId),
                    cancellationToken)),
            _ => ValueTask.CompletedTask
        };

    async ValueTask PublishLifecycleAsync(IEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case IntrinsicTimeStrategyWorkflowStartedEvent started:
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowStartedEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    started with { Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowStartedEvent.Verb, started.EntityId) },
                    cancellationToken).ConfigureAwait(false);
                break;
            case IntrinsicTimeStrategyWorkflowContinuedEvent continued:
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowContinuedEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    continued with { Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowContinuedEvent.Verb, continued.EntityId) },
                    cancellationToken).ConfigureAwait(false);
                break;
            case IntrinsicTimeStrategyWorkflowCompletedEvent completed:
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowCompletedEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    completed with { Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowCompletedEvent.Verb, completed.EntityId) },
                    cancellationToken).ConfigureAwait(false);
                break;
            case IntrinsicTimeStrategyWorkflowStoppedEvent stopped:
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowStoppedEvent, IntrinsicTimeStrategyWorkflowEntityId>(
                    stopped with { Subject = RealtimeSubject(IntrinsicTimeStrategyWorkflowStoppedEvent.Verb, stopped.EntityId) },
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    static ActorSubject RealtimeSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Realtime, RealtimeActorName, verb, entityId.Format());

    static StrategyWorkflowStage PreviousStage(StrategyWorkflowStage stage) => stage switch
    {
        StrategyWorkflowStage.MarketCondition => StrategyWorkflowStage.RegimeDiscovery,
        StrategyWorkflowStage.TradeSelection => StrategyWorkflowStage.MarketCondition,
        StrategyWorkflowStage.OrderComposition => StrategyWorkflowStage.TradeSelection,
        StrategyWorkflowStage.RiskManagement => StrategyWorkflowStage.OrderComposition,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The first stage has no predecessor.")
    };

    static StrategyWorkflowId? GetProjectionWorkflowId(IEvent domainEvent)
        => domainEvent switch
        {
            StrategyWorkflowStartAcceptedEvent accepted => accepted.WorkflowId,
            StrategyWorkflowStartRejectedEvent rejected => rejected.ActiveWorkflowId,
            _ => domainEvent.GetType().GetProperty(nameof(IntrinsicTimeStrategyWorkflowState.WorkflowId))
                ?.GetValue(domainEvent) is StrategyWorkflowId workflowId
                    ? workflowId
                    : null
        };

    static long GetLongProperty(IEvent domainEvent, string propertyName)
        => domainEvent.GetType().GetProperty(propertyName)?.GetValue(domainEvent) is long value ? value : 0;

    static StrategyWorkflowStage GetStage(IEvent domainEvent)
    {
        foreach (var propertyName in new[] { "Stage", "NextPipelineStage", "CompletedPipelineStage", "ActiveStage" })
        {
            if (domainEvent.GetType().GetProperty(propertyName)?.GetValue(domainEvent) is StrategyWorkflowStage stage)
                return stage;
        }
        return StrategyWorkflowStage.None;
    }
}
