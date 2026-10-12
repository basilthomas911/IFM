using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>Initializes a new TickAggregationContractStatus instance.</summary>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="AssetTypeId">The asset type identifying futures or futures options.</param>
/// <param name="ServiceRunning">The service running.</param>
/// <param name="ContractConfigured">The contract configured.</param>
/// <param name="ContractRunning">The contract running.</param>
/// <param name="StreamActive">Whether the contract stream is activated.</param>
/// <param name="LastSourceRecordObservedAtUtc">The last source record observed at utc.</param>
/// <param name="LastMarketPricePublishedAtUtc">The last market price published at utc.</param>
/// <param name="LastDurableTickPublishedAtUtc">The last durable tick published at utc.</param>
/// <param name="StreamActivatedAtUtc">The UTC instant when the stream was activated.</param>
/// <param name="LastAcceptedCacheUpdateAtUtc">The UTC time of the latest accepted cache update.</param>
/// <param name="LastAcceptedSourceEventAtUtc">The source event time of the latest accepted update.</param>
/// <param name="AcceptedCacheUpdates">The accepted cache updates.</param>
/// <param name="RejectedCacheUpdates">The rejected cache updates.</param>
public readonly record struct TickAggregationContractStatus(
    string ContractId,
    AssetTypeId AssetTypeId,
    bool ServiceRunning,
    bool ContractConfigured,
    bool ContractRunning,
    bool StreamActive = false,
    DateTimeOffset? LastSourceRecordObservedAtUtc = null,
    DateTimeOffset? LastMarketPricePublishedAtUtc = null,
    DateTimeOffset? LastDurableTickPublishedAtUtc = null,
    DateTimeOffset? StreamActivatedAtUtc = null,
    DateTimeOffset? LastAcceptedCacheUpdateAtUtc = null,
    DateTimeOffset? LastAcceptedSourceEventAtUtc = null,
    long AcceptedCacheUpdates = 0,
    long RejectedCacheUpdates = 0)
{
    /// <summary>Gets the route-level health of accepted Databento input.</summary>
    /// <param name="utcNow">The current UTC instant used to evaluate freshness.</param>
    /// <returns>The health at result.</returns>
    public DatabentoLiveFeedHealthState HealthAt(DateTimeOffset utcNow) =>
        DatabentoLiveFeedHealthPolicy.Evaluate(
            StreamActive,
            StreamActivatedAtUtc,
            LastAcceptedCacheUpdateAtUtc,
            LastAcceptedSourceEventAtUtc,
            utcNow);
}
