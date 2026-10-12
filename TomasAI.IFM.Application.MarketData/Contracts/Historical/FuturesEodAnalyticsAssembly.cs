using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Combines raw EOD facts with exact independently calculated Analytics signals.</summary>
public sealed record FuturesEodAnalyticsAssembly(
    FuturesEodObservationReadModel Raw,
    IReadOnlyDictionary<MarketAnalyticsSignalFamily, MarketAnalyticsSignalResult> Signals,
    IReadOnlyList<MarketAnalyticsSignalFamily> MissingSignals);
