using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Describes one expected trading date missing from normalized history.</summary>
public sealed record HistoricalDataLoaderGap(DateOnly ValueDate, string SeriesKey, string ReasonCode);
