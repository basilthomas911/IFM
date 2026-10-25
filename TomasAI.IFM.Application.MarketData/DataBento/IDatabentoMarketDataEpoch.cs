using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// One fully owned, date-scoped DataBento runtime. The application API owns
/// its lifecycle and never reuses it for another value date.
/// </summary>
public interface IDatabentoMarketDataEpoch : IAsyncDisposable
{
    DateOnly ValueDate { get; }
    IDatabentoMarketDataCatalog Catalog { get; }
    IDatabentoLastPriceReaderProvider LastPrices { get; }
    bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId)
    {
        marketInstrumentId = 0;
        return false;
    }
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

    /// <summary>Returns whether every native feed and managed aggregation worker is running.</summary>
    bool IsFeedUp(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            return false;
        var health = GetHealth();
        return health.Running
            && health.AggregationRunning
            && health.ConfiguredContracts > 0
            && health.LastPriceStoreActive;
    }

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<DatabentoDatasetResetResult>(
            new NotSupportedException("Per-dataset reset is not supported by this epoch."));
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
    Task<Pricing.WorkerOptionChainResult> AcquireOptionChainAsync(Pricing.WorkerOptionChainRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Qualified worker chains are unavailable.");
    Task<Pricing.WorkerOptionChainResult> ReleaseOptionChainAsync(Pricing.WorkerOptionChainRelease request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Qualified worker chains are unavailable.");
    Task<Pricing.CompositionSnapshotResult> CaptureCompositionSnapshotAsync(Pricing.CompositionSnapshotRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Worker composition snapshots are unavailable.");
}
