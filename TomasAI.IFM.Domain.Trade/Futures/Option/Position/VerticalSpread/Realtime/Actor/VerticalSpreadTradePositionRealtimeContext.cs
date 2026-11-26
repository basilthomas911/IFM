using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
public sealed class VerticalSpreadTradePositionRealtimeContext : EventActorContext, IVerticalSpreadTradePositionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public VerticalSpreadTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<VerticalSpreadTradePositionRealtimeActor> logger, TimeProvider? timeProvider = null, TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents? positionIntents = null, TomasAI.IFM.Application.Storage.IDbContextFactory? databases = null) : base(supervisor, new(ActorType.Realtime, VerticalSpreadTradePositionRealtimeActor.ActorName)) { PositionIntents = positionIntents ?? throw new InvalidOperationException("Durable position intent store required."); this.databases=databases; Logger = logger; TimeProvider = timeProvider ?? TimeProvider.System; }
    public TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents PositionIntents { get; }
    readonly TomasAI.IFM.Application.Storage.IDbContextFactory? databases;
    public async ValueTask<StrategyPositionSnapshot?> ReadRecoveryPositionAsync(StrategyPositionId position)
        => databases is null ? throw new InvalidOperationException("Position intent recovery requires position storage.")
            : await databases.TradeDb.GetStrategyPositionAsync(position,RealtimeGeneration.Token).ConfigureAwait(false);
    public TimeProvider TimeProvider { get; }
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<VerticalSpreadTradePositionRealtimeActor> Logger { get; }
}
