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
