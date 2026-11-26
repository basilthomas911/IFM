using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
public interface IFuturesTradePositionRealtimeContext : IRealtimeActorContext<FuturesTradePositionRealtimeActor>
{
    TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents? PositionIntents => null;

    DailyFuturesExitIntents ExitIntents { get; }
    ValueTask<StrategyPositionSnapshot?> ReadDailyExitRecoveryPositionAsync(StrategyPositionId position);
    TimeProvider TimeProvider { get; }

    TradePlanParameters Parameters { get; }

    ILogger<FuturesTradePositionRealtimeActor> Logger { get; }
}
