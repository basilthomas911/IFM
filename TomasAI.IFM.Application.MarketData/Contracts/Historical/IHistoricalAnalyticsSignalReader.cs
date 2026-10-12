using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Reads already-calculated signals; implementations never derive a value from raw EOD.</summary>
public interface IHistoricalAnalyticsSignalReader
{
    /// <summary>Gets a signal whose ObservationId exactly matches the raw session.</summary>
    ValueTask<MarketAnalyticsSignalResult?> GetExactAsync(
        MarketAnalyticsSignalFamily family,
        MarketSeriesIdentity seriesIdentity,
        TimeFrameType timeFrame,
        FuturesTradeSessionBarId observationId,
        CancellationToken cancellationToken);
}
