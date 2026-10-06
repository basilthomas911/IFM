using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Model;

/// <summary>Immutable business inputs for StartMarketDataFeed; contains no state mutation or external effects.</summary>
/// <param name="FuturesContracts">The proposed futures contracts.</param>
/// <param name="ValueDate">The proposed value date.</param>
/// <param name="ResetStream">The proposed reset stream.</param>
internal readonly record struct MarketDataFeedStart(
    FuturesContractV3ReadModel[]? FuturesContracts,
    DateOnly ValueDate,
    bool ResetStream);
