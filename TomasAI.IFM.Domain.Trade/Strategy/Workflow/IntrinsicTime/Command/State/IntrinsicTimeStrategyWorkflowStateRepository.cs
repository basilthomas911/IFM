using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

/// <summary>
/// Loads and saves the authoritative Intrinsic Time Strategy Workflow Command state through PostgreSQL event sourcing.
/// </summary>
/// <remarks>
/// The repository replays the complete entity stream because the workflow has no authoritative snapshot contract.
/// After the ACID event batch commits, ITSW-7 queues the same committed events for conventional, non-durable
/// ScyllaDB projection.
/// </remarks>
/// <param name="stateFactory">Factory that creates an empty workflow command-state shell for replay.</param>
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
    readonly IEventProjector<IntrinsicTimeStrategyWorkflowCommandActor> _eventProjector
        = eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Loads the complete workflow entity stream and reconstructs its current command state.</summary>
    /// <param name="command">Command whose stream identity selects the workflow entity stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Loads the complete workflow entity stream and reconstructs its current command state.</summary>
    /// <param name="command">Command whose stream identity selects the workflow entity stream.</param>
    /// <param name="cancellationToken">Cancellation token honored while reading the event stream.</param>
    /// <returns>The reconstructed workflow command state.</returns>
    public async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateAsync<IntrinsicTimeStrategyWorkflowCommandState>(command, cancellationToken)
            .ConfigureAwait(false);

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
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);

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
