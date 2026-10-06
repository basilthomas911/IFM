using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;

/// <summary>Immutable business inputs for StartFuturesOptionTickDataStreaming; contains no state mutation or external effects.</summary>
/// <param name="FuturesContract">The proposed futures contract.</param>
/// <param name="BaseFuturesContract">The proposed base futures contract.</param>
/// <param name="ValueDate">The proposed value date.</param>
/// <param name="MaturityDate">The proposed maturity date.</param>
/// <param name="RiskFreeRate">The proposed risk free rate.</param>
internal readonly record struct FuturesOptionTickDataStreamingStart(
    FuturesOptionContractReadModel FuturesContract,
    FuturesContractV3ReadModel BaseFuturesContract,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    double RiskFreeRate);
