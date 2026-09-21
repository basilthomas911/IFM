using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilityHistoricalMode
{
    AsKnown = 1,
    Restated = 2
}

public sealed record VolatilityStorageScope(
    string Environment,
    VolatilitySeriesIdentity Series,
    string MetricPolicyVersion);

public sealed record VolatilityCalendarBucket(int Year, int Month)
{
    public static VolatilityCalendarBucket From(DateOnly date) => new(date.Year, date.Month);
    public int Value => checked(Year * 100 + Month);
}

public sealed record VolatilityHistoryPageRequest(
    VolatilityStorageScope Scope,
    VolatilityCalendarBucket Bucket,
    DateOnly FromValueDate,
    DateOnly ToValueDate,
    string? SamplingSlot,
    VolatilityHistoricalMode Mode,
    DateTimeOffset? KnownAtUtc,
    int PageSize,
    byte[]? PagingState);

public sealed record VolatilityPage<T>(ImmutableArray<T> Items, byte[]? PagingState);

public sealed record OptionIvMetricRevision(
    OptionIvMetricSnapshot Snapshot,
    int Revision,
    string? SupersedesSnapshotId,
    long PublicationSequence);

public sealed record OptionIvPublication(
    string Environment,
    OptionIvMetricRevision Metric,
    ImmutableArray<OptionIvObservation> SourceObservations);

public sealed record OptionIvLatestPointer(
    VolatilityStorageScope Scope,
    string SnapshotId,
    string SnapshotDigest,
    long PublicationSequence,
    DateTimeOffset AvailableAtUtc);

public sealed record LatestVolatilityRequest(
    VolatilityStorageScope Scope,
    DateTimeOffset RequestedAtUtc,
    TimeSpan MaximumAge);

public sealed record LatestVolatilityResult(
    OptionIvMetricRevision? Metric,
    VolatilityFreshnessStatus FreshnessStatus);

public interface IOptionVolatilityRepository
{
    Task AppendObservationAsync(string environment, OptionIvObservation observation,
        CancellationToken cancellationToken = default);
    Task PublishAsync(OptionIvPublication publication,
        CancellationToken cancellationToken = default);
    Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default);
    Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(
        IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default);
}

public interface IOptionVolatilityQueryApi
{
    Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default);
    Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
}

public interface IOptionVolatilityPublicationService
{
    Task PublishAsync(OptionIvPublication publication,
        CancellationToken cancellationToken = default);
}
