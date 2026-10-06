using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

/// <summary>Immutable business inputs for StopFuturesBarDataStreaming; contains no state mutation or external effects.</summary>
/// <param name="EntityId">The proposed entity id.</param>
internal readonly record struct FuturesBarDataStreamingStop(
    FuturesBarDataStreamingId EntityId);
