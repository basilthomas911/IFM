using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Model;

/// <summary>Immutable business inputs for StartFuturesTickDataStreaming; contains no state mutation or external effects.</summary>
/// <param name="FuturesContract">The proposed futures contract.</param>
/// <param name="ValueDate">The proposed value date.</param>
/// <param name="ResetStream">The proposed reset stream.</param>
internal readonly record struct FuturesTickDataStreamingStart(
    FuturesContractV3ReadModel FuturesContract,
    DateOnly ValueDate,
    bool ResetStream);
