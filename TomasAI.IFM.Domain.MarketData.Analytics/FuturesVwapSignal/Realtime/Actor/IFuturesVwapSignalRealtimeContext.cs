using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;

/// <summary>Defines readonly services required by the stateless VWAP Realtime actor.</summary>
public interface IFuturesVwapSignalRealtimeContext
    : IRealtimeActorContext<FuturesVwapSignalRealtimeActor>
{
    IMarketDataApi MarketDataApi { get; }
    IMarketSessionCalendar SessionCalendar { get; }
    ILogger<FuturesVwapSignalRealtimeActor> Logger { get; }
}
