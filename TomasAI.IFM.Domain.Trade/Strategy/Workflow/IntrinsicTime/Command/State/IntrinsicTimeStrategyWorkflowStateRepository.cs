using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Snapshots;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
/// <summary>
/// Loads and saves the authoritative Intrinsic Time Strategy Workflow Command state through PostgreSQL event sourcing.
/// </summary>
/// <remarks>
/// Runtime recovery applies only the latest <see cref = "WorkflowStrategyStateUpdatedEvent"/>. A non-empty stream
/// without a safe snapshot, or with an event after that snapshot, fails closed. After an expected-version ACID batch
/// commits source and required PostgreSQL read rows together, then queues committed notifications and actor handoffs.
/// </remarks>
/// <param name = "stateFactory">Factory that creates an empty workflow command-state shell before snapshot load.</param>
/// <param name = "eventSource">PostgreSQL event-source database context.</param>
/// <param name = "actorService">Actor infrastructure used by the base event-source repository.</param>
/// <param name = "eventProjector">Post-commit notification and actor-handoff projector.</param>
/// <param name = "logger">Repository logger.</param>
public sealed class IntrinsicTimeStrategyWorkflowStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> eventProjector, ILogger<IntrinsicTimeStrategyWorkflowStateRepository> logger, IDbContextFactory? dbFactory = null, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
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
        var converted = events.Select(value => (Stream: value, Event: value.ToDomainEvent())).ToArray();
        decode?.Stop();
        using var apply = WorkflowTrace.Start("workflow.state.apply", null);
        apply?.SetTag("ifm.workflow.entity", command.Subject.EntityId);
        var snapshots = converted.Where(value => value.Event is WorkflowStrategyStateUpdatedEvent).ToArray();
        if (snapshots.Length == 0)
        {
            if (_logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write29(_logger, nameof(IntrinsicTimeStrategyWorkflowStateRepository), nameof(LoadStateAsync), command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count);
        }

        if (converted.Any(value => value.Event is not (WorkflowStrategyStateUpdatedEvent or IntrinsicTimeStrategyWorkflowStateSnapshottedEvent)))
        {
            if (_logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write30(_logger, nameof(IntrinsicTimeStrategyWorkflowStateRepository), nameof(LoadStateAsync), command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count, "The stream contains events after its latest authoritative snapshot.");
        }

        foreach (var entry in converted)
        {
            var restored = entry.Event switch
            {
                WorkflowStrategyStateUpdatedEvent source => state.Apply(source, addEvent: false),
                IntrinsicTimeStrategyWorkflowStateSnapshottedEvent snapshot => state.RestoreSnapshot(snapshot),
                _ => false
            };
            if (!restored)
                throw new InvalidOperationException($"The workflow creation snapshot or tail in {command.StreamId} is invalid or cannot be applied.");
        }
        var head = events[^1].StreamVersion;
        state.RestoreCommittedStreamVersion(head);
        state.SetPersistedStreamVersion(head);
        return state;
    }

    private static readonly Type[] CheckpointTypes = [typeof(IntrinsicTimeStrategyWorkflowStateSnapshottedEvent)];

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
    public async ValueTask SaveStateAsync(ICommandActorContext context, IntrinsicTimeStrategyWorkflowCommandState state, ICommand command, CancellationToken cancellationToken)
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
            var capture = ShouldCapturePeriodicSnapshot(state.PersistedStreamVersion, pending.Count);
            await owner.ExecuteOnceAsync(async (transaction, token) =>
            {
                await transaction.ReserveCommandAsync(command, token).ConfigureAwait(false);
                var version = state.PersistedStreamVersion;
                foreach (var domainEvent in pending)
                {
                    if (domainEvent is not WorkflowStrategyStateUpdatedEvent snapshot)
                        throw new InvalidOperationException("A strategy workflow transaction requires complete state snapshots.");
                    await transaction.AppendAsync(command.StreamId, command.CommandId, snapshot, version++, token).ConfigureAwait(false);
                    await writer.ProjectWorkflowAsync(transaction, snapshot, token).ConfigureAwait(false);
                }
                if (capture)
                {
                    var latest = (WorkflowStrategyStateUpdatedEvent)pending[^1];
                    await transaction.AppendAsync(command.StreamId, command.CommandId, state.CaptureSnapshot(command, latest),
                        version, token).ConfigureAwait(false);
                }
                return true;
            }, cancellationToken).ConfigureAwait(false);
            state.SetPersistedStreamVersion(checked(state.PersistedStreamVersion + pending.Count + (capture ? 1 : 0)));
            state.AcceptChanges();
            if (capture)
                state.RestoreCommittedStreamVersion(state.PersistedStreamVersion);
            await _eventProjector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
            return;
        }
        var committed = await _eventSource.SaveEventsAsync(command.StreamId, command.CommandId, state.Events, state.PersistedStreamVersion, cancellationToken).ConfigureAwait(false);
        await _eventProjector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    /// <summary>
    /// Queues committed events for conventional projection after PostgreSQL has completed the ACID transaction.
    /// </summary>
    /// <param name = "context">Command actor context associated with the committed batch.</param>
    /// <param name = "domainEvents">Committed workflow events awaiting future projection.</param>
    /// <returns>A task representing non-durable projector queueing.</returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
