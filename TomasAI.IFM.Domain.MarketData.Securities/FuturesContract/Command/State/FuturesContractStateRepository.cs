using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;

public class FuturesContractStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
    ILogger<FuturesContractStateRepository> logger) 
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesContractCommandState>
{
    /// <summary>
    /// load futures contract state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesContractCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesContractCommandState, FuturesContractAddedEvent>(command);

    /// <summary>
    /// save futures contract state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesContractCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures contract query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the futures
    /// contract query state accordingly. It is typically called as part of the event sourcing workflow to keep the read
    /// model in sync with the latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.SecuritiesDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesContractAddedEvent e => await UpdateReadModelAsync<FuturesContractAddedEvent, FuturesContractAddedCompleteEvent, FuturesContractAddedFailEvent, FuturesContractId>(
                    context, e, () => InsertFuturesContractAsync(db, e.Contract)),
                FuturesContractChangedEvent e => await UpdateReadModelAsync<FuturesContractChangedEvent, FuturesContractChangedCompleteEvent, FuturesContractChangedFailEvent, FuturesContractId>(
                    context, e, () => UpdateFuturesContractAsync(db, e.OriginalContractId, e.Contract)),
                FuturesContractRemovedEvent e => await UpdateReadModelAsync<FuturesContractRemovedEvent, FuturesContractRemovedCompleteEvent, FuturesContractRemovedFailEvent, FuturesContractId>(
                    context, e, () => DeleteFuturesContractAsync(db, e.ContractId)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesContractAsync(ISecuritiesDbContext db, FuturesContractV2ReadModel futuresContract)
            => await db.InsertFuturesContractAsync(futuresContract);

        static async ValueTask UpdateFuturesContractAsync(ISecuritiesDbContext db, FuturesContractId contractId, FuturesContractV2ReadModel futuresContract)
            => await db.UpdateFuturesContractAsync(contractId, futuresContract);

        static async ValueTask DeleteFuturesContractAsync(ISecuritiesDbContext db, FuturesContractId contractId)
            => await db.DeleteFuturesContractAsync(contractId);
    }
}
