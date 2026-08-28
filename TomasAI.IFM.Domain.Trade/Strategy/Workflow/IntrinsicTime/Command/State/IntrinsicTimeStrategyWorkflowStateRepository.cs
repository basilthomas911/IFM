using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

/// <summary>
/// Loads and saves the authoritative Intrinsic Time Strategy Workflow Command state through PostgreSQL event sourcing.
/// </summary>
/// <remarks>
/// Runtime recovery applies only the latest <see cref="WorkflowStrategyStateUpdatedEvent"/>. A non-empty stream
/// without a safe snapshot, or with an event after that snapshot, fails closed. After an expected-version ACID batch
/// commits, the repository queues those committed snapshots for conventional, non-durable ScyllaDB projection.
/// </remarks>
/// <param name="stateFactory">Factory that creates an empty workflow command-state shell before snapshot load.</param>
/// <param name="eventSource">PostgreSQL event-source database context.</param>
/// <param name="actorService">Actor infrastructure used by the base event-source repository.</param>
/// <param name="eventProjector">Conventional non-durable ScyllaDB projector.</param>
/// <param name="logger">Repository logger.</param>
public sealed class IntrinsicTimeStrategyWorkflowStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> eventProjector,
    ILogger<IntrinsicTimeStrategyWorkflowStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
{
    readonly IEventSourceActorStateFactory _stateFactory
        = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
    readonly IEventSourceActorDbContext _eventSource
        = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
    readonly IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> _eventProjector
        = eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));
    readonly ILogger<IntrinsicTimeStrategyWorkflowStateRepository> _logger
        = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Loads the latest authoritative workflow snapshot.</summary>
    /// <param name="command">Command whose stream identity selects the workflow entity stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Loads the latest authoritative workflow snapshot.</summary>
    /// <param name="command">Command whose stream identity selects the workflow entity stream.</param>
    /// <param name="cancellationToken">Cancellation token honored while reading the event stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var state = (IntrinsicTimeStrategyWorkflowCommandState)_stateFactory
            .CreateState<IntrinsicTimeStrategyWorkflowCommandState>();
        state.Id = command.Subject.ThreadId;

        var stream = await _eventSource.GetEventStreamIdFromDbAsync(command.StreamId).ConfigureAwait(false);
        if (stream is null)
            return state;

        var events = await _eventSource
            .LoadActorEventStreamAsync<IntrinsicTimeStrategyWorkflowCommandState, WorkflowStrategyStateUpdatedEvent>(
                stream.EventStreamId)
            .ConfigureAwait(false);
        if (events.Count == 0)
            return state;

        var converted = events.Select(value => (Stream: value, Event: value.ToDomainEvent())).ToArray();
        var snapshots = converted
            .Where(value => value.Event is WorkflowStrategyStateUpdatedEvent)
            .ToArray();
        if (snapshots.Length == 0)
        {
            _logger.LogError(
                "Workflow stream is migration-blocked: {StreamId} contains {EventCount} legacy event(s)",
                command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count);
        }
        if (converted.Any(value => value.Event is not WorkflowStrategyStateUpdatedEvent))
        {
            _logger.LogError(
                "Workflow stream is migration-blocked: {StreamId} contains unsupported events among {EventCount} event(s)",
                command.StreamId, events.Count);
            throw new LegacyWorkflowStreamException(command.StreamId, events.Count,
                "The stream contains events after its latest authoritative snapshot.");
        }

        var latest = snapshots.MaxBy(static value => value.Stream.StreamVersion);
        var domainEvent = latest.Event;
        if (domainEvent is not WorkflowStrategyStateUpdatedEvent snapshot ||
            !state.Apply(snapshot, addEvent: false))
            throw new InvalidOperationException(
                $"The latest workflow snapshot in {command.StreamId} is invalid or cannot be applied.");
        state.SetPersistedStreamVersion(latest.Stream.StreamVersion);
        return state;
    }

    /// <summary>Persists pending workflow events as one ACID event batch.</summary>
    /// <param name="context">Command actor context associated with the save operation.</param>
    /// <param name="state">Workflow command state containing pending events.</param>
    /// <param name="command">Command that produced the pending events.</param>
    /// <returns>A task representing the save operation.</returns>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Persists pending workflow events as one ACID event batch.</summary>
    /// <param name="context">Command actor context associated with the save operation.</param>
    /// <param name="state">Workflow command state containing pending events.</param>
    /// <param name="command">Command that produced the pending events.</param>
    /// <param name="cancellationToken">Cancellation token honored until event persistence commits.</param>
    /// <returns>A task representing the save operation.</returns>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        IntrinsicTimeStrategyWorkflowCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Events.Count == 0)
            return;

        var committed = await _eventSource.SaveEventsAsync(
            command.StreamId,
            command.CommandId,
            state.Events,
            state.PersistedStreamVersion,
            cancellationToken).ConfigureAwait(false);
        await _eventProjector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    /// <summary>
    /// Queues committed events for conventional projection after PostgreSQL has completed the ACID transaction.
    /// </summary>
    /// <param name="context">Command actor context associated with the committed batch.</param>
    /// <param name="domainEvents">Committed workflow events awaiting future projection.</param>
    /// <returns>A task representing non-durable projector queueing.</returns>
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}

/// <summary>Blocks a non-empty workflow stream that has not been explicitly migrated to state snapshots.</summary>
public sealed class LegacyWorkflowStreamException : InvalidOperationException
{
    /// <summary>Creates a fail-closed legacy-stream error.</summary>
    public LegacyWorkflowStreamException(string streamId, int eventCount, string? detail = null)
        : base($"Workflow stream '{streamId}' contains {eventCount} event(s) but no safe current snapshot can be " +
               $"loaded. Explicit migration or archival is required. {detail}".Trim())
    {
        StreamId = streamId;
        EventCount = eventCount;
    }

    /// <summary>Gets the blocked stream identity.</summary>
    public string StreamId { get; }
    /// <summary>Gets the number of events observed by the snapshot load.</summary>
    public int EventCount { get; }
}
