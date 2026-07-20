using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.State;

public class LookupTypeStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
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
        => await LoadStateFromSnapshotAsync<LookupTypeCommandState, LookupTypeAddedEvent>(command);

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
        => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Asynchronously denormalizes the specified domain events into the read model database.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.ReferenceDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                LookupTypeAddedEvent e => await UpdateReadModelAsync<LookupTypeAddedEvent, LookupTypeAddedCompleteEvent, LookupTypeAddedFailEvent, LookupTypeId>(
                    context, e, () => InsertLookupTypeAsync(db, e.LookupType)),
                LookupTypeChangedEvent e => await UpdateReadModelAsync<LookupTypeChangedEvent, LookupTypeChangedCompleteEvent, LookupTypeChangedFailEvent, LookupTypeId>(
                    context, e, () => UpdateLookupTypeAsync(db, e.EntityId, e.LookupType)),
                LookupTypeRemovedEvent e => await UpdateReadModelAsync<LookupTypeRemovedEvent, LookupTypeRemovedCompleteEvent, LookupTypeRemovedFailEvent, LookupTypeId>(
                    context, e, () => DeleteLookupTypeAsync(db, e.EntityId)),
                _ => false
            };
        }

        static async ValueTask InsertLookupTypeAsync( IReferenceDbContext db, LookupTypeReadModel e)
            => await db.InsertLookupTypeAsync(e);

        static async ValueTask UpdateLookupTypeAsync(IReferenceDbContext db, LookupTypeId id, LookupTypeReadModel e)
            => await db.UpdateLookupTypeAsync(id, e);

        static async ValueTask DeleteLookupTypeAsync(IReferenceDbContext db, LookupTypeId lookupTypeId)
            => await db.DeleteLookupTypeAsync(lookupTypeId);
    }
}