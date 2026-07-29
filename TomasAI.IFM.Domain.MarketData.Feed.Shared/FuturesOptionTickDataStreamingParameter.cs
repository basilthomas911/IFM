using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

public record FuturesOptionTickDataStreamingParameter(
        int RequestId,
        DateOnly ValueDate,
        DateOnly MaturityDate,
        double RiskFreeRate,
        FuturesContractV2ReadModel FuturesContract,
        FuturesOptionContractReadModel FuturesOptionContract);
