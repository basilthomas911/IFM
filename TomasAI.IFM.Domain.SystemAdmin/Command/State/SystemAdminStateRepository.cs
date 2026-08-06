using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Events;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.Command.State;

/// <summary>
/// Provides fresh command state for stateless administrative operations and persists their emitted events.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{SystemAdminCommandState}"/> to provide specialized behavior for managing <see
/// cref="SystemAdminCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// event history. Backup commands do not depend on prior state, so loading avoids an unnecessary storage read while
/// saving continues to append the immutable event history.</remarks>
/// <param name="aggregateFactory">The factory used to create instances of event source actor state.</param>
/// <param name="dbEventSource">The database context used to access event source actor data.</param>
/// <param name="actorService">The service responsible for managing actor instances.</param>
/// <param name="logger">The logger used to record events and errors.</param>
public class SystemAdminStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    ILogger<SystemAdminStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<SystemAdminCommandState>
{
    readonly IEventSourceActorStateFactory _stateFactory = aggregateFactory;

    /// <summary>
    /// Creates fresh state because the backup decision does not depend on previously emitted events.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded.</param>
    /// <returns>A task that represents the asynchronous operation containing the loaded state.</returns>
    public ValueTask<SystemAdminCommandState> LoadStateAsync(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ValueTask.FromResult(
            (SystemAdminCommandState)_stateFactory.CreateState<SystemAdminCommandState>());
    }

    /// <summary>
    /// Saves system admin state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, SystemAdminCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the system admin state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and posts the corresponding
    /// events. It is typically called as part of the event sourcing workflow to keep the read model in sync with the
    /// latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is DatabaseBackupEvent e)
            {
                e.CheckForEmptyCommandId();
                await context.SendAsync<DatabaseBackupEvent, DatabaseBackupId>(e);
            }
        }
    }
}
