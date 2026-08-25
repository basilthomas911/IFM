using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

/// <summary>
/// Loads and saves the authoritative Intrinsic Time Strategy Workflow Command state through PostgreSQL event sourcing.
/// </summary>
/// <remarks>
/// ITSW-5 deliberately replays the complete entity stream because the workflow has no snapshot contract. ScyllaDB
/// projection is introduced by ITSW-7; until then the denormalization hook performs no external work after the ACID
/// event batch commits.
/// </remarks>
/// <param name="stateFactory">Factory that creates an empty workflow command-state shell for replay.</param>
/// <param name="eventSource">PostgreSQL event-source database context.</param>
/// <param name="actorService">Actor infrastructure used by the base event-source repository.</param>
/// <param name="logger">Repository logger.</param>
public sealed class IntrinsicTimeStrategyWorkflowStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    ILogger<IntrinsicTimeStrategyWorkflowStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>
{
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
    /// Completes the ITSW-5 post-commit hook without projection; ITSW-7 replaces this with the conventional projector.
    /// </summary>
    /// <param name="context">Command actor context associated with the committed batch.</param>
    /// <param name="domainEvents">Committed workflow events awaiting future projection.</param>
    /// <returns>A completed task.</returns>
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => ValueTask.CompletedTask;
}
