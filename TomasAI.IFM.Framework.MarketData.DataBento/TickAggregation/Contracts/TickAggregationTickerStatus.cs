using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>Initializes a new TickAggregationTickerStatus instance.</summary>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="ServiceRunning">The service running.</param>
/// <param name="TickerConfigured">The ticker configured.</param>
/// <param name="TickerRunning">The ticker running.</param>
public readonly record struct TickAggregationTickerStatus(
    string FuturesContractId,
    bool ServiceRunning,
    bool TickerConfigured,
    bool TickerRunning);
