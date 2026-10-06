using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

/// <summary>Immutable business inputs for RemoveTradeLiveFeed; contains no state mutation or external effects.</summary>
/// <param name="OrderId">The proposed order id.</param>
/// <param name="TradeId">The proposed trade id.</param>
/// <param name="TradeLiveFeedState">The proposed trade live feed state.</param>
/// <param name="Accepted">Whether the change is permitted by the current business state.</param>
internal readonly record struct TradeLiveFeedRemoval(
    int OrderId,
    int TradeId,
    TradeLiveFeedStateType TradeLiveFeedState,
    bool Accepted);
