using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

/// <summary>Immutable business inputs for InsertVixFuturesEodData; contains no state mutation or external effects.</summary>
/// <param name="VixFuturesTickData">The proposed vix futures tick data.</param>
internal readonly record struct VixFuturesEodDataInsertion(
    FuturesTickDataV2ReadModel VixFuturesTickData);
