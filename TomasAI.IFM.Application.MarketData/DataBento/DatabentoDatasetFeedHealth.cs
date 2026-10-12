using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Native transport and managed-drain health for one Databento dataset. This preserves the
/// terminal status and warning that explain why a completed aggregation reader stopped.
/// </summary>
public readonly record struct DatabentoDatasetFeedHealth(
    string Dataset,
    Guid GenerationId,
    FeedHealthSnapshot Health,
    TickAggregationMetricsSnapshot AggregationMetrics);
