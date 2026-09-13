using Microsoft.Extensions.Logging;using TomasAI.IFM.Application.Storage;using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;using TomasAI.IFM.Shared.EventModelActor;using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
public interface IFuturesOptionRealtimeContext:IRealtimeActorContext<FuturesOptionRealtimeActor>{IDbContextFactory DbFactory{get;}IActorService ActorService{get;}MarketInstrumentRouteIndex RouteIndex{get;}ILogger<FuturesOptionRealtimeActor>Logger{get;}}
public sealed class FuturesOptionRealtimeContext:EventActorContext,IRealtimeActorContext<FuturesOptionRealtimeActor>,IFuturesOptionRealtimeContext
{
 public FuturesOptionRealtimeContext(IActorSupervisor supervisor,IDbContextFactory db,IActorService actors,ILogger<FuturesOptionRealtimeActor>logger):base(supervisor,new(ActorType.Realtime,FuturesOptionRealtimeActor.ActorName)){DbFactory=db;ActorService=actors;Logger=logger;RouteIndex=new(4096);}
 public IDbContextFactory DbFactory{get;}public IActorService ActorService{get;}public MarketInstrumentRouteIndex RouteIndex{get;}public ILogger<FuturesOptionRealtimeActor>Logger{get;}
}
