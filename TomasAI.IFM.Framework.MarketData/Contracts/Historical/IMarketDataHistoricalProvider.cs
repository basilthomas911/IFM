using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Defines the provider-neutral historical acquisition boundary.</summary>
public interface IMarketDataHistoricalProvider
{
    /// <summary>Estimates cost, bytes, and records without submitting billable work.</summary>
    ValueTask<HistoricalProviderEstimate> EstimateAsync(
        HistoricalProviderRequest request,
        CancellationToken cancellationToken);

    /// <summary>Submits one batch request after application budget approval.</summary>
    ValueTask<HistoricalProviderJob> SubmitBatchAsync(
        HistoricalProviderRequest request,
        CancellationToken cancellationToken);

    /// <summary>Gets the latest state of an existing provider job.</summary>
    ValueTask<HistoricalProviderJob> GetBatchJobAsync(
        string providerJobId,
        CancellationToken cancellationToken);

    /// <summary>Lists immutable files for a completed provider job.</summary>
    ValueTask<IReadOnlyList<HistoricalProviderFile>> ListBatchFilesAsync(
        string providerJobId,
        CancellationToken cancellationToken);

    /// <summary>Downloads one provider file to an exact validated staging path.</summary>
    ValueTask DownloadBatchFileAsync(
        string providerJobId,
        HistoricalProviderFile file,
        string destinationPath,
        CancellationToken cancellationToken);

    /// <summary>Opens a bounded direct range reader for repair workflows.</summary>
    ValueTask<IHistoricalRecordReader> OpenRangeAsync(
        HistoricalProviderRequest request,
        int maximumBatchRecords,
        CancellationToken cancellationToken);

    /// <summary>Opens a bounded reader over one downloaded and verified file.</summary>
    ValueTask<IHistoricalRecordReader> OpenFileAsync(
        string path,
        HistoricalDataSchema schema,
        int maximumBatchRecords,
        CancellationToken cancellationToken);
}
