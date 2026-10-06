using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

/// <summary>Immutable business inputs for DeleteFuturesBarData; contains no state mutation or external effects.</summary>
/// <param name="BarDataId">The proposed bar data id.</param>
internal readonly record struct DeleteFuturesBarDataRequest(
    FuturesBarDataId BarDataId);
