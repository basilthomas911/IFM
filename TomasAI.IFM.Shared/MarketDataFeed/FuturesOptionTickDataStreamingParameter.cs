using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public record FuturesOptionTickDataStreamingParameter(
        int RequestId,
        DateOnly ValueDate,
        DateOnly MaturityDate,
        double RiskFreeRate,
        FuturesContractV2ReadModel FuturesContract,
        FuturesOptionContractReadModel FuturesOptionContract);
