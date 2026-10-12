using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
public interface IVerticalSpreadTradePositionRealtimeContext : IRealtimeActorContext<VerticalSpreadTradePositionRealtimeActor>
{
    TimeProvider TimeProvider { get; }

    TradePlanParameters Parameters { get; }

    ILogger<VerticalSpreadTradePositionRealtimeActor> Logger { get; }
}
