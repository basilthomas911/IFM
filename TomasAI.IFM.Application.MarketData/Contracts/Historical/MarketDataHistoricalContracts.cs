using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies the stage durably completed by a historical acquisition.</summary>
public enum HistoricalAcquisitionStage : byte
{
    /// <summary>No stage has completed.</summary>
    None = 0,
    /// <summary>The estimate was approved.</summary>
    Estimated = 1,
    /// <summary>The provider job was submitted or resumed.</summary>
    Submitted = 2,
    /// <summary>All provider files were downloaded and verified.</summary>
    Downloaded = 3,
    /// <summary>All records were normalized and accepted by the sink.</summary>
    Normalized = 4,
    /// <summary>The manifest was finalized.</summary>
    Completed = 5
}

/// <summary>Describes one domain series acquisition profile.</summary>
public sealed record MarketDataHistoricalSeriesRequest
{
    /// <summary>Gets the provider-neutral series identity.</summary>
    public required MarketSeriesIdentity SeriesIdentity { get; init; }
    /// <summary>Gets the actual contract when an exact contract is requested.</summary>
    public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the provider-neutral schema.</summary>
    public required HistoricalDataSchema Schema { get; init; }
    /// <summary>Gets whether exact individual trades are required.</summary>
    public bool ExactTradesRequired { get; init; }
}

/// <summary>Describes one cost-controlled application historical acquisition.</summary>
public sealed record MarketDataHistoricalRequest
{
    /// <summary>Gets the stable bootstrap attempt identity.</summary>
    public required Guid BootstrapAttemptId { get; init; }
    /// <summary>Gets the requested series profiles.</summary>
    public required MarketDataHistoricalSeriesRequest[] Series { get; init; }
    /// <summary>Gets the inclusive first futures value date.</summary>
    public required DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive last futures value date.</summary>
    public required DateOnly EndDate { get; init; }
    /// <summary>Gets the maximum approved provider cost in US dollars.</summary>
    public required decimal MaximumCostUsd { get; init; }
    /// <summary>Gets the maximum approved provider bytes.</summary>
    public required long MaximumBytes { get; init; }
    /// <summary>Gets an approved override identity when normal limits may be exceeded.</summary>
    public string ApprovedOverrideId { get; init; } = string.Empty;
    /// <summary>Gets the normalization implementation version.</summary>
    public required string NormalizationVersion { get; init; }
    /// <summary>Gets the requesting principal.</summary>
    public required string RequestedBy { get; init; }
}

/// <summary>Reports the combined non-billable estimate for an application request.</summary>
public sealed record MarketDataHistoricalEstimate(
    Guid BootstrapAttemptId,
    decimal EstimatedCostUsd,
    long EstimatedBytes,
    long EstimatedRecords,
    string RequestSha256,
    DateTimeOffset EstimatedAtUtc);

/// <summary>Provides the durable resume position for acquisition redelivery.</summary>
public sealed record HistoricalAcquisitionCheckpoint
{
    /// <summary>Gets the bootstrap attempt identity.</summary>
    public required Guid BootstrapAttemptId { get; init; }
    /// <summary>Gets the last durably completed stage.</summary>
    public HistoricalAcquisitionStage Stage { get; init; }
    /// <summary>Gets the existing provider job identity, when submitted.</summary>
    public string ProviderJobId { get; init; } = string.Empty;
    /// <summary>Gets the last accepted file identity.</summary>
    public string ProviderFileId { get; init; } = string.Empty;
    /// <summary>Gets the last accepted bounded batch ordinal.</summary>
    public long BatchOrdinal { get; init; } = -1;
    /// <summary>Gets the provider reader resume position.</summary>
    public string SourcePosition { get; init; } = string.Empty;
}

/// <summary>Provides one normalized historical trade for exact replay.</summary>
public sealed record NormalizedHistoricalTrade
{
    /// <summary>Gets the actual domain contract identity.</summary>
    public required string ContractId { get; init; }
    /// <summary>Gets the futures value date.</summary>
    public required DateOnly ValueDate { get; init; }
    /// <summary>Gets the normalized price.</summary>
    public required decimal Price { get; init; }
    /// <summary>Gets the individual executed size.</summary>
    public required long Size { get; init; }
    /// <summary>Gets the exchange event time.</summary>
    public required DateTimeOffset EventTimestampUtc { get; init; }
    /// <summary>Gets the source sequence.</summary>
    public long SourceSequence { get; init; }
    /// <summary>Gets the normalized action.</summary>
    public NormalizedTradeAction Action { get; init; }
    /// <summary>Gets the normalized aggressor side.</summary>
    public NormalizedTradeSide Side { get; init; }
    /// <summary>Gets provider-neutral conditions.</summary>
    public NormalizedTradeConditionFlags Conditions { get; init; }
    /// <summary>Gets the provider instrument identity.</summary>
    public required string ProviderInstrumentId { get; init; }
}

