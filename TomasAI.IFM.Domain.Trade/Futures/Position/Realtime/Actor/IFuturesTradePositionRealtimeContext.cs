using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
public interface IFuturesTradePositionRealtimeContext : IRealtimeActorContext<FuturesTradePositionRealtimeActor>
{
    TimeProvider TimeProvider { get; }

    TradePlanParameters Parameters { get; }

    ILogger<FuturesTradePositionRealtimeActor> Logger { get; }
}
