using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi;

/// <summary>
/// Defines an interface for interacting with market data, including futures and options contracts, tick data, and
/// related computations. Provides methods for retrieving, streaming, and managing market data for futures and options,
/// as well as calculating option greeks.
/// </summary>
/// <remarks>This interface is designed to support operations related to futures and options market data,
/// including querying contract details, retrieving price data, streaming tick data, and performing computations such as
/// option greeks. It provides asynchronous methods for data retrieval and synchronous methods for streaming control and
/// computations.</remarks>
public interface IMarketDataApi
{

    void Start(Guid commandId, Action<Guid, int, string>? errorMessageHandler=null);
    void Stop(Guid commandId);

    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        Guid commandId,
        FuturesContractV2ReadModel queryForContract);
    
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        Guid commandId,
        FuturesOptionContractReadModel qfContract);

    Task<FuturesTickDataV2ReadModel?> GetFuturesPriceAsync(
        Guid commandId,
        FuturesContractV2ReadModel contract);

    Task<(FuturesOptionContractReadModel? shortContract, FuturesOptionContractReadModel? longContract)> GetFuturesOptionSpreadAsync(
        Guid commandId,
        FuturesOptionContractReadModel qfShortContract,
        FuturesOptionContractReadModel qfLongContract);

    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionPriceAsync(
        Guid commandId,
        DateOnly valueDate,
        FuturesOptionContractReadModel optionContract);
    
    TickOptionComputation? GetFuturesOptionGreeks(
        Guid commandId,
        DateOnly valueDate, 
        DateOnly maturityDate, 
        FuturesOptionContractReadModel optionContract, 
        double optionPrice, 
        double futuresPrice, 
        double riskFreeRate);

    void StartStreamingFuturesTickData(
        Guid commandId,
        DateOnly valueDate,
        FuturesContractV2ReadModel contract);

    bool StopStreamingFuturesTickData(Guid commandId);

    void StartStreamingFuturesOptionTickData(
        Guid commandId,
        DateOnly valueDate,
        DateTime maturityDate,
        FuturesOptionContractReadModel contract,
        double riskFreeRate);

    bool StopStreamingFuturesOptionTickData(Guid commandId);

}

public interface IMarketDataSnapshotApi : IMarketDataApi
{

}
