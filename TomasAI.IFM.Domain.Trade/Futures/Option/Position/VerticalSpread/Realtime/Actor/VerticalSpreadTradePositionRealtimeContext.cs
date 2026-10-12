using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
public sealed class VerticalSpreadTradePositionRealtimeContext : EventActorContext, IVerticalSpreadTradePositionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public VerticalSpreadTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<VerticalSpreadTradePositionRealtimeActor> logger) : base(supervisor, new(ActorType.Realtime, VerticalSpreadTradePositionRealtimeActor.ActorName)) => Logger = logger;
    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<VerticalSpreadTradePositionRealtimeActor> Logger { get; }
}
