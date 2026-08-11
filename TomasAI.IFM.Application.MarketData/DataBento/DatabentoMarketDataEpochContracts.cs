using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoMarketDataEpochFactory
{
    IDatabentoMarketDataEpoch Create(DateOnly valueDate);
}

/// <summary>
/// One fully owned, date-scoped DataBento runtime. The application API owns
/// its lifecycle and never reuses it for another value date.
/// </summary>
public interface IDatabentoMarketDataEpoch : IAsyncDisposable
{
    DateOnly ValueDate { get; }
    IDatabentoMarketDataCatalog Catalog { get; }
    IDatabentoLastPriceReaderProvider LastPrices { get; }

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    DatabentoMarketDataEpochHealth GetHealth();
    TickAggregationContractStatus GetAggregationStatus(string contractId);
    bool StartFuturesRoute(string futuresContractId);
    bool StopFuturesRoute(string futuresContractId);
    bool StartIndividualOptionRoute(string futuresOptionContractId);
    bool StopIndividualOptionRoute(string futuresOptionContractId);
    Task<bool> StartOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds);
    Task<bool> StopOptionChainAsync(string futuresContractId, DateOnly maturityDate);
}

public readonly record struct DatabentoMarketDataEpochHealth(
    DateOnly ValueDate,
    bool Running,
    bool AggregationRunning,
    int ConfiguredContracts,
    int LastPriceSlots,
    bool LastPriceStoreActive,
    long SourceQuoteRecords,
    long SourceTradeRecords,
    long PublicationFailures);

public readonly record struct DatabentoMarketDataApiHealth(
    bool Running,
    DateOnly? ValueDate,
    DatabentoMarketDataEpochHealth? Epoch);

public interface IDatabentoMarketDataCatalog
{
    FuturesContractV2ReadModel? FindFutures(string contractId);
    FuturesOptionContractReadModel? FindFuturesOption(string contractId);
    string? FindOptionUnderlying(string futuresOptionContractId);
    Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate);
}

public sealed record DatabentoMarketDataApiOptions
{
    public TimeSpan MaximumLastPriceAge { get; init; } = TimeSpan.FromSeconds(10);
}