/// <summary>Provides one bounded normalized sink batch.</summary>
public sealed record NormalizedHistoricalBatch(
    Guid BootstrapAttemptId,
    string ProviderFileId,
    long BatchOrdinal,
    string SourcePosition,
    IReadOnlyList<FuturesAnalyticsObservationReadModel> Observations,
    IReadOnlyList<NormalizedHistoricalTrade> Trades,
    string NormalizedSha256,
    bool IsFinal);

/// <summary>Reports the immutable acquisition result without embedding provider records.</summary>
public sealed record MarketDataHistoricalManifest
{
    /// <summary>Gets the manifest identity.</summary>
    public required Guid ManifestId { get; init; }
    /// <summary>Gets the bootstrap attempt identity.</summary>
    public required Guid BootstrapAttemptId { get; init; }
    /// <summary>Gets the provider job identity.</summary>
    public required string ProviderJobId { get; init; }
    /// <summary>Gets the request hash.</summary>
    public required string RequestSha256 { get; init; }
    /// <summary>Gets the normalized manifest hash.</summary>
    public required string NormalizedSha256 { get; init; }
    /// <summary>Gets the normalized observation count.</summary>
    public long ObservationCount { get; init; }
    /// <summary>Gets the normalized exact-trade count.</summary>
    public long TradeCount { get; init; }
    /// <summary>Gets the first covered value date.</summary>
    public DateOnly FirstValueDate { get; init; }
    /// <summary>Gets the last covered value date.</summary>
    public DateOnly LastValueDate { get; init; }
    /// <summary>Gets the UTC completion time.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }
}

/// <summary>Accepts bounded normalized batches and returns only after durable checkpointing.</summary>
public interface IHistoricalObservationSink
{
    /// <summary>Persists one bounded batch idempotently.</summary>
    ValueTask AcceptAsync(NormalizedHistoricalBatch batch, CancellationToken cancellationToken);
}

/// <summary>Accepts acquisition-stage checkpoints before normalized record batches are available.</summary>
public interface IHistoricalAcquisitionCheckpointSink
{
    /// <summary>Persists one monotonic provider acquisition checkpoint.</summary>
    ValueTask CheckpointAsync(
        HistoricalAcquisitionCheckpoint checkpoint,
        CancellationToken cancellationToken);
}

/// <summary>Defines the provider-neutral application historical API used by domain bootstrap actors.</summary>
public interface IMarketDataHistoricalApi
{
    /// <summary>Estimates a request without submitting billable provider work.</summary>
    ValueTask<MarketDataHistoricalEstimate> EstimateAsync(
        MarketDataHistoricalRequest request,
        CancellationToken cancellationToken);

    /// <summary>Acquires or resumes a request and streams bounded normalized batches to a durable sink.</summary>
    ValueTask<MarketDataHistoricalManifest> AcquireAsync(
        MarketDataHistoricalRequest request,
        HistoricalAcquisitionCheckpoint checkpoint,
        IHistoricalObservationSink sink,
        CancellationToken cancellationToken);
}

/// <summary>Resolves domain series requests to exact provider request profiles.</summary>
public interface IHistoricalSeriesRequestResolver
{
    /// <summary>Resolves one domain series to an exact provider request.</summary>
    HistoricalProviderRequest Resolve(
        MarketDataHistoricalRequest request,
        MarketDataHistoricalSeriesRequest series);

    /// <summary>Resolves an actual domain contract from a provider record.</summary>
    string ResolveContractId(MarketDataHistoricalSeriesRequest series, HistoricalProviderRecord record);
}

/// <summary>Resolves futures value dates and exact session boundaries without fixed UTC offsets.</summary>
public interface IMarketSessionCalendar
{
    /// <summary>Gets the value date containing a UTC exchange timestamp.</summary>
    DateOnly GetValueDate(DateTimeOffset exchangeTimestampUtc);

    /// <summary>Gets the exact UTC session boundaries for a futures value date.</summary>
    MarketSessionBounds GetSession(DateOnly valueDate);

    /// <summary>Gets whether the supplied value date is a trading session.</summary>
    bool IsTradingDate(DateOnly valueDate);
}

/// <summary>Provides the exact UTC boundaries of one futures session.</summary>
public readonly record struct MarketSessionBounds(
    DateOnly ValueDate,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc);
