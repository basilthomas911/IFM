using TomasAI.IFM.Application.EventSourcing.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
/// <summary>Commits position source and material rows together before admitting notifications.</summary>
public sealed class FuturesPositionStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext source, IActorService actors, IEventProjector<FuturesTradePositionCommandActor> projector, ILogger<FuturesPositionStateRepository> logger, IPostgresEventTransaction? transactions = null) : BaseEventSourceActorRepository(factory, source, actors, logger), IResidentEventSourceActorStateRepository<FuturesPositionCommandState>
{
    public ValueTask<FuturesPositionCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    public async ValueTask<FuturesPositionCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken) => await LoadStateAsync<FuturesPositionCommandState>(command, cancellationToken).ConfigureAwait(false);
    public ValueTask SaveStateAsync(ICommandActorContext context, FuturesPositionCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesPositionCommandState state, ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (context is not IFuturesPositionCommandContext typed || !string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
            return;
        }

        var pending = new DomainEventCollection(state.Events);
        await TradeAtomicPersistence.CommitAsync(command, pending, state.CommittedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
        await projector.DomainEventsProjectionAsync(pending).ConfigureAwait(false);
    }

    public async ValueTask SaveResidentEventsAsync(ICommandActorContext context, DomainEventCollection events, ICommand command, long expectedStreamVersion, CancellationToken cancellationToken)
    {
        DomainEventCollection committed;
        if (context is IFuturesPositionCommandContext typed && string.Equals(typed.DbFactory.TradeDb.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
        {
            await TradeAtomicPersistence.CommitAsync(command, events, expectedStreamVersion, typed.DbEventSource, typed.DbFactory.TradeDb, transactions, cancellationToken).ConfigureAwait(false);
            committed = events;
        }
        else
            committed = await EventSourceDb.SaveCommandEventsAtomicallyAsync(command, events, expectedStreamVersion, cancellationToken).ConfigureAwait(false);
        await projector.DomainEventsProjectionAsync(committed).ConfigureAwait(false);
    }

    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => projector.DomainEventsProjectionAsync(domainEvents);
}
