using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

/// <summary>Immutable business inputs for StopMarketDataFeed; contains no state mutation or external effects.</summary>
/// <param name="ValueDate">The proposed value date.</param>
internal readonly record struct MarketDataFeedStop(
    DateOnly ValueDate);
