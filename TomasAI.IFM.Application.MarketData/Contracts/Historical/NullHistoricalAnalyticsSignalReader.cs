using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Reports signals as unavailable until their MDSI migration gate registers a typed reader.</summary>
public sealed class NullHistoricalAnalyticsSignalReader : IHistoricalAnalyticsSignalReader
{
    /// <inheritdoc />
    public ValueTask<MarketAnalyticsSignalResult?> GetExactAsync(
        MarketAnalyticsSignalFamily family,
        MarketSeriesIdentity seriesIdentity,
        TimeFrameType timeFrame,
        FuturesTradeSessionBarId observationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<MarketAnalyticsSignalResult?>(null);
    }
}
