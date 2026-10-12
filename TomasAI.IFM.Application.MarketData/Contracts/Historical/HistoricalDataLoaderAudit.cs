using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Captures deterministic coverage, gap, and roll audit results.</summary>
public sealed record HistoricalDataLoaderAudit(
    int ValidSessionCount,
    IReadOnlyList<HistoricalDataLoaderGap> Gaps,
    IReadOnlyList<HistoricalDataLoaderRoll> Rolls);
