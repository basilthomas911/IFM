using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;

internal class YieldCurveRateStateRepository(
    IDbContextFactory dbFactory,
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    ILogger<BaseEventSourceRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<YieldCurveRateCommandState>
{
    /// <summary>
    /// Loads the yield curve rate state from a snapshot event.
    /// </summary>
    /// <param name="command">The command used to identify and load the state. Must not be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="YieldCurveRateCommandState"/>.</returns>
    public async ValueTask<YieldCurveRateCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<YieldCurveRateCommandState, YieldCurveRatesImportedEvent>(command);

    /// <summary>
    /// Saves the yield curve rate state changes.
    /// </summary>
    /// <param name="context">The command actor context providing contextual information for the save operation. Cannot be <see langword="null"/>.</param>
    /// <param name="state">The state to be saved. Cannot be <see langword="null"/>.</param>
    /// <param name="command">The command associated with the state change. Cannot be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, YieldCurveRateCommandState state, ICommand command)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the yield curve rate query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the yield curve rate
    /// query state accordingly. It is typically called as part of the event sourcing workflow to keep the read
    /// model in sync with the latest events.</remarks>
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
                YieldCurveRateAddedEvent e => await UpdateReadModelAsync<YieldCurveRateAddedEvent, YieldCurveRateAddedCompleteEvent, YieldCurveRateAddedFailEvent, YieldCurveRateEntityId>(
                    context, e, () =>InsertYieldCurveRateAsync(db, e.YieldCurveRate)),
                YieldCurveRateChangedEvent e => await UpdateReadModelAsync<YieldCurveRateChangedEvent, YieldCurveRateChangedCompleteEvent, YieldCurveRateChangedFailEvent, YieldCurveRateEntityId>(
                    context, e, async () =>
                    {
                        await DeleteYieldCurveRateAsync(db, e.YieldCurveRate.ValueDate);
                        await InsertYieldCurveRateAsync(db, e.YieldCurveRate);
                    }),
                YieldCurveRateRemovedEvent e => await UpdateReadModelAsync<YieldCurveRateRemovedEvent, YieldCurveRateRemovedCompleteEvent, YieldCurveRateRemovedFailEvent, YieldCurveRateEntityId>(
                    context, e, () => DeleteYieldCurveRateAsync(db, e.ValueDate)),
                YieldCurveRatesImportedEvent e => await UpdateReadModelAsync<YieldCurveRatesImportedEvent, YieldCurveRatesImportedCompleteEvent, YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(
                    context, e, () => InsertYieldCurveRatesAsync(db, e.YieldCurveRates)),
                _ => false
            };
        }

        static async ValueTask InsertYieldCurveRateAsync(IMarketDataDbContext db, YieldCurveRateReadModel yieldCurveRate)
            => await db.InsertYieldCurveRateAsync(yieldCurveRate);

        static async ValueTask DeleteYieldCurveRateAsync(IMarketDataDbContext db, DateOnly valueDate)
            => await db.DeleteYieldCurveRateAsync(valueDate);

        static async ValueTask InsertYieldCurveRatesAsync(IMarketDataDbContext db, ICollection<YieldCurveRateReadModel> rates)
            => await db.InsertYieldCurveRatesAsync(rates);
    }
}
