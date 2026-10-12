using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
public sealed class FuturesTradePositionRealtimeContext : EventActorContext, IFuturesTradePositionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<FuturesTradePositionRealtimeActor> logger) : base(supervisor, new(ActorType.Realtime, FuturesTradePositionRealtimeActor.ActorName)) => Logger = logger;
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<FuturesTradePositionRealtimeActor> Logger { get; }
}
