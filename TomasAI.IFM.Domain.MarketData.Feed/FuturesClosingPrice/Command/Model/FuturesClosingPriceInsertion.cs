using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Model;

/// <summary>Immutable business inputs for InsertFuturesClosingPrice; contains no state mutation or external effects.</summary>
/// <param name="FuturesClosingPriceId">The proposed futures closing price id.</param>
/// <param name="ClosingPrice">The proposed closing price.</param>
/// <param name="Accepted">Whether the change is permitted by the current business state.</param>
internal readonly record struct FuturesClosingPriceInsertion(
    FuturesDataId FuturesClosingPriceId,
    decimal ClosingPrice,
    bool Accepted);
