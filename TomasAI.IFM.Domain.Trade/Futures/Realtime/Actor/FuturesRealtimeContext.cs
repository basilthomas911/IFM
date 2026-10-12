using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
public sealed class FuturesRealtimeContext : EventActorContext, IFuturesRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public FuturesRealtimeContext(IActorSupervisor supervisor, IDbContextFactory dbFactory, IActorService actorService, ILogger<FuturesRealtimeActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesRealtimeActor.ActorName))
    {
        DbFactory = dbFactory;
        ActorService = actorService;
        Logger = logger;
        RouteIndex = new ContractIdRouteIndex(4096);
    }

    public IDbContextFactory DbFactory { get; }
    public IActorService ActorService { get; }
    public ContractIdRouteIndex RouteIndex { get; }
    public ILogger<FuturesRealtimeActor> Logger { get; }
}
