using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

/// <summary>
/// Combines the independently advancing latest trade and quote state for one contract.
/// </summary>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="AssetTypeId">The asset type identifying futures or futures options.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="Quote">The bid and ask snapshot, when available.</param>
/// <param name="Trade">The last trade snapshot, when available.</param>
public readonly record struct TickerPriceSnapshot(
    string ContractId,
    uint InstrumentId,
    ushort PublisherId,
    AssetTypeId AssetTypeId,
    DateOnly ValueDate,
    TickerQuoteSnapshot? Quote,
    TickerTradeSnapshot? Trade);
