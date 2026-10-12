using TomasAI.IFM.Domain.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Application.Storage.TradeDb;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.State;
public sealed class TradeOrderStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource, IActorService actorService, IEventProjector<TradeOrderCommandActor> eventProjector, ILogger<TradeOrderStateRepository> logger, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<TradeOrderCommandState>
{
    public ValueTask<TradeOrderCommandState> LoadStateAsync(ICommand command) => new(LoadStateAsync<TradeOrderCommandState>(command));
    public async ValueTask SaveStateAsync(ICommandActorContext context, TradeOrderCommandState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (context is not ITradeOrderCommandContext typed || !string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);
            return;
        }

        var pending = new DomainEventCollection(state.Events);
        await TomasAI.IFM.Domain.Trade.Persistence.TradeAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions).ConfigureAwait(false);
        state.AcceptChanges();
        await eventProjector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
