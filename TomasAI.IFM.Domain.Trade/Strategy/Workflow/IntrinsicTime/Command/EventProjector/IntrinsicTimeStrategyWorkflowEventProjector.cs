using System.Collections.Concurrent;
using System.Collections.Immutable;
using MessagePack;
using Microsoft.Extensions.Logging;
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
        using var trace = WorkflowTrace.Start("workflow.project", snapshot.State);
        var entityKey = snapshot.EntityId.Format();
        var entityLock = _entityLocks.GetOrAdd(entityKey, static _ => new SemaphoreSlim(1, 1));
        using var timing_workflow_project_lock_wait = WorkflowTrace.Start("workflow.project.lock_wait", snapshot.State);
        await entityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        timing_workflow_project_lock_wait?.Stop();
        try
        {
            ValidateSnapshot(snapshot);
            var timelineWrite = InsertTimelineAsync(snapshot, cancellationToken).AsTask();
            var startAttemptWrite = InsertStartAttemptAsync(snapshot, cancellationToken).AsTask();
            var workflowWrite = UpsertWorkflowAsync(snapshot.State, snapshot.EventId, cancellationToken);
            // Independent tables may write concurrently, but all siblings must finish before
            // publication or lock release, including when any one write fails.
            await Task.WhenAll(timelineWrite, startAttemptWrite, workflowWrite).ConfigureAwait(false);
            var active = await workflowWrite.ConfigureAwait(false);
            // A Query cache miss can immediately read and cache the active table. Keep this
            // externally visible publication after every detail/history/index write succeeds.
            var tradeDb = _actorContext.DbFactory.TradeDb;
            if (active is not null)
            {
                await WriteProjectionAsync("workflow.project.active_write", snapshot.State,
                    () => tradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active, cancellationToken))
                    .ConfigureAwait(false);
                _cache.Set(active);
            }
            else
            {
                await WriteProjectionAsync("workflow.project.active_delete", snapshot.State,
                    () => tradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(entityKey, cancellationToken))
                    .ConfigureAwait(false);
                _cache.Remove(entityKey);
            }

            using var timing_workflow_project_notify = WorkflowTrace.Start("workflow.project.notify", snapshot.State);
            await _actorContext.SendAsync<WorkflowStrategyStateUpdatedEvent,
                IntrinsicTimeStrategyWorkflowEntityId>(snapshot with
                {
                    Subject = new ActorSubject(
                        ActorType.Realtime,
                        RealtimeActorName,
                        WorkflowStrategyStateUpdatedEvent.Verb,
                        entityKey)
                }, cancellationToken).ConfigureAwait(false);
            timing_workflow_project_notify?.Stop();

            // UI observation is deliberately isolated from workflow dispatch. A desktop listener outage must never
            // prevent an already committed workflow from continuing or reaching its terminal state.
            try
            {
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent,
                    IntrinsicTimeStrategyWorkflowEntityId>(new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent
                    {
                        Subject = new ActorSubject(
                            ActorType.Notify,
                            IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor,
                            IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb,
                            entityKey),
                        Id = snapshot.Id,
                        EntityId = snapshot.EntityId,
                        EventId = snapshot.EventId,
                        CommandId = snapshot.CommandId,
                        AggregateId = snapshot.AggregateId,
                        EventSource = snapshot.EventSource,
                        ReceivedOn = snapshot.ReceivedOn,
                        WorkflowId = snapshot.WorkflowId,
                        WorkflowRevision = snapshot.WorkflowRevision,
                        SourceEventId = snapshot.Id,
                        State = snapshot.State,
                        UpdatedAtUtc = snapshot.UpdatedAtUtc
                    }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _actorContext.Logger.LogError(
                    exception,
                    "Strategy Workflow UI notification failed for {WorkflowId} revision {WorkflowRevision}",
                    snapshot.WorkflowId,
                    snapshot.WorkflowRevision);
            }
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

    async Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> UpsertWorkflowAsync(
        IntrinsicTimeStrategyWorkflowView workflow,
        long eventId,
        CancellationToken cancellationToken)
    {
        using var timing_workflow_project_state_serialize = WorkflowTrace.Start("workflow.project.state_serialize", workflow);
        var payload = MessagePackSerializer.Serialize(workflow);
        timing_workflow_project_state_serialize?.SetTag("ifm.payload.bytes", payload.Length);
        timing_workflow_project_state_serialize?.Stop();
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

        var active = workflow.Status == WorkflowStrategyMachineStatus.Started
            ? new ActiveIntrinsicTimeStrategyWorkflowReadModel(
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
                workflow.UpdatedAtUtc)
            : null;
        var tradeDb = _actorContext.DbFactory.TradeDb;
        await Task.WhenAll(
            WriteProjectionAsync("workflow.project.detail_write", workflow,
                () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowAsync(detail, cancellationToken)),
            WriteProjectionAsync("workflow.project.entity_write", workflow,
                () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(history, cancellationToken)),
            WriteProjectionAsync("workflow.project.status_write", workflow,
                () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(history, cancellationToken)))
            .ConfigureAwait(false);
        // The caller writes the active table and cache after timeline/start-attempt completion.
        return active;
    }

    static async Task WriteProjectionAsync(string operation, IntrinsicTimeStrategyWorkflowView workflow,
        Func<Task> write)
    {
        using var trace = WorkflowTrace.Start(operation, workflow);
        await write().ConfigureAwait(false);
    }

    async ValueTask InsertTimelineAsync(
        WorkflowStrategyStateUpdatedEvent snapshot,
        CancellationToken cancellationToken)
    {
        using var serialization = WorkflowTrace.Start("workflow.project.timeline_serialize", snapshot.State);
        var payload = MessagePackSerializer.Serialize(snapshot);
        serialization?.SetTag("ifm.payload.bytes", payload.Length);
        serialization?.Stop();
        using var write = WorkflowTrace.Start("workflow.project.timeline_write", snapshot.State);
        await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(
            new IntrinsicTimeStrategyWorkflowTimelineReadModel(
                snapshot.WorkflowId, snapshot.EventId, snapshot.EntityId.Format(), snapshot.WorkflowRevision,
                snapshot.State.CurrentStage, snapshot.EventName, EventSchemaVersion, payload, snapshot.UpdatedAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    async ValueTask InsertStartAttemptAsync(
        WorkflowStrategyStateUpdatedEvent snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.State.Status != WorkflowStrategyMachineStatus.Started ||
            snapshot.PreviousStatus == WorkflowStrategyMachineStatus.Started)
            return;

        using var timing_workflow_project_start_attempt_write = WorkflowTrace.Start("workflow.project.start_attempt_write", snapshot.State);
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
        timing_workflow_project_start_attempt_write?.Stop();
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
