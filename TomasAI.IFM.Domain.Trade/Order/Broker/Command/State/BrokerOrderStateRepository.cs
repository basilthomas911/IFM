using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;

public sealed class BrokerOrderStateRepository(IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource, IActorService actorService,
    IEventProjector<BrokerOrderCommandActor> eventProjector, ILogger<BrokerOrderStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<BrokerOrderCommandState>
{
    public ValueTask<BrokerOrderCommandState> LoadStateAsync(ICommand command) => new(LoadStateAsync<BrokerOrderCommandState>(command));
    public async ValueTask SaveStateAsync(ICommandActorContext context, BrokerOrderCommandState state, ICommand command) => await SaveStateAndDenormalizeEventsAsync(context, state, command).ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) => eventProjector.DomainEventsProjectionAsync(events);
}
