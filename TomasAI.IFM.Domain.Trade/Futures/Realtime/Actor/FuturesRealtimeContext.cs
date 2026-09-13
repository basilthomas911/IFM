using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;

public interface IFuturesRealtimeContext : IRealtimeActorContext<FuturesRealtimeActor>
{
    IDbContextFactory DbFactory { get; }
    IActorService ActorService { get; }
    ContractIdRouteIndex RouteIndex { get; }
    ILogger<FuturesRealtimeActor> Logger { get; }
}

public sealed class FuturesRealtimeContext : EventActorContext,
    IRealtimeActorContext<FuturesRealtimeActor>, IFuturesRealtimeContext
{
    public FuturesRealtimeContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IActorService actorService,
        ILogger<FuturesRealtimeActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Realtime, FuturesRealtimeActor.ActorName))
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
