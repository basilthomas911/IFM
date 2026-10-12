using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the VX term-structure Realtime actor.</summary>
public interface IFuturesVxTermStructureSignalRealtimeContext
    : IRealtimeActorContext<FuturesVxTermStructureSignalRealtimeActor>
{
    IMarketDataApi MarketDataApi { get; }
    ILogger<FuturesVxTermStructureSignalRealtimeActor> Logger { get; }
}
