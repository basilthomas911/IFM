using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Events;
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
public sealed class ApplicationStateRepository(
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
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<ApplicationCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotAsync<ApplicationCommandState, ApplicationStartupEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Save application state changes.
    /// </summary>
    /// <param name="context">The command actor context.</param>
    /// <param name="state">The current actor command state.</param>
    /// <param name="command">The command that produced the state changes.</param>
    public async ValueTask SaveStateAsync(ICommandActorContext context, ApplicationCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, ApplicationCommandState state, ICommand command, CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the application query state
    /// asynchronously.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        // Preserve event ordering while avoiding an enumerator/interface dispatch in
        // this persistence hot path. Actor mailbox serialization already provides the
        // required per-stream concurrency boundary, so no additional lock is needed.
        for (var index = 0; index < domainEvents.Count; index++)
        {
            switch (domainEvents[index])
            {
                case ApplicationStartupEvent startup:
                    _ = await PostEventAsync<ApplicationStartupEvent, ApplicationEntityId>(context, startup).ConfigureAwait(false);
                    break;
                case ApplicationShutdownEvent shutdown:
                    _ = await PostEventAsync<ApplicationShutdownEvent, ApplicationEntityId>(context, shutdown).ConfigureAwait(false);
                    break;
            }
        }
    }
}

