using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

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

/// <summary>
/// Resolves stable, contract-bound reader handles without starting a feed or
/// subscription.
/// </summary>
public interface IDatabentoLastPriceReaderProvider
{
    IFuturesLastPriceReader GetFuturesReader(string futuresContractId, DateOnly valueDate);
    IFuturesOptionLastPriceReader GetFuturesOptionReader(
        string futuresOptionContractId,
        DateOnly valueDate);
}

public interface IDatabentoLastPriceStore :
    IDatabentoLastPriceWriter,
    IDatabentoLastPriceReaderProvider,
    IDisposable
{
    DateOnly ValueDate { get; }
    int Capacity { get; }
    int Count { get; }
    bool IsActive { get; }
    void Invalidate();
}
