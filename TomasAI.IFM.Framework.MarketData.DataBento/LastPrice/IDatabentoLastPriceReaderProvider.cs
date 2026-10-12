using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;

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
