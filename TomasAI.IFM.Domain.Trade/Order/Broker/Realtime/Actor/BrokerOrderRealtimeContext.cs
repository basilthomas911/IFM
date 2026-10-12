using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
public sealed class BrokerOrderRealtimeContext : EventActorContext, IBrokerOrderRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public BrokerOrderRealtimeContext(IActorSupervisor supervisor, ITradeBroker tradeBroker, ILogger<BrokerOrderRealtimeActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Realtime, BrokerOrderRealtimeActor.ActorName))
    {
        TradeBroker = tradeBroker;
        Logger = logger;
    }

    public ITradeBroker TradeBroker { get; }
    public ILogger<BrokerOrderRealtimeActor> Logger { get; }
}
