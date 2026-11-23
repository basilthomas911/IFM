using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
/// <summary>
/// Loads and saves the authoritative Intrinsic Time Strategy Workflow Command state through PostgreSQL event sourcing.
/// </summary>
/// <remarks>
/// Runtime recovery applies the newest complete checkpoint and its authoritative tail, or replays the original source facts.
/// Non-empty legacy streams without a verified checkpoint and unsupported tail facts fail closed. An expected-version ACID batch
/// commits source and required PostgreSQL read rows together, then queues committed notifications and actor handoffs.
/// </remarks>
/// <param name = "stateFactory">Factory that creates an empty workflow command-state shell before snapshot load.</param>
/// <param name = "eventSource">PostgreSQL event-source database context.</param>
/// <param name = "actorService">Actor infrastructure used by the base event-source repository.</param>
/// <param name = "eventProjector">Post-commit notification and actor-handoff projector.</param>
/// <param name = "logger">Repository logger.</param>
public sealed class IntrinsicTimeStrategyWorkflowStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> eventProjector, ILogger<IntrinsicTimeStrategyWorkflowStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null,
    Runtime.WorkflowResidentStateRegistry? resident = null, Runtime.WorkflowLocalDispatcher? localDispatcher = null,
    Runtime.WorkflowDecisionBatchRunner? decisions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
{
    readonly IEventSourceActorStateFactory _stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
    readonly IEventSourceActorDbContext _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
    readonly IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> _eventProjector = eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));
    readonly ILogger<IntrinsicTimeStrategyWorkflowStateRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    /// <summary>Loads the latest authoritative workflow snapshot.</summary>
    /// <param name = "command">Command whose stream identity selects the workflow entity stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    /// <summary>Loads the latest authoritative workflow snapshot.</summary>
    /// <param name = "command">Command whose stream identity selects the workflow entity stream.</param>
    /// <param name = "cancellationToken">Cancellation token honored while reading the event stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var trace = WorkflowTrace.Start("workflow.state.load", null);
        trace?.SetTag("ifm.workflow.entity", command.Subject.EntityId);
        trace?.SetTag("ifm.command.name", command.CommandName);
        cancellationToken.ThrowIfCancellationRequested();
        if (resident is not null && string.Equals(dbFactory?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
            && resident.TryFork(command.Subject.EntityId, out var owned))
        {
            await RememberPersistedTriggerAsync(owned!, command, cancellationToken).ConfigureAwait(false);
            return owned!;
        }
        var state = (IntrinsicTimeStrategyWorkflowCommandState)_stateFactory.CreateState<IntrinsicTimeStrategyWorkflowCommandState>();
        state.Id = command.Subject.ThreadId;
        using var streamRead = WorkflowTrace.Start("workflow.state.stream_lookup", null);
        var stream = await _eventSource.GetEventStreamIdFromDbAsync(command.StreamId).ConfigureAwait(false);
        streamRead?.Stop();
        if (stream is null)
            return state;
        using var snapshotRead = WorkflowTrace.Start("workflow.state.snapshot_read", null);
        var events = new List<global::TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel>();
        await _eventSource.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<IntrinsicTimeStrategyWorkflowCommandState>(
            stream.EventStreamId, CheckpointTypes, rows => events.AddRange(rows), cancellationToken).ConfigureAwait(false);
        snapshotRead?.SetTag("ifm.workflow.snapshot_count", events.Count);
        snapshotRead?.Stop();
        if (events.Count == 0)
            return state;
        trace?.SetTag("ifm.workflow.snapshot_count", events.Count);
        using var decode = WorkflowTrace.Start("workflow.state.deserialize", null);
        decode?.SetTag("ifm.workflow.entity", command.Subject.EntityId);
        // The storage query retains row one as a stream boundary. A verified checkpoint carries
        // both the complete workflow view and the private metadata needed to supersede that prefix.
        var checkpointIndex = events.FindLastIndex(row => EventTypeRegistry.Default.Resolve(row.EventTypeName) ==
            typeof(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent));
        var converted = events.Skip(Math.Max(checkpointIndex, 0))
            .Select(value => (Stream: value, Event: value.ToDomainEvent())).ToArray();
        decode?.Stop();
        using var apply = WorkflowTrace.Start("workflow.state.apply", null);
        apply?.SetTag("ifm.workflow.entity", command.Subject.EntityId);
        if (checkpointIndex < 0 && !converted.Any(value => value.Event is WorkflowStrategyStateUpdatedEvent))
        {
            if (_logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write29(_logger, nameof(IntrinsicTimeStrategyWorkflowStateRepository), nameof(LoadStateAsync), command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count);
        }

        if (converted.Skip(checkpointIndex >= 0 ? 1 : 0).Any(value =>
            value.Event is not (WorkflowStrategyStateUpdatedEvent or IntrinsicTimeStrategySignalQueuedEvent)))
        {
            if (_logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write30(_logger, nameof(IntrinsicTimeStrategyWorkflowStateRepository), nameof(LoadStateAsync), command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count, "The stream contains events after its latest authoritative snapshot.");
        }

        foreach (var entry in converted)
        {
            var restored = entry.Event switch
            {
                WorkflowStrategyStateUpdatedEvent source => IsOwned(source) && state.Apply(source, addEvent: false),
                IntrinsicTimeStrategySignalQueuedEvent queued =>
                    string.Equals(queued.EntityId.Format(), command.Subject.EntityId, StringComparison.Ordinal) &&
                    state.Apply(queued, addEvent: false),
                IntrinsicTimeStrategyWorkflowStateSnapshottedEvent snapshot =>
                    snapshot.Latest is { } latest && IsOwned(latest) && latest.EventId < entry.Stream.EventVersion &&
                    snapshot.AppliedEventCount < entry.Stream.StreamVersion && Enum.IsDefined(snapshot.LastStartDecision) &&
                    state.RestoreSnapshot(snapshot),
                _ => false
            };
            if (!restored)
                throw new InvalidOperationException($"The workflow creation snapshot or tail in {command.StreamId} is invalid or cannot be applied.");
        }
        var head = events[^1].StreamVersion;
        state.AcceptChanges();
        state.RestoreCommittedStreamVersion(head);
        state.SetPersistedStreamVersion(head);
        state.ResetQueueChangeTracking();
        await RememberPersistedTriggerAsync(state, command, cancellationToken).ConfigureAwait(false);
        if (string.Equals(dbFactory?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            resident?.PublishCommitted(command.Subject.EntityId, state);
        return state;

        bool IsOwned(WorkflowStrategyStateUpdatedEvent source) => source.WorkflowId.Value != Guid.Empty && source.WorkflowRevision > 0 &&
            string.Equals(source.EntityId.Format(), command.Subject.EntityId, StringComparison.Ordinal);
    }

    private static readonly Type[] CheckpointTypes = [typeof(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent)];

    async Task RememberPersistedTriggerAsync(IntrinsicTimeStrategyWorkflowCommandState state, ICommand command, CancellationToken token)
    {
        // This is duplicate checking at durable signal admission, never a decision input read.
        if (command is not ExecuteIntrinsicTimeStrategyWorkflowCommand execute || execute.TriggerEventId == Guid.Empty
            || state.IsDuplicateTrigger(execute.TriggerEventId) || transactions is null
            || !string.Equals(dbFactory?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)) return;
        var started = await transactions.ExecuteOnceAsync((db, ct) => db.ScalarAsync(
            TradeDbSql.FindStartedIntrinsicTimeTrigger, [command.Subject.EntityId, execute.TriggerEventId], ct), token).ConfigureAwait(false);
        if (started is true) state.RememberStartedTrigger(execute.TriggerEventId);
    }

    /// <summary>Persists pending workflow events as one ACID event batch.</summary>
    /// <param name = "context">Command actor context associated with the save operation.</param>
    /// <param name = "state">Workflow command state containing pending events.</param>
    /// <param name = "command">Command that produced the pending events.</param>
    /// <returns>A task representing the save operation.</returns>
    public ValueTask SaveStateAsync(ICommandActorContext context, IntrinsicTimeStrategyWorkflowCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    /// <summary>Persists pending workflow events as one ACID event batch.</summary>
    /// <param name = "context">Command actor context associated with the save operation.</param>
    /// <param name = "state">Workflow command state containing pending events.</param>
    /// <param name = "command">Command that produced the pending events.</param>
    /// <param name = "cancellationToken">Cancellation token honored until event persistence commits.</param>
    /// <returns>A task representing the save operation.</returns>
    public ValueTask SaveStateAsync(ICommandActorContext context, IntrinsicTimeStrategyWorkflowCommandState state, ICommand command, CancellationToken cancellationToken)
        => SaveCoreAsync(context, state, command, cancellationToken);

    async ValueTask SaveCoreAsync(ICommandActorContext context, IntrinsicTimeStrategyWorkflowCommandState state, ICommand command,
        CancellationToken cancellationToken, IReadOnlyList<ICommand>? decisionCommands = null,
        DomainEventCollection? precedingProjectionEvents = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Events.Count == 0)
            return;
        using var trace = WorkflowTrace.Start("workflow.state.save", state.Events.OfType<WorkflowStrategyStateUpdatedEvent>().LastOrDefault()?.State);
        if (string.Equals(dbFactory?.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            if (!string.Equals(_eventSource.ProviderName, "System.Data.Postgres", StringComparison.Ordinal)
                || PostgresDatabaseIdentity.Parse(_eventSource.ConnectionString) != PostgresDatabaseIdentity.Parse(dbFactory.TradeDb.ConnectionString))
                throw new InvalidOperationException("Workflow source and read rows must share one PostgreSQL database/security identity.");
            var owner = transactions ?? throw new InvalidOperationException("Atomic workflow persistence requires the PostgreSQL transaction service.");
            var writer = dbFactory.TradeDb as IEnlistedTradeDbWriter ?? throw new InvalidOperationException("TradeDb requires an enlisted workflow writer.");
            var pending = new DomainEventCollection(state.Events);
            if (decisionCommands is not null)
            {
                var commands = decisionCommands.Select(value => value.CommandId).Append(command.CommandId).ToHashSet();
                if (pending.Any(value => !commands.Contains(value.CommandId))) throw new InvalidOperationException("WF.EVIDENCE.COMMAND_NOT_OWNED");
            }
            var capture = ShouldCapturePeriodicSnapshot(state.PersistedStreamVersion, pending.Count);
            try
            {
            await owner.ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ExecuteAsync(TradeDbWorkflowRuntimeSql.LockDecisionStream, [command.StreamId], token).ConfigureAwait(false);
                await transaction.ReserveCommandAsync(command, token).ConfigureAwait(false);
                if (decisionCommands is not null)
                    foreach (var decision in decisionCommands.DistinctBy(value => value.CommandId))
                        await transaction.ReserveCommandAsync(decision, token).ConfigureAwait(false);
                var version = state.PersistedStreamVersion;
                foreach (var domainEvent in pending)
                {
                    if (domainEvent is not (WorkflowStrategyStateUpdatedEvent or IntrinsicTimeStrategySignalQueuedEvent))
                        throw new InvalidOperationException("A strategy workflow transaction requires a snapshot or a queued ITI signal.");
                    await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, domainEvent.CommandId, domainEvent, version++), token).ConfigureAwait(false);
                    if (domainEvent is WorkflowStrategyStateUpdatedEvent snapshot)
                        await writer.ProjectWorkflowAsync(transaction, snapshot, token).ConfigureAwait(false);
                }
                if (state.QueueChanged)
                    await transaction.ExecuteAsync(state.NextSignal is null ? TradeDbSql.ClearIntrinsicTimeSignalQueuePending :
                        TradeDbSql.MarkIntrinsicTimeSignalQueuePending, [command.Subject.EntityId], token).ConfigureAwait(false);
                if (capture)
                {
                    var latest = state.LatestSource!;
                    await transaction.AppendAsync(new EnlistedEventAppend(command.StreamId, command.CommandId, state.CaptureSnapshot(command, latest),
                        version), token).ConfigureAwait(false);
                }
                return true;
            }, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                resident?.Invalidate(command.Subject.EntityId);
                foreach (var unconfirmed in pending.OfType<WorkflowStrategyStateUpdatedEvent>()
                    .Where(value => value.State.DecisionRuntimeVersion > 0).DistinctBy(value => value.WorkflowId))
                    resident?.MarkUncertain(unconfirmed.State, command.StreamId);
                throw;
            }
            state.SetPersistedStreamVersion(checked(state.PersistedStreamVersion + pending.Count + (capture ? 1 : 0)));
            state.AcceptChanges();
            state.ResetQueueChangeTracking();
            if (capture)
                state.RestoreCommittedStreamVersion(state.PersistedStreamVersion);
            resident?.PublishCommitted(command.Subject.EntityId, state);
            trace?.Stop(); // Source persistence ends here; subsequent decisions have their own timing boundary.
            var latest = pending.OfType<WorkflowStrategyStateUpdatedEvent>().LastOrDefault();
            if (latest is not null) resident?.ConfirmOutcome(latest.WorkflowId.Value);
            if (decisions is not null && latest is { DispatchRegimeDiscovery: true, SkipPipelineDispatch: false }
                && latest.State is { DecisionRuntimeVersion: > 0, Status: Shared.Strategy.Workflow.IntrinsicTime.Model.WorkflowStrategyMachineStatus.Started })
            {
                // Admission is durable before any calculation. The same assignment owner retains its state while
                // all stages run locally, so another command cannot interleave with private decision evidence.
                // Publish admission projection work after calculation, so notification backpressure cannot
                // consume an operator deadline. Its earlier stream version must not be stranded behind
                // the final evidence: fenced projectors correctly refuse to skip that admission.
                Runtime.WorkflowDecisionBatchRunner.Evidence evidence;
                try
                {
                    evidence = await decisions.RunAsync((ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>)context, state).ConfigureAwait(false);
                    await SaveCoreAsync(context, state, evidence.Command, cancellationToken, evidence.Commands, pending).ConfigureAwait(false);
                }
                finally
                {
                    // Admission is already committed even when the final evidence reply is lost.
                    // Startup recovery also retains the original source identity if publication fails.
                    await ProjectWorkflowEventsAsync(pending).ConfigureAwait(false);
                }
                // A following signal is a separate admission, never part of an uncertain decision-result write.
                if (state.NextSignal is not null && !state.HasActiveWorkflow)
                {
                    var promotion = evidence.Command with { CommandId = Guid.NewGuid(), RequestedBy = Realtime.IntrinsicTimeSignalQueueRecoveryService.RequestSource };
                    ExecuteIntrinsicTimeStrategyWorkflow.PromoteQueuedSignals(
                        (ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>)context, state, promotion, expireActive: false);
                    await SaveStateAsync(context, state, promotion, cancellationToken).ConfigureAwait(false);
                }
                return;
            }
            // Only the acknowledged source transaction can publish a decision continuation.
            // Projector reads, notifications and financial handoffs cannot delay its scheduling.
            foreach (var snapshot in pending.OfType<WorkflowStrategyStateUpdatedEvent>().Where(value => value.State.DecisionRuntimeVersion > 0 && !value.SkipPipelineDispatch
                && (decisions is null || ReferenceEquals(value, latest))))
                (localDispatcher ?? throw new InvalidOperationException("WF.RUNTIME.DISPATCHER_NOT_READY")).Dispatch(snapshot);
            await ProjectWorkflowEventsAsync(precedingProjectionEvents is null ? pending :
                new DomainEventCollection(precedingProjectionEvents.Concat(pending))).ConfigureAwait(false);
            return;
        }
        var committed = await _eventSource.SaveEventsAsync(command.StreamId, command.CommandId, state.Events, state.PersistedStreamVersion, cancellationToken).ConfigureAwait(false);
        await ProjectWorkflowEventsAsync(committed).ConfigureAwait(false);
    }

    ValueTask ProjectWorkflowEventsAsync(DomainEventCollection events)
    {
        var snapshots = new DomainEventCollection(events.OfType<WorkflowStrategyStateUpdatedEvent>());
        return snapshots.Count == 0 ? ValueTask.CompletedTask : _eventProjector.DomainEventsProjectionAsync(snapshots);
    }

    /// <summary>
    /// Queues committed events for conventional projection after PostgreSQL has completed the ACID transaction.
    /// </summary>
    /// <param name = "context">Command actor context associated with the committed batch.</param>
    /// <param name = "domainEvents">Committed workflow events awaiting future projection.</param>
    /// <returns>A task representing non-durable projector queueing.</returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
