using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.State;

public sealed class TradeOrderStateRepository(IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<TradeOrderCommandActor> eventProjector, ILogger<TradeOrderStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory,eventSource,actorService,logger),
      IEventSourceActorStateRepository<TradeOrderCommandState>
{
    public ValueTask<TradeOrderCommandState> LoadStateAsync(ICommand command) => new(LoadStateAsync<TradeOrderCommandState>(command));
    public async ValueTask SaveStateAsync(ICommandActorContext context, TradeOrderCommandState state, ICommand command) =>
        await SaveStateAndDenormalizeEventsAsync(context,state,command).ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) =>
        eventProjector.DomainEventsProjectionAsync(events);
}
