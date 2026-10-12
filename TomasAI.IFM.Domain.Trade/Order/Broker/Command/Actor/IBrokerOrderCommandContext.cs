using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
public interface IBrokerOrderCommandContext : ICommandActorContext<BrokerOrderCommandActor>
{
    IDbContextFactory DbFactory { get; }

    IEventSourceActorDbContext DbEventSource { get; }

    IDurableReplayQueue DurableReplayQueue { get; }

    IBlackboardService BlackboardService { get; }

    IActorService ActorService { get; }

    ITradeBroker TradeBroker { get; }

    IBrokerOrderReadStore ReadStore { get; }

    IBrokerAccountReadStore BrokerAccountStore { get; }

    BrokerOrderObservationBridge ObservationBridge { get; }

    IEventProjector<BrokerOrderCommandActor> EventProjector { get; }

    IEventSourceActorStateRepository<BrokerOrderCommandState> StateRepository { get; }

    ILogger<BrokerOrderCommandActor> Logger { get; }
}
