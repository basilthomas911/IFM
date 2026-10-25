using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public interface ITickAggregationService : IAsyncDisposable
{
    bool IsRunning { get; }
    TickAggregationContractStatus GetContractStatus(string contractId);
    TickAggregationTickerStatus GetTickerStatus(string futuresContractId);

    /// <summary>
    /// Reads the latest normalized market-price hot-cache snapshot without checking stream ownership.
    /// </summary>
    /// <param name="contractId">The domain contract identifier.</param>
    /// <param name="snapshot">The latest combined quote and trade snapshot when available.</param>
    /// <returns><see langword="true"/> when the contract cache has observed a price.</returns>
    bool TryGetLastTickPrice(string contractId, out FuturesMarketPriceSnapshot snapshot);

    /// <summary>
    /// Reads the latest normalized futures-option snapshot, including cached Greeks when available,
    /// without consulting stream ownership.
    /// </summary>
    bool TryGetLastOptionTickPrice(string contractId, out OptionTickerPriceSnapshot snapshot);

    /// <summary>Reads the latest complete provider-neutral session open/high/low snapshot.</summary>
    bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }

    /// <summary>Returns whether at least one workflow currently owns the contract's transient stream.</summary>
    bool IsTickDataStreamActive(string contractId);

    /// <summary>Adds an idempotent workflow owner and activates the route for the first owner.</summary>
    bool StartTickDataStream(TickerStreamOwner owner, string contractId);

    /// <summary>Removes a workflow owner and deactivates the route after the final owner leaves.</summary>
    bool StopTickDataStream(TickerStreamOwner owner, string contractId);
    ValueTask StartAsync();
    ValueTask StopAsync();
}
