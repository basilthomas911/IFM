using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

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
    bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot);
    bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot);
    bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }
    bool IsTickDataStreamActive(string contractId);

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    DatabentoMarketDataEpochHealth GetHealth();
    TickAggregationContractStatus GetAggregationStatus(string contractId);
    bool StartFuturesRoute(TickerStreamOwner owner, string futuresContractId);
    bool StopFuturesRoute(TickerStreamOwner owner, string futuresContractId);
    bool StartIndividualOptionRoute(TickerStreamOwner owner, string futuresOptionContractId);
    bool StopIndividualOptionRoute(TickerStreamOwner owner, string futuresOptionContractId);
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
    long PublicationFailures,
    long ProcessingFailures = 0,
    IReadOnlyList<TickAggregationContractStatus>? ContractStatuses = null);

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
