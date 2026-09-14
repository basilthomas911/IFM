using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;

public interface IIronCondorTradePositionRealtimeContext : IRealtimeActorContext<IronCondorTradePositionRealtimeActor>
{
    TimeProvider TimeProvider { get; }
    TradePlanParameters Parameters { get; }
    ILogger<IronCondorTradePositionRealtimeActor> Logger { get; }
}

public sealed class IronCondorTradePositionRealtimeContext : EventActorContext,
    IRealtimeActorContext<IronCondorTradePositionRealtimeActor>, IIronCondorTradePositionRealtimeContext
{
    public IronCondorTradePositionRealtimeContext(IActorSupervisor supervisor,
        ILogger<IronCondorTradePositionRealtimeActor> logger)
        : base(supervisor, new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName)) => Logger = logger;

    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<IronCondorTradePositionRealtimeActor> Logger { get; }
}
