using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;

public interface IBrokerOrderRealtimeContext : IRealtimeActorContext<BrokerOrderRealtimeActor>
{
    ITradeBroker TradeBroker { get; }
    ILogger<BrokerOrderRealtimeActor> Logger { get; }
}

public sealed class BrokerOrderRealtimeContext : EventActorContext,
    IRealtimeActorContext<BrokerOrderRealtimeActor>, IBrokerOrderRealtimeContext
{
    public BrokerOrderRealtimeContext(IActorSupervisor supervisor, ITradeBroker tradeBroker,
        ILogger<BrokerOrderRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, BrokerOrderRealtimeActor.ActorName))
    {
        TradeBroker = tradeBroker;
        Logger = logger;
    }

    public ITradeBroker TradeBroker { get; }
    public ILogger<BrokerOrderRealtimeActor> Logger { get; }
}
