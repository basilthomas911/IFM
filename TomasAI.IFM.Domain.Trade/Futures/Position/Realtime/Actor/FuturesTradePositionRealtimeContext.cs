using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
public sealed class FuturesTradePositionRealtimeContext : EventActorContext, IFuturesTradePositionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<FuturesTradePositionRealtimeActor> logger, TimeProvider? timeProvider = null, DailyFuturesExitIntents? exitIntents = null, TomasAI.IFM.Application.Storage.IDbContextFactory? databases = null, TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents? positionIntents = null) : base(supervisor, new(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName)) { PositionIntents = positionIntents ?? throw new InvalidOperationException("Durable position intent store required."); Logger = logger; TimeProvider = timeProvider ?? TimeProvider.System; ExitIntents = exitIntents ?? throw new InvalidOperationException("Durable Daily exit intent store required."); this.databases = databases; }
    readonly TomasAI.IFM.Application.Storage.IDbContextFactory? databases;
    public DailyFuturesExitIntents ExitIntents { get; }
    public async ValueTask<StrategyPositionSnapshot?> ReadDailyExitRecoveryPositionAsync(StrategyPositionId position)
        => databases is null ? throw new InvalidOperationException("Daily exit recovery requires position storage.")
            : await databases.TradeDb.GetStrategyPositionAsync(position, RealtimeGeneration.Token).ConfigureAwait(false);
    public TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents PositionIntents { get; }
    public TimeProvider TimeProvider { get; }
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<FuturesTradePositionRealtimeActor> Logger { get; }
}
