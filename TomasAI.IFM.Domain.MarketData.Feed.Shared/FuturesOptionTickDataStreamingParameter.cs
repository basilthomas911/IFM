using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

public record FuturesOptionTickDataStreamingParameter(
        int RequestId,
        DateOnly ValueDate,
        DateOnly MaturityDate,
        double RiskFreeRate,
        FuturesContractV3ReadModel FuturesContract,
        FuturesOptionContractReadModel FuturesOptionContract);
