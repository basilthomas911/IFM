using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures ITI signals, including loading state and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesItiSignalCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesItiSignalCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory">The factory used to create instances of event source actor state.</param>
/// <param name="dbEventSource">The database context used to access event source actor data.</param>
/// <param name="actorService">The service responsible for managing actor instances.</param>
/// <param name="dbFactory">The database context factory used to create instances of the market data database context.</param>
/// <param name="logger">The logger used to record events and errors.</param>
public class FuturesItiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesItiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesItiSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <remarks>This method retrieves the state from a snapshot, which may involve I/O operations. Ensure
    /// that the command is valid and properly initialized before calling this method.</remarks>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesItiSignalCommandState.</returns>
    public async ValueTask<FuturesItiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesItiSignalCommandState, FuturesItiSignalGeneratedEvent>(command);

    /// <summary>
    /// Saves futures ITI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesItiSignalCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures ITI signal
    /// read model asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection. For
    /// <see cref="FuturesItiSignalGeneratedEvent"/> events, it skips trending mode signals, inserts the signal
    /// into the database, and checks whether the hold-trade state should be preserved on trend direction changes.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            var updated = domainEvent switch
            {
                FuturesItiSignalGeneratedEvent e => await UpdateReadModelAsync<FuturesItiSignalGeneratedEvent, FuturesItiSignalGeneratedCompleteEvent, FuturesItiSignalGeneratedFailEvent, FuturesItiSignalEntityId>(
                    context, e, async () => await UpdateFuturesItiSignalAsync(e)),
                _ => false
            };
            if (!updated)
                break;
        }
    }

    /// <summary>
    /// Updates the futures ITI signal read model by inserting the signal into the database and
    /// preserving the hold-trade state on trend direction changes.
    /// </summary>
    /// <param name="e">The generated event containing the futures ITI signal to persist.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    async ValueTask UpdateFuturesItiSignalAsync(FuturesItiSignalGeneratedEvent e)
    {
        var db = dbFactory.MarketDataDb;
        await db.InsertFuturesItiSignalAsync(e.FuturesItiSignal);
        if (e.FuturesItiSignal?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged)
        {
            var futuresItiSignal = await db.GetLastFuturesItiSignalAsync(e.FuturesItiSignal.ContractId, e.FuturesItiSignal.ValueDate);
            if (futuresItiSignal is not null && futuresItiSignal.TradeState == IntrinsicTimeTradeState.Hold)
                EventInitHelper.SetProperty(e, nameof(FuturesItiSignalGeneratedEvent.FuturesItiSignal), e.FuturesItiSignal with { TradeState = IntrinsicTimeTradeState.Hold });
        }
    }
}

