namespace TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

/// <summary>
/// Provides lock-free, non-consuming access to the latest futures trade and
/// quote snapshots held by the active market-data provider.
/// </summary>
public interface IFuturesLastPriceReader
{
    string FuturesContractId { get; }
    DateOnly ValueDate { get; }

    bool TryGetLastTrade(out LastTradeTickSnapshot snapshot);
    bool TryGetLastQuote(out LastQuoteTickSnapshot snapshot);
}
