using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Provides exact common metadata plus an opaque typed-signal payload.</summary>
public sealed record MarketAnalyticsSignalResult(
    MarketAnalyticsSignalFamily Family,
    MarketAnalyticsSignalMetadata Metadata,
    object Value);
