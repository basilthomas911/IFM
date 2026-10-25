using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

/// <summary>
/// The aggregation-side write boundary for the bounded, epoch-local DataBento
/// latest-value store.
/// </summary>
public interface IDatabentoLastPriceWriter
{
    void RegisterContract(string contractId, AssetTypeId assetTypeId);
    bool TryUpdateTrade(LastTradeTickSnapshot snapshot);
    bool TryUpdateQuote(LastQuoteTickSnapshot snapshot);
    bool TryUpdateTradeWithGreeks(LastTradeTickWithGreeksSnapshot snapshot);
    bool TryUpdateQuoteWithGreeks(LastQuoteTickWithGreeksSnapshot snapshot);
}
