using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public interface IMarketDataServerApi
{
    IStreamIdCollection StreamIds { get; }

    Task StartAsync(Guid commandId, FuturesContractV2ReadModel[] FuturesContracts, DateOnly valueDate);
    Task StopAsync(Guid commandId);
    Task ResetAsync(Guid commandId, FuturesContractV2ReadModel[] FuturesContracts, DateOnly valueDate);

    Task StartStreamingFuturesTickDataAsync(
        Guid commandId,
        int requestId,
        DateOnly valueDate,
        FuturesContractV2ReadModel contract);

    Task StopStreamingFuturesTickDataAsync(
        Guid commandId,
        int requestId);

    Task StartStreamingFuturesOptionTickDataAsync(
        Guid commandId,
        int optionRequestId,
        DateOnly valueDate,
        DateTime maturityDate,
        FuturesOptionContractReadModel contract,
        double riskFreeRate);

    Task StopStreamingFuturesOptionTickDataAsync(
        Guid commandId, 
        int optionRequestId);

}

public interface IMarketDataServerSnapshotApi : IMarketDataServerApi
{
    /*
    Task<FuturesContractV2ReadModel> ReadFuturesContractAsync(
        int requestId,
        FuturesContractV2ReadModel queryForContract);
    Task<FuturesOptionContractReadModel> ReadFuturesOptionContractAsync(
        int optionRequestId,
        FuturesOptionContractReadModel qfContract);

    Task<FuturesTickDataViewModel> ReadFuturesPriceAsync(int requestId, FuturesContractV2ReadModel contract);
    //Task<FuturesTickData> GetFuturesPriceAsync(int requestId, string contractId);

    Task<(FuturesOptionContractReadModel shortContract, FuturesOptionContractReadModel longContract)> ReadFuturesOptionSpreadAsync(
        FuturesOptionContractReadModel qfShortContract,
        FuturesOptionContractReadModel qfLongContract);

    Task ReadFuturesOptionPriceAsync(int requestId, FuturesOptionContractReadModel optionContract, Action<FuturesOptionTickDataViewModel> optionTickData);
    TickOptionComputation ReadFuturesOptionGreeks(DateOnly valueDate, DateTime maturityDate, FuturesOptionContractReadModel optionContract, double optionPrice, double futuresPrice, double riskFreeRate);
    */

}
