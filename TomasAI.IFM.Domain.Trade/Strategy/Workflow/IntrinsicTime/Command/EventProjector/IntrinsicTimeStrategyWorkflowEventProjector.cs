using System.Collections.Concurrent;
using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector;

/// <summary>Projects committed authoritative workflow snapshots to ScyllaDB and then publishes them.</summary>
/// <remarks>
/// Projection and notification are conventional post-commit work. A failure stops this notification chain and does
/// not schedule replay, rebuild, resume, or redispatch.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowEventProjector
    : ConventionalEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>
{
    const int StateSchemaVersion = 2;
    const int EventSchemaVersion = 2;
    const string RealtimeActorName = "IntrinsicTimeStrategyWorkflowRealtime";

    readonly ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> _actorContext;
    readonly IIntrinsicTimeStrategyWorkflowProjectionCache _cache;
    readonly ConcurrentDictionary<string, SemaphoreSlim> _entityLocks = new(StringComparer.Ordinal);
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    /// <summary>Initializes the conventional state-snapshot projector.</summary>
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
        _descriptors = [Describe()];
    }

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(WorkflowStrategyStateUpdatedEvent)];

    EventProjectionDescriptor Describe()
        => new(
            typeof(WorkflowStrategyStateUpdatedEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await ProjectAsync((WorkflowStrategyStateUpdatedEvent)domainEvent,
                    CancellationToken.None).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            _ => null,
            (_, _) => null,
            publishProcessingEvent: false,
            useDurableReplay: false,
            publishTerminalEvent: false);

    async ValueTask ProjectAsync(
        WorkflowStrategyStateUpdatedEvent snapshot,
        CancellationToken cancellationToken)
    {
        var entityKey = snapshot.EntityId.Format();
        var entityLock = _entityLocks.GetOrAdd(entityKey, static _ => new SemaphoreSlim(1, 1));
        await entityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidateSnapshot(snapshot);
            await InsertTimelineAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await InsertStartAttemptAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await UpsertWorkflowAsync(snapshot.State, snapshot.EventId, cancellationToken).ConfigureAwait(false);

            await _actorContext.SendAsync<WorkflowStrategyStateUpdatedEvent,
                IntrinsicTimeStrategyWorkflowEntityId>(snapshot with
                {
                    Subject = new ActorSubject(
                        ActorType.Realtime,
                        RealtimeActorName,
                        WorkflowStrategyStateUpdatedEvent.Verb,
                        entityKey)
                }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            entityLock.Release();
        }
    }

    static void ValidateSnapshot(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        if (snapshot.State.EntityId != snapshot.EntityId ||
            snapshot.State.WorkflowId != snapshot.WorkflowId ||
            snapshot.State.WorkflowRevision != snapshot.WorkflowRevision)
            throw new InvalidOperationException("Workflow state-update event metadata does not match its state view.");
    }

    async ValueTask UpsertWorkflowAsync(
        IntrinsicTimeStrategyWorkflowView workflow,
        long eventId,
        CancellationToken cancellationToken)
    {
        var payload = MessagePackSerializer.Serialize(workflow);
        var entity = workflow.EntityId;
        var iti = entity.ItiSignalEntityId;
        var status = ToLegacyStatus(workflow.Status);
        var outcome = workflow.Outcome != StrategyWorkflowOutcome.None
            ? workflow.Outcome : ToLegacyOutcome(workflow.Status);
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
            status,
            outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            eventId,
            StateSchemaVersion,
            payload,
            workflow.StopReasonCode,
            workflow.StartedAtUtc,
            workflow.TerminalAtUtc,
            workflow.UpdatedAtUtc);
        var history = new IntrinsicTimeStrategyWorkflowHistoryReadModel(
            entity.Format(),
            workflow.StartedAtUtc,
            workflow.WorkflowId,
            status,
            outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            workflow.TerminalAtUtc,
            workflow.StopReasonCode);

        var tradeDb = _actorContext.DbFactory.TradeDb;
        await tradeDb.UpsertIntrinsicTimeStrategyWorkflowAsync(detail, cancellationToken).ConfigureAwait(false);
        await tradeDb.UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(history, cancellationToken)
            .ConfigureAwait(false);
        await tradeDb.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(history, cancellationToken)
            .ConfigureAwait(false);

        if (workflow.Status == WorkflowStrategyMachineStatus.Started)
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
                workflow.UpdatedAtUtc);
            await tradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active, cancellationToken)
                .ConfigureAwait(false);
            _cache.Set(active);
        }
        else
        {
            await tradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(entity.Format(), cancellationToken)
                .ConfigureAwait(false);
            _cache.Remove(entity.Format());
        }
    }

    async ValueTask InsertTimelineAsync(
        WorkflowStrategyStateUpdatedEvent snapshot,
        CancellationToken cancellationToken)
        => await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
            new IntrinsicTimeStrategyWorkflowTimelineReadModel(
                snapshot.WorkflowId,
                snapshot.EventId,
                snapshot.EntityId.Format(),
                snapshot.WorkflowRevision,
                snapshot.State.CurrentStage,
                snapshot.EventName,
                EventSchemaVersion,
                MessagePackSerializer.Serialize(snapshot),
                snapshot.UpdatedAtUtc),
            cancellationToken).ConfigureAwait(false);

    async ValueTask InsertStartAttemptAsync(
        WorkflowStrategyStateUpdatedEvent snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.State.Status != WorkflowStrategyMachineStatus.Started ||
            snapshot.PreviousStatus == WorkflowStrategyMachineStatus.Started)
            return;

        await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(
            new IntrinsicTimeStrategyWorkflowStartAttemptReadModel(
                snapshot.EntityId.Format(),
                snapshot.State.StartedAtUtc,
                snapshot.WorkflowId,
                StrategyWorkflowStartDecision.Accepted,
                snapshot.WorkflowId,
                snapshot.CommandId,
                snapshot.State.TriggerEventId,
                snapshot.State.CurrentStage,
                string.Empty,
                snapshot.EventId),
            cancellationToken).ConfigureAwait(false);
    }

    static StrategyWorkflowStatus ToLegacyStatus(WorkflowStrategyMachineStatus status) => status switch
    {
        WorkflowStrategyMachineStatus.Empty => StrategyWorkflowStatus.None,
        WorkflowStrategyMachineStatus.Started => StrategyWorkflowStatus.Running,
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowStatus.Completed,
        _ => StrategyWorkflowStatus.Stopped
    };

    static StrategyWorkflowOutcome ToLegacyOutcome(WorkflowStrategyMachineStatus status) => status switch
    {
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowOutcome.Completed,
        WorkflowStrategyMachineStatus.Failed => StrategyWorkflowOutcome.PipelineFailed,
        WorkflowStrategyMachineStatus.TimedOut => StrategyWorkflowOutcome.TimedOut,
        WorkflowStrategyMachineStatus.Cancelled => StrategyWorkflowOutcome.Cancelled,
        _ => StrategyWorkflowOutcome.None
    };
}
