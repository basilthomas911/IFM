using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
/// <summary>Preserves the existing market-data-only reset boundary outside the generic actor runtime.</summary>
public static class RealtimeActorCapabilities
{
    public static void RegisterProductionActors()
    {
        foreach (var assembly in new[] {
            typeof(TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor.FuturesOptionTickDataEventActor).Assembly,
            typeof(TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model.FuturesItiSignalCompute).Assembly })
        foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && !type.ContainsGenericParameters
            && typeof(IActor).IsAssignableFrom(type) && typeof(IReplaceableRealtimeActor).IsAssignableFrom(type)))
            RealtimeActorResetPolicy.Register(type);
        RealtimeActorResetPolicy.Register(typeof(TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor.FuturesOptionRealtimeActor));
        RealtimeActorResetPolicy.Register(typeof(TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor.IronCondorTradePositionRealtimeActor));
    }
}
