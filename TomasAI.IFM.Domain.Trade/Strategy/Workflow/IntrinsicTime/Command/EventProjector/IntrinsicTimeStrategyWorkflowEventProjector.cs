using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector;
/// <summary>Publishes committed workflow snapshots and actor handoffs after the source transaction has written PostgreSQL read rows.</summary>
/// <remarks>
/// Projection and notification are conventional post-commit work. Durable execution recovers only incomplete
/// projector work and does not poll the event store during normal operation.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowEventProjector : ConventionalEventProjector<IntrinsicTimeStrategyWorkflowCommandActor>
{
    const int StateSchemaVersion = 2;
    const int EventSchemaVersion = 2;
    const string RealtimeActorName = "IntrinsicTimeStrategyWorkflowRealtime";
    readonly ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> _actorContext;
    readonly IIntrinsicTimeStrategyWorkflowProjectionCache _cache;
    readonly ConcurrentDictionary<string, SemaphoreSlim> _entityLocks = new(StringComparer.Ordinal);
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors;
    readonly IWorkflowRiskProjection _riskProjection;
    readonly ICommittedCompositionSubscriptionProjector _subscriptionProjection;
    readonly IPostgresEventTransaction? _transactions;
    /// <summary>Initializes the conventional state-snapshot projector.</summary>
    public IntrinsicTimeStrategyWorkflowEventProjector(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> actorContext, IWorkflowRiskProjection riskProjection, ICommittedCompositionSubscriptionProjector subscriptionProjection, EventProjectorReliabilityOptions? reliabilityOptions = null, IPostgresEventTransaction? transactions = null) : base((actorContext ?? throw new ArgumentNullException(nameof(actorContext))).DurableReplayQueue, actorContext.DbEventSource, actorContext.BlackboardService, actorContext.Logger, PostCommitReliability(reliabilityOptions))
    {
        _actorContext = actorContext;
        _transactions = transactions;
        _riskProjection = riskProjection;
        _subscriptionProjection = subscriptionProjection;
        _cache = IntrinsicTimeStrategyWorkflowProjectionCache.Shared;
        _descriptors = [Describe()];
    }

    static EventProjectorReliabilityOptions? PostCommitReliability(EventProjectorReliabilityOptions? options) => options is null ? null : options with
    {
        // This source-only projector publishes no completion messages. Its in-process queue is signaled by
        // committed events, while bounded startup recovery handles interrupted executions.
        TransactionalOutboxEnabled = false,
        BacklogMetricsPollingEnabled = false
    };
    /// <inheritdoc/>
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    /// <inheritdoc/>
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(WorkflowStrategyStateUpdatedEvent)];

    EventProjectionDescriptor Describe() => new EventProjectionDescriptor(typeof(WorkflowStrategyStateUpdatedEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation, async (domainEvent, context) =>
    {
        await ProjectAsync((WorkflowStrategyStateUpdatedEvent)domainEvent, context).ConfigureAwait(false);
        return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
    }, _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: true, publishTerminalEvent: false).WithReadModelRebuild((source, token) => RebuildReadModelAsync((WorkflowStrategyStateUpdatedEvent)source, token));
    /// <summary>Restores only the workflow detail, indexes, timeline and accepted start-attempt facts.
    /// No workflow dispatch, subscription, notification or runtime cache publication is performed.</summary>
    async Task RebuildReadModelAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token)
    {
        ValidateSnapshot(snapshot);
        if (string.Equals(_actorContext.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            var owner = _transactions ?? throw new InvalidOperationException("Workflow reconstruction requires the PostgreSQL transaction service.");
            await owner.ExecuteOnceAsync(async (transaction, cancellation) =>
            {
                await TradeDbContextExtensions.WorkflowProjectAsync(transaction, snapshot, cancellation).ConfigureAwait(false);
                return true;
            }, token).ConfigureAwait(false);
            return;
        }
        await InsertTimelineAsync(snapshot, token).ConfigureAwait(false);
        await InsertStartAttemptAsync(snapshot, token).ConfigureAwait(false);
        var active = await UpsertWorkflowAsync(snapshot.State, snapshot.EventId, token).ConfigureAwait(false);
        if (active is null)
            await _actorContext.DbFactory.TradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(snapshot.EntityId.Format(), token).ConfigureAwait(false);
        else
            await _actorContext.DbFactory.TradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active, token).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "UI notification is best effort after source and projection commit; log its failure and continue the committed business handoffs.")]
    async ValueTask ProjectAsync(WorkflowStrategyStateUpdatedEvent snapshot, ProjectionExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        if (_actorContext.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Trace))
        {
            var loggingArgument1 = snapshot.WorkflowId.ToString();
            var loggingArgument2 = snapshot.EntityId.Format();
            var loggingArgument3 = snapshot.State.CurrentStage.ToString();
            var loggingArgument4 = snapshot.State.Status.ToString();
            IntrinsicTimeStrategyWorkflowLogging.ProjectionStarted(_actorContext.Logger, snapshot.Id, loggingArgument1, loggingArgument2, snapshot.WorkflowRevision, loggingArgument3, loggingArgument4);
        }

        using var trace = WorkflowTrace.Start("workflow.project", snapshot.State);
        var entityKey = snapshot.EntityId.Format();
        var entityLock = _entityLocks.GetOrAdd(entityKey, static _ => new SemaphoreSlim(1, 1));
        using var timing_workflow_project_lock_wait = WorkflowTrace.Start("workflow.project.lock_wait", snapshot.State);
        await entityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        timing_workflow_project_lock_wait?.Stop();
        try
        {
            ValidateSnapshot(snapshot);
            var tradeDb = _actorContext.DbFactory.TradeDb;
            if (string.Equals(tradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                var active = TradeDbContextExtensions.WorkflowActive(snapshot);
                if (active is not null) _cache.Set(active);
                else _cache.Remove(entityKey);
            }
            else
            {
                await ProjectLegacyRowsAsync(snapshot, entityKey, cancellationToken).ConfigureAwait(false);

            }

            using var timing_workflow_project_notify = WorkflowTrace.Start("workflow.project.notify", snapshot.State);
            await _actorContext.SendAsync<WorkflowStrategyStateUpdatedEvent, IntrinsicTimeStrategyWorkflowEntityId>(snapshot with { Subject = new ActorSubject(ActorType.Realtime, RealtimeActorName, WorkflowStrategyStateUpdatedEvent.Verb, entityKey) }, cancellationToken).ConfigureAwait(false);
            timing_workflow_project_notify?.Stop();
            // UI observation is deliberately isolated from workflow dispatch. A desktop listener outage must never
            // prevent an already committed workflow from continuing or reaching its terminal state.
            try
            {
                await _actorContext.SendAsync<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent, IntrinsicTimeStrategyWorkflowEntityId>(new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent { Subject = new ActorSubject(ActorType.Notify, IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor, IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb, entityKey), Id = snapshot.Id, EntityId = snapshot.EntityId, EventId = snapshot.EventId, CommandId = snapshot.CommandId, AggregateId = snapshot.AggregateId, EventSource = snapshot.EventSource, ReceivedOn = snapshot.ReceivedOn, WorkflowId = snapshot.WorkflowId, WorkflowRevision = snapshot.WorkflowRevision, SourceEventId = snapshot.Id, State = snapshot.State, UpdatedAtUtc = snapshot.UpdatedAtUtc }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (_actorContext.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                    global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write22(_actorContext.Logger, exception, nameof(IntrinsicTimeStrategyWorkflowEventProjector), nameof(ProjectAsync), snapshot.WorkflowId, snapshot.WorkflowRevision);
            }

            // These handlers receive the already committed event on the projector worker. They do not scan or
            // deserialize event_log and therefore add no database polling to the workflow command path.
            await _riskProjection.ProjectCommittedAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await _subscriptionProjection.ProjectCommittedAsync(snapshot, context).ConfigureAwait(false);
            if (_actorContext.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
            {
                var loggingArgument1 = snapshot.WorkflowId.ToString();
                var loggingArgument2 = snapshot.EntityId.Format();
                var loggingArgument3 = snapshot.State.CurrentStage.ToString();
                var loggingArgument4 = snapshot.State.Status.ToString();
                var loggingArgument5 = snapshot.State.Outcome.ToString();
                IntrinsicTimeStrategyWorkflowLogging.ProjectionCompleted(_actorContext.Logger, snapshot.Id, loggingArgument1, loggingArgument2, snapshot.WorkflowRevision, loggingArgument3, loggingArgument4, loggingArgument5);
            }
        }
        finally
        {
            entityLock.Release();
        }
    }

    // Compatibility writers finish all required row writes before cache publication or external notifications.
    async Task ProjectLegacyRowsAsync(WorkflowStrategyStateUpdatedEvent snapshot, string entityKey, CancellationToken cancellationToken)
    {
        var tradeDb = _actorContext.DbFactory.TradeDb;
            var timelineWrite = InsertTimelineAsync(snapshot, cancellationToken).AsTask();
            var startAttemptWrite = InsertStartAttemptAsync(snapshot, cancellationToken).AsTask();
            var workflowWrite = UpsertWorkflowAsync(snapshot.State, snapshot.EventId, cancellationToken);
            // Independent tables may write concurrently, but all siblings must finish before
            // publication or lock release, including when any one write fails.
            await Task.WhenAll(timelineWrite, startAttemptWrite, workflowWrite).ConfigureAwait(false);
            var active = await workflowWrite.ConfigureAwait(false);
            // A Query cache miss can immediately read and cache the active table. Keep this
            // externally visible publication after every detail/history/index write succeeds.
            if (active is not null)
            {
                await WriteProjectionAsync("workflow.project.active_write", snapshot.State, () => tradeDb.UpsertActiveIntrinsicTimeStrategyWorkflowAsync(active, cancellationToken)).ConfigureAwait(false);
                _cache.Set(active);
            }
            else
            {
                await WriteProjectionAsync("workflow.project.active_delete", snapshot.State, () => tradeDb.DeleteActiveIntrinsicTimeStrategyWorkflowAsync(entityKey, cancellationToken)).ConfigureAwait(false);
                _cache.Remove(entityKey);
            }

    }

    static void ValidateSnapshot(WorkflowStrategyStateUpdatedEvent snapshot)
    {
        if (snapshot.State.EntityId != snapshot.EntityId || snapshot.State.WorkflowId != snapshot.WorkflowId || snapshot.State.WorkflowRevision != snapshot.WorkflowRevision)
            throw new InvalidOperationException("Workflow state-update event metadata does not match its state view.");
    }

    async Task<ActiveIntrinsicTimeStrategyWorkflowReadModel?> UpsertWorkflowAsync(IntrinsicTimeStrategyWorkflowView workflow, long eventId, CancellationToken cancellationToken)
    {
        using var timing_workflow_project_state_serialize = WorkflowTrace.Start("workflow.project.state_serialize", workflow);
        var payload = MessagePackSerializer.Serialize(workflow, cancellationToken: cancellationToken);
        timing_workflow_project_state_serialize?.SetTag("ifm.payload.bytes", payload.Length);
        timing_workflow_project_state_serialize?.Stop();
        var entity = workflow.EntityId;
        var iti = entity.ItiSignalEntityId;
        var status = ToLegacyStatus(workflow.Status);
        var outcome = workflow.Outcome != StrategyWorkflowOutcome.None ? workflow.Outcome : ToLegacyOutcome(workflow.Status);
        var detail = new IntrinsicTimeStrategyWorkflowReadModel(workflow.WorkflowId, entity.Format(), entity.WorkflowDefinitionId, workflow.WorkflowDefinitionVersion, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod, workflow.TriggerEventId, workflow.CorrelationId, status, outcome, workflow.CurrentStage, workflow.WorkflowRevision, eventId, StateSchemaVersion, payload, workflow.StopReasonCode, workflow.StartedAtUtc, workflow.TerminalAtUtc, workflow.UpdatedAtUtc);
        var history = new IntrinsicTimeStrategyWorkflowHistoryReadModel(entity.Format(), workflow.StartedAtUtc, workflow.WorkflowId, status, outcome, workflow.CurrentStage, workflow.WorkflowRevision, workflow.TerminalAtUtc, workflow.StopReasonCode);
        var active = workflow.Status == WorkflowStrategyMachineStatus.Started ? new ActiveIntrinsicTimeStrategyWorkflowReadModel(entity.Format(), workflow.WorkflowId, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod, workflow.CurrentStage, workflow.WorkflowRevision, eventId, StateSchemaVersion, payload, workflow.StartedAtUtc, workflow.UpdatedAtUtc) : null;
        var tradeDb = _actorContext.DbFactory.TradeDb;
        await Task.WhenAll(WriteProjectionAsync("workflow.project.detail_write", workflow, () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowAsync(detail, cancellationToken)), WriteProjectionAsync("workflow.project.entity_write", workflow, () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowByEntityAsync(history, cancellationToken)), WriteProjectionAsync("workflow.project.status_write", workflow, () => tradeDb.UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync(history, cancellationToken))).ConfigureAwait(false);
        // The caller writes the active table and cache after timeline/start-attempt completion.
        return active;
    }

    static async Task WriteProjectionAsync(string operation, IntrinsicTimeStrategyWorkflowView workflow, Func<Task> write)
    {
        using var trace = WorkflowTrace.Start(operation, workflow);
        await write().ConfigureAwait(false);
    }

    async ValueTask InsertTimelineAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken cancellationToken)
    {
        using var serialization = WorkflowTrace.Start("workflow.project.timeline_serialize", snapshot.State);
        var payload = MessagePackSerializer.Serialize(snapshot, cancellationToken: cancellationToken);
        serialization?.SetTag("ifm.payload.bytes", payload.Length);
        serialization?.Stop();
        using var write = WorkflowTrace.Start("workflow.project.timeline_write", snapshot.State);
        await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowTimelineAsync(new IntrinsicTimeStrategyWorkflowTimelineReadModel(snapshot.WorkflowId, snapshot.EventId, snapshot.EntityId.Format(), snapshot.WorkflowRevision, snapshot.State.CurrentStage, snapshot.EventName, EventSchemaVersion, payload, snapshot.UpdatedAtUtc), cancellationToken).ConfigureAwait(false);
    }

    async ValueTask InsertStartAttemptAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.State.Status != WorkflowStrategyMachineStatus.Started || snapshot.PreviousStatus == WorkflowStrategyMachineStatus.Started)
            return;
        using var timing_workflow_project_start_attempt_write = WorkflowTrace.Start("workflow.project.start_attempt_write", snapshot.State);
        await _actorContext.DbFactory.TradeDb.InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync(new IntrinsicTimeStrategyWorkflowStartAttemptReadModel(snapshot.EntityId.Format(), snapshot.State.StartedAtUtc, snapshot.WorkflowId, StrategyWorkflowStartDecision.Accepted, snapshot.WorkflowId, snapshot.CommandId, snapshot.State.TriggerEventId, snapshot.State.CurrentStage, string.Empty, snapshot.EventId), cancellationToken).ConfigureAwait(false);
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
