using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures TDI signals, including loading state and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <param name="aggregateFactory">The factory used to create instances of event source actor state.</param>
/// <param name="dbEventSource">The database context used to access event source actor data.</param>
/// <param name="actorService">The service responsible for managing actor instances.</param>
/// <param name="dbFactory">The database context factory used to read and write TDI signal data.</param>
/// <param name="logger">The logger used to record events and errors.</param>
public class FuturesTdiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesTdiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesTdiSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesTdiSignalCommandState.</returns>
    public async ValueTask<FuturesTdiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesTdiSignalCommandState, FuturesTdiSignalGeneratedEvent>(command);

    /// <summary>
    /// Saves futures TDI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesTdiSignalCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures TDI signal
    /// read model asynchronously.
    /// </summary>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesTdiSignalGeneratedEvent e => await UpdateReadModelAsync<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalGeneratedCompleteEvent, FuturesTdiSignalGeneratedFailEvent, FuturesTdiSignalEntityId>(
                    context, e, () => InsertFuturesTdiSignalAsync(db, e.FuturesTdiSignal)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesTdiSignalAsync(IMarketDataDbContext db, FuturesTdiSignalReadModel futuresTdiSignal)
            => await db.InsertFuturesTdiSignalAsync(futuresTdiSignal);
    }
}
