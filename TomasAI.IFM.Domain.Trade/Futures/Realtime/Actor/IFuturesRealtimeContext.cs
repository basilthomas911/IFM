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
