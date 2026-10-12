using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Describes a resumable provider batch job.</summary>
/// <param name="ProviderJobId">The provider job id.</param>
/// <param name="State">The store holding the option-chain session state.</param>
/// <param name="CostUsd">The cost usd.</param>
/// <param name="RecordCount">The record count.</param>
/// <param name="BilledBytes">The billed bytes.</param>
/// <param name="ProgressPercent">The progress percent.</param>
/// <param name="ErrorMessage">The error message.</param>
public sealed record HistoricalProviderJob(
    string ProviderJobId,
    HistoricalProviderJobState State,
    decimal CostUsd,
    long RecordCount,
    long BilledBytes,
    byte ProgressPercent,
    string ErrorMessage);
