using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Historical;

/// <summary>Reports the non-billable estimate for a historical provider request.</summary>
/// <param name="EstimatedCostUsd">The estimated cost usd.</param>
/// <param name="EstimatedBytes">The estimated bytes.</param>
/// <param name="EstimatedRecords">The estimated records.</param>
/// <param name="EstimatedAtUtc">The estimated at utc.</param>
public sealed record HistoricalProviderEstimate(
    decimal EstimatedCostUsd,
    long EstimatedBytes,
    long EstimatedRecords,
    DateTimeOffset EstimatedAtUtc);
