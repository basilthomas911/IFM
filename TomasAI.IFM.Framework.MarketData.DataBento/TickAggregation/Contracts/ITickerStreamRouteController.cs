using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>
/// Controls transient delivery when a contract obtains its first stream owner or
/// releases its final stream owner.
/// </summary>
public interface ITickerStreamRouteController
{
    void Activate(TickContractMapping mapping);
    void Deactivate(TickContractMapping mapping);
}
