using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Model;

/// <summary>Immutable business inputs for InsertFuturesTickData; contains no state mutation or external effects.</summary>
/// <param name="FuturesContract">The proposed futures contract.</param>
/// <param name="FuturesTickData">The proposed futures tick data.</param>
internal readonly record struct FuturesTickDataInsertion(
    FuturesContractV3ReadModel FuturesContract,
    FuturesTickDataV2ReadModel FuturesTickData);
