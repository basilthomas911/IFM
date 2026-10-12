using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

/// <summary>Initializes a new TickContractMapping instance.</summary>
/// <param name="Dataset">The dataset.</param>
/// <param name="DefinitionDate">The definition date.</param>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="AssetTypeId">The asset type identifying futures or futures options.</param>
/// <param name="ContractDetails">The reviewed reference details for the mapped contract.</param>
public readonly record struct TickContractMapping(
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    string ContractId,
    AssetTypeId AssetTypeId,
    TickerContractDetails? ContractDetails = null);
