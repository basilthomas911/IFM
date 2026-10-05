using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Identifies a provider-neutral historical market-data schema.</summary>
public enum HistoricalDataSchema : byte
{
    /// <summary>No schema was selected.</summary>
    Unknown = 0,
    /// <summary>Instrument definitions and symbol mappings.</summary>
    Definition = 1,
    /// <summary>One-minute open, high, low, close, and volume records.</summary>
    OhlcvOneMinute = 2,
    /// <summary>Individual trade records.</summary>
    Trades = 3,
    /// <summary>Exchange statistics records.</summary>
    Statistics = 4,
    /// <summary>Daily open, high, low, close, and volume records.</summary>
    OhlcvDaily = 5
}

/// <summary>Identifies provider-neutral input symbology.</summary>
public enum HistoricalSymbology : byte
{
    /// <summary>No symbology was selected.</summary>
    Unknown = 0,
    /// <summary>An exact provider raw symbol.</summary>
    RawSymbol = 1,
    /// <summary>A provider continuous futures symbol.</summary>
    Continuous = 2,
    /// <summary>A provider instrument identifier.</summary>
    InstrumentId = 3
}

/// <summary>Identifies the lifecycle state of a provider batch job.</summary>
public enum HistoricalProviderJobState : byte
{
    /// <summary>The state is unknown.</summary>
    Unknown = 0,
    /// <summary>The job is queued.</summary>
    Queued = 1,
    /// <summary>The provider is processing the job.</summary>
    Processing = 2,
    /// <summary>The job completed and its files are available.</summary>
    Completed = 3,
    /// <summary>The job failed.</summary>
    Failed = 4,
    /// <summary>The job expired.</summary>
    Expired = 5
}

/// <summary>Identifies the normalized record shape returned by a historical reader.</summary>
public enum HistoricalProviderRecordKind : byte
{
    /// <summary>The record kind is unknown.</summary>
    Unknown = 0,
    /// <summary>An instrument definition or mapping.</summary>
    Definition = 1,
    /// <summary>A one-minute OHLCV observation.</summary>
    Ohlcv = 2,
    /// <summary>An individual trade.</summary>
    Trade = 3,
    /// <summary>An exchange statistic.</summary>
    Statistic = 4
}

/// <summary>Describes one cost-controlled provider historical request.</summary>
public sealed record HistoricalProviderRequest
{
    /// <summary>Gets the provider dataset.</summary>
    public required string Dataset { get; init; }
    /// <summary>Gets the requested provider symbols.</summary>
    public required string[] Symbols { get; init; }
    /// <summary>Gets the requested schema.</summary>
    public required HistoricalDataSchema Schema { get; init; }
    /// <summary>Gets the input symbology.</summary>
    public required HistoricalSymbology Symbology { get; init; }
    /// <summary>Gets the inclusive UTC range start.</summary>
    public required DateTimeOffset StartUtc { get; init; }
    /// <summary>Gets the exclusive UTC range end.</summary>
    public required DateTimeOffset EndUtc { get; init; }
    /// <summary>Gets the maximum provider records, or zero for the approved complete range.</summary>
    public long RecordLimit { get; init; }
    /// <summary>Gets the stable request hash used for idempotency.</summary>
    public required string RequestHash { get; init; }
}

/// <summary>Reports the non-billable estimate for a historical provider request.</summary>
/// <param name="EstimatedCostUsd">The estimated cost usd.</param>
/// <param name="EstimatedBytes">The estimated bytes.</param>
/// <param name="EstimatedRecords">The estimated records.</param>
/// <param name="EstimatedAtUtc">The estimated at utc.</param>
public sealed record HistoricalProviderEstimate(
    decimal EstimatedCostUsd,
    long EstimatedBytes,
    long EstimatedRecords,
    DateTimeOffset EstimatedAtUtc);

/// <summary>Describes a resumable provider batch job.</summary>
/// <param name="ProviderJobId">The provider job id.</param>
/// <param name="State">The store holding the option-chain session state.</param>
/// <param name="CostUsd">The cost usd.</param>
/// <param name="RecordCount">The record count.</param>
/// <param name="BilledBytes">The billed bytes.</param>
/// <param name="ProgressPercent">The progress percent.</param>
/// <param name="ErrorMessage">The error message.</param>
public sealed record HistoricalProviderJob(
    string ProviderJobId,
    HistoricalProviderJobState State,
    decimal CostUsd,
    long RecordCount,
    long BilledBytes,
    byte ProgressPercent,
    string ErrorMessage);

/// <summary>Describes one immutable file produced by a provider batch job.</summary>
/// <param name="ProviderFileId">The provider file id.</param>
/// <param name="FileName">The file name.</param>
/// <param name="Schema">The provider schema or recovery baseline required for the operation.</param>
/// <param name="SizeBytes">The size bytes.</param>
/// <param name="Sha256">The sha256.</param>
public sealed record HistoricalProviderFile(
    string ProviderFileId,
    string FileName,
    HistoricalDataSchema Schema,
    long SizeBytes,
    string Sha256);

/// <summary>Provides one provider-neutral decoded historical record.</summary>
public sealed record HistoricalProviderRecord
{
    /// <summary>Gets the normalized record kind.</summary>
    public required HistoricalProviderRecordKind Kind { get; init; }
    /// <summary>Gets the provider symbol.</summary>
    public required string Symbol { get; init; }
    /// <summary>Gets the provider instrument identity.</summary>
    public required string InstrumentId { get; init; }
    /// <summary>Gets the provider publisher identity.</summary>
    public string PublisherId { get; init; } = string.Empty;
    /// <summary>Gets the exchange event time in UTC.</summary>
    public required DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the source sequence when supplied.</summary>
    public long SourceSequence { get; init; }
    /// <summary>Gets the open price for OHLCV records.</summary>
    public decimal Open { get; init; }
    /// <summary>Gets the high price for OHLCV records.</summary>
    public decimal High { get; init; }
    /// <summary>Gets the low price for OHLCV records.</summary>
    public decimal Low { get; init; }
    /// <summary>Gets the close or individual trade price.</summary>
    public decimal CloseOrPrice { get; init; }
    /// <summary>Gets OHLCV volume or an individual trade size.</summary>
    public long VolumeOrSize { get; init; }
    /// <summary>Gets the normalized provider-neutral action.</summary>
    public NormalizedTradeAction Action { get; init; }
    /// <summary>Gets the normalized provider-neutral side.</summary>
    public NormalizedTradeSide Side { get; init; }
    /// <summary>Gets normalized provider-neutral conditions.</summary>
    public NormalizedTradeConditionFlags Conditions { get; init; }
}

/// <summary>Provides one bounded decoded historical batch.</summary>
/// <param name="Records">The records.</param>
/// <param name="BatchOrdinal">The batch ordinal.</param>
/// <param name="SourcePosition">The source position.</param>
/// <param name="IsFinal">The is final.</param>
public sealed record HistoricalProviderRecordBatch(
    IReadOnlyList<HistoricalProviderRecord> Records,
    long BatchOrdinal,
    string SourcePosition,
    bool IsFinal);

/// <summary>Streams bounded decoded records from a provider range or verified file.</summary>
public interface IHistoricalRecordReader : IAsyncDisposable
{
    /// <summary>Reads the next bounded batch, or <see langword="null"/> at end of input.</summary>
    ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken);
}

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
