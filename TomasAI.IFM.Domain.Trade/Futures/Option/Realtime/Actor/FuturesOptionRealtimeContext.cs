using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
public sealed class FuturesOptionRealtimeContext : EventActorContext, IFuturesOptionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesOptionRealtimeContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IActorService actorService, ILogger<FuturesOptionRealtimeActor> logger, FuturesOptionRoutingReceipts routingReceipts, TradeRealtimeCacheRegistry? realtimeCache = null, TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents? positionIntents = null) : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesOptionRealtimeActor.ActorName))
    { PositionIntents = positionIntents ?? throw new InvalidOperationException("Durable position intent store required.");
        RoutingReceipts = routingReceipts;
        DbFactory = dbFactory;
        ActorService = actorService;
        Logger = logger;
        RouteIndex = new ContractIdRouteIndex(4096);
        RealtimeCache = realtimeCache;
    }

    public TomasAI.IFM.Domain.Trade.Persistence.Realtime.PositionRealtimeIntents PositionIntents { get; }
    public TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model.OptionQuoteRoutingProgress RoutingProgress { get; } = new();
    public FuturesOptionRoutingReceipts RoutingReceipts { get; }
    public IDbContextFactory DbFactory { get; }
    public IActorService ActorService { get; }
    public ContractIdRouteIndex RouteIndex { get; }
    public TradeRealtimeCacheRegistry? RealtimeCache { get; }
    public ILogger<FuturesOptionRealtimeActor> Logger { get; }
}
