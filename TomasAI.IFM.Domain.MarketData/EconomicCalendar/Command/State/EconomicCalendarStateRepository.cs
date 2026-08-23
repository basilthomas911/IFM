using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Actor;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;

public class EconomicCalendarStateRepository(
    ICommandActorContext<EconomicCalendarCommandActor> actorContext)
    : BaseEventSourceActorRepository(actorContext.StateFactory, actorContext.DbEventSource,
        actorContext.ActorService, actorContext.Logger), IEventSourceActorStateRepository<EconomicCalendarCommandState>
{
    /// <summary>
    /// Asynchronously loads the current state of the economic calendar actor associated with the specified command.
    /// </summary>
    /// <remarks>This method reconstructs the actor's state by replaying all domain events associated with the
    /// command's entity ID from the event source database. The events are applied in sequence to build the current
    /// state representation.</remarks>
    /// <param name="command">The command for which the state is being loaded. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded economic calendar command state.</returns>
    public async ValueTask<EconomicCalendarCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<EconomicCalendarCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => command is ImportEconomicCalendarsCommand
            ? await LoadStateFromSnapshotAsync<EconomicCalendarCommandState, EconomicCalendarsImportedEvent>(command, cancellationToken).ConfigureAwait(false)
            : await LoadStateFromSnapshotAsync<EconomicCalendarCommandState, EconomicCalendarAddedEvent>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously saves the current state of the economic calendar actor by persisting the pending events from the state
    /// to the event source database.
    /// </summary>
    /// <remarks>This method extracts all uncommitted events from the provided state and persists them to the event
    /// source database. Once saved, the events are cleared from the state's pending events collection. The stream ID
    /// for the events is derived from the command's stream identifier.</remarks>
    /// <param name="context">The command actor context providing contextual information for the save operation. Cannot be null.</param>
    /// <param name="state">The current state of the economic calendar actor containing pending events to save. Cannot be null.</param>
    /// <param name="command">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, EconomicCalendarCommandState state, ICommand command)
        => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, EconomicCalendarCommandState state, ICommand command, CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the economic calendar query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the economic
    /// calendar query state accordingly. It is typically called as part of the event sourcing workflow to keep the read
    /// model in sync with the latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context,  DomainEventCollection domainEvents)
    {
        var db = actorContext.DbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                EconomicCalendarAddedEvent e => await UpdateReadModelAsync<EconomicCalendarAddedEvent, EconomicCalendarAddedCompleteEvent, EconomicCalendarAddedFailEvent, EconomicCalendarId>(
                    context, e, () =>InsertEconomicCalendarAsync(db, e.EconomicCalendar!)),
                EconomicCalendarsImportedEvent e => await PostEventAsync<EconomicCalendarsImportedEvent, EconomicCalendarId>(context, e),
                EconomicCalendarChangedEvent e => await UpdateReadModelAsync<EconomicCalendarChangedEvent, EconomicCalendarChangedCompleteEvent, EconomicCalendarChangedFailEvent, EconomicCalendarId>(
                    context, e, () =>UpdateEconomicCalendarAsync(db, e.EntityId!, e.EconomicCalendar!)),
                EconomicCalendarRemovedEvent e => await UpdateReadModelAsync<EconomicCalendarRemovedEvent, EconomicCalendarRemovedCompleteEvent, EconomicCalendarRemovedFailEvent, EconomicCalendarId>(
                    context, e, () => DeleteEconomicCalendarAsync(db, e.EntityId!)),
                _ => false
            };
        }

        static ValueTask InsertEconomicCalendarAsync(IMarketDataDbContext db, EconomicCalendarReadModel e)
            => new(db.InsertEconomicCalendarAsync(e));

        static ValueTask UpdateEconomicCalendarAsync(IMarketDataDbContext db, EconomicCalendarId id, EconomicCalendarReadModel e)
            => new(db.UpdateEconomicCalendarAsync(id, e));

        static ValueTask DeleteEconomicCalendarAsync(IMarketDataDbContext db, EconomicCalendarId id)
            => new(db.DeleteEconomicCalendarAsync(id));
    }
}
