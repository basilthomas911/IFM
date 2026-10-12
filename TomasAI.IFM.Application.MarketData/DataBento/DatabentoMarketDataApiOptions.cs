using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

public sealed record DatabentoMarketDataApiOptions
{
    /// <summary>
    /// Maximum age at which an accepted hot-cache value remains usable by a
    /// realtime interpreter. This is the red boundary of the shared feed policy.
    /// </summary>
    public TimeSpan MaximumLastPriceAge { get; init; } = TimeSpan.FromMinutes(15);
}
