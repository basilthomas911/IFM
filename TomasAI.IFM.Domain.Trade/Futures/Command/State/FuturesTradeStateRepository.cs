using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Command.State;
/// <summary>Accepts source state only after audit and required read rows commit together.</summary>
public sealed class FuturesTradeStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext source, IActorService actors, IEventProjector<FuturesTradeCommandActor> projector, ILogger<FuturesTradeStateRepository> logger, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(factory, source, actors, logger), IEventSourceActorStateRepository<FuturesTradeCommandState>
{
    public ValueTask<FuturesTradeCommandState> LoadStateAsync(ICommand command) => new(LoadStateAsync<FuturesTradeCommandState>(command));
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesTradeCommandState state, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (context is not IFuturesTradeCommandContext typed || !string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);
            return;
        }

        var pending = new DomainEventCollection(state.Events);
        await TradeAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions).ConfigureAwait(false);
        state.AcceptChanges();
        await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
