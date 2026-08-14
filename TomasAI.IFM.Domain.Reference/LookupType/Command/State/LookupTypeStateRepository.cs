using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.Reference.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.State;

public class LookupTypeStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    IActorService actorService,
    IEventProjector<LookupTypeCommandActor> eventProjector,
    ILogger<LookupTypeStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<LookupTypeCommandState>
{
    /// <summary>
    /// Asynchronously loads the current state of the lookup type actor associated with the specified command.
    /// </summary>
    /// <remarks>This method reconstructs the actor's state by replaying all domain events associated with the
    /// command's entity ID from the event source database. The events are applied in sequence to build the current
    /// state representation.</remarks>
    /// <param name="command">The command for which the state is being loaded. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded lookup type command state.</returns>
    public async ValueTask<LookupTypeCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<LookupTypeCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<LookupTypeCommandState, LookupTypeAddedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously saves the current state of the lookup type actor by persisting the pending events from the state
    /// to the event source database.
    /// </summary>
    /// <remarks>This method extracts all uncommitted events from the provided state and persists them to the event
    /// source database. Once saved, the events are cleared from the state's pending events collection. The stream ID
    /// for the events is derived from the command's stream identifier.</remarks>
    /// <param name="state">The current state of the lookup type actor containing pending events to save. Cannot be null.</param>
    /// <param name="command">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, LookupTypeCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, LookupTypeCommandState state, ICommand command, CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously denormalizes the specified domain events into the read model database.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
