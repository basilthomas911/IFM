using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.Command.State;

/// <summary>
/// Provides functionality to manage the state of the application, including loading state from snapshots and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements
/// <see cref="IEventSourceActorStateRepository{ApplicationCommandState}"/> to provide specialized behaviour for
/// managing <see cref="ApplicationCommandState"/> entities. It relies on an event-sourcing pattern to persist and
/// retrieve state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class ApplicationStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    ILogger<ApplicationStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<ApplicationCommandState>
{
    /// <summary>
    /// Load application state from snapshot event.
    /// </summary>
    /// <param name="command">The command for which state is required.</param>
    /// <returns>The reconstructed <see cref="ApplicationCommandState"/>.</returns>
    public async ValueTask<ApplicationCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<ApplicationCommandState, ApplicationStartupEvent>(command);

    /// <summary>
    /// Save application state changes.
    /// </summary>
    /// <param name="context">The command actor context.</param>
    /// <param name="state">The current actor command state.</param>
    /// <param name="command">The command that produced the state changes.</param>
    public async ValueTask SaveStateAsync(ICommandActorContext context, ApplicationCommandState state, ICommand command)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the application query state
    /// asynchronously.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                ApplicationStartupEvent e => await PostEventAsync<ApplicationStartupEvent, ApplicationEntityId>(context, e),
                ApplicationShutdownEvent e => await PostEventAsync<ApplicationShutdownEvent, ApplicationEntityId>(context, e),
                _ => false
            };
        }
    }
}

