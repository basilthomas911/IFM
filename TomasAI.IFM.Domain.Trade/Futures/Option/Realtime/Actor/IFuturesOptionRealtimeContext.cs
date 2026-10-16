using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
public interface IFuturesOptionRealtimeContext : IRealtimeActorContext<FuturesOptionRealtimeActor>
{
    TradeRealtimeCacheRegistry? RealtimeCache => null;
    IDbContextFactory DbFactory { get; }

    IActorService ActorService { get; }

    ContractIdRouteIndex RouteIndex { get; }

    ILogger<FuturesOptionRealtimeActor> Logger { get; }
}
