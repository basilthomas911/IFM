using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public interface IMarketDataApi
{
    IStreamIdCollection StreamIds { get; }

    bool Start(Action<int, string> errorMessageHandler=default!);
    void Stop();

    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        int requestId,
        FuturesContractV2ReadModel queryForContract);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        int optionRequestId,
        FuturesOptionContractReadModel qfContract);

    Task<FuturesTickDataV2ReadModel> GetFuturesPriceAsync(int requestId, FuturesContractV2ReadModel contract);

    Task<(FuturesOptionContractReadModel? shortContract, FuturesOptionContractReadModel? longContract)> GetFuturesOptionSpreadAsync(
        FuturesOptionContractReadModel qfShortContract,
        FuturesOptionContractReadModel qfLongContract);

    Task GetFuturesOptionPriceAsync(int requestId, DateOnly valueDate,FuturesOptionContractReadModel optionContract, Action<FuturesOptionTickDataV2ReadModel> optionTickData);
    TickOptionComputation GetFuturesOptionGreeks(DateOnly valueDate, DateOnly maturityDate, FuturesOptionContractReadModel optionContract, double optionPrice, double futuresPrice, double riskFreeRate);

    void StartStreamingFuturesTickData(
        int requestId,
        DateOnly valueDate,
        FuturesContractV2ReadModel contract);

    bool StopStreamingFuturesTickData(int requestId);

    void StartStreamingFuturesOptionTickData(
        int optionRequestId,
        DateOnly valueDate,
        DateOnly maturityDate,
        FuturesOptionContractReadModel contract,
        double riskFreeRate);

    bool StopStreamingFuturesOptionTickData(int optionRequestId);

    void StartStreamingFuturesOptionQuoteData(
      int optionRequestId,
      FuturesOptionContractReadModel optionContract,
      FuturesOptionQuoteReadModel optionQuote);

    bool StopStreamingFuturesOptionQuoteData(int optionRequestId);

}

public interface IMarketDataSnapshotApi : IMarketDataApi
{

}
