using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

/// <summary>
/// Provides functionality to manage the state of spread distributions, including loading state from snapshots and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{SpreadDistributionCommandState}"/> to provide specialized behavior for managing <see
/// cref="SpreadDistributionCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="dbFactory"></param>
/// <param name="blackboardService"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class SpreadDistributionStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    IActorService actorService,
    ILogger<SpreadDistributionStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<SpreadDistributionCommandState>
{
    /// <summary>
    /// load spread distribution state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<SpreadDistributionCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<SpreadDistributionCommandState, SpreadDistributionInsertedEvent>(command);

    /// <summary>
    /// save spread distribution state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the spread distribution query state
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
        var db = dbFactory.OptionPricerDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                SpreadDistributionInsertedEvent e => await UpdateReadModelAsync<SpreadDistributionInsertedEvent, SpreadDistributionInsertedCompleteEvent, SpreadDistributionInsertedFailEvent, SpreadDistributionEntityId>(
                    context, e, async () => await InsertSpreadDistributionsAsync(db, e.PutSpreadDistribution, e.CallSpreadDistribution)),
                SpreadDistributionDeletedEvent e => await UpdateReadModelAsync<SpreadDistributionDeletedEvent, SpreadDistributionDeletedCompleteEvent, SpreadDistributionDeletedFailEvent, SpreadDistributionEntityId>(
                    context, e, async () => await DeleteSpreadDistributionAsync(db, e.EntityId.TradeId, e.EntityId.ValueDate)),
                _ => false
            };
        }

        static async ValueTask InsertSpreadDistributionsAsync(IOptionPricerDbContext db, SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution)
            =>   await db.InsertSpreadDistributionsAsync( putSpreadDistribution, callSpreadDistribution);
        
        static async ValueTask DeleteSpreadDistributionAsync( IOptionPricerDbContext db, int tradeId, DateOnly valueDate)
            => await db.DeleteSpreadDistributionAsync(tradeId, valueDate);
    }
}
