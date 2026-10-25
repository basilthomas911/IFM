using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Reports the combined non-billable estimate for an application request.</summary>
public sealed record MarketDataHistoricalEstimate(
    Guid DataLoadAttemptId,
    decimal EstimatedCostUsd,
    long EstimatedBytes,
    long EstimatedRecords,
    string RequestSha256,
    DateTimeOffset EstimatedAtUtc);
