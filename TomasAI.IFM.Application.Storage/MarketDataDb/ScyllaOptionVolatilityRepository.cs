using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Monthly-partitioned, append-only Stage 4 evidence repository.</summary>
public sealed class ScyllaOptionVolatilityRepository(IObjectRepository db) : IOptionVolatilityRepository
{
    const int MaximumPayloadBytes = 1_048_576;

    public async Task AppendObservationAsync(string environment, OptionIvObservation observation,
        CancellationToken cancellationToken = default)
    {
        ValidateEnvironment(environment);
        ValidateObservation(observation);
        var existing = await ReadObservationAsync(environment, observation.ObservationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null && !Serialize(existing).AsSpan().SequenceEqual(Serialize(observation)))
            throw new InvalidOperationException("Observation identity conflicts with different immutable evidence.");
        if (observation.Revision > 1)
        {
            var superseded = await ReadObservationAsync(environment, observation.SupersedesObservationId!,
                cancellationToken).ConfigureAwait(false);
            if (superseded is null || superseded.Series != observation.Series ||
                superseded.ExchangeValueDate != observation.ExchangeValueDate ||
                superseded.SamplingSlot != observation.SamplingSlot ||
                superseded.Revision >= observation.Revision)
                throw new InvalidOperationException("An observation revision must supersede existing immutable evidence.");
        }
        var payload = Serialize(observation);
        var bucket = VolatilityCalendarBucket.From(observation.ExchangeValueDate).Value;
        await db.Use("OptionVolatility.Observation.Id.Insert", OptionVolatilityCql.InsertObservationById)
            .SetParameters(new Values([environment, observation.ObservationId, payload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        var stored = await ReadObservationAsync(environment, observation.ObservationId, cancellationToken).ConfigureAwait(false);
        if (stored is null || !Serialize(stored).AsSpan().SequenceEqual(payload))
            throw new InvalidOperationException("Observation identity conflicts with different immutable evidence.");
        await db.Use("OptionVolatility.Observation.History.Insert", OptionVolatilityCql.InsertObservationHistory)
            .SetParameters(new Values([environment, observation.Series.SeriesId,
                observation.Series.MethodologyVersion, bucket, observation.ExchangeValueDate,
                observation.SamplingSlot, observation.Revision, observation.ObservationId,
                observation.AvailableAtUtc.UtcDateTime, payload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishAsync(OptionIvPublication publication,
        CancellationToken cancellationToken = default)
    {
        ValidatePublication(publication);
        foreach (var observation in publication.SourceObservations)
            await AppendObservationAsync(publication.Environment, observation, cancellationToken).ConfigureAwait(false);

        var metric = publication.Metric;
        var snapshot = metric.Snapshot;
        if (metric.Revision > 1)
        {
            var superseded = await GetSnapshotAsync(publication.Environment,
                metric.SupersedesSnapshotId!, cancellationToken).ConfigureAwait(false);
            if (superseded is null || superseded.Snapshot.Series != snapshot.Series ||
                superseded.Snapshot.MetricPolicyVersion != snapshot.MetricPolicyVersion)
                throw new InvalidOperationException("A metric revision must supersede existing evidence in the same series policy.");
        }
        var metricPayload = Serialize(metric);
        await db.Use("OptionVolatility.Snapshot.Insert", OptionVolatilityCql.InsertSnapshot)
            .SetParameters(new Values([publication.Environment, snapshot.SnapshotId,
                snapshot.SnapshotDigest, metric.PublicationSequence, metricPayload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        var sealedMetric = await GetSnapshotAsync(publication.Environment, snapshot.SnapshotId,
            cancellationToken).ConfigureAwait(false);
        if (sealedMetric is null || !Serialize(sealedMetric).AsSpan().SequenceEqual(metricPayload))
            throw new InvalidOperationException("Snapshot identity conflicts with different immutable evidence.");

        await db.Use("OptionVolatility.Metric.History.Insert", OptionVolatilityCql.InsertMetricHistory)
            .SetParameters(new Values([publication.Environment, snapshot.Series.SeriesId,
                snapshot.Series.MethodologyVersion, snapshot.MetricPolicyVersion,
                VolatilityCalendarBucket.From(snapshot.ExchangeValueDate).Value, snapshot.ExchangeValueDate,
                snapshot.SamplingSlot, metric.Revision, snapshot.SnapshotId,
                snapshot.AvailableAtUtc.UtcDateTime, metric.PublicationSequence, metricPayload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        var pointer = Pointer(publication);
        var parameters = LatestValues(pointer);
        await db.Use("OptionVolatility.Latest.Insert", OptionVolatilityCql.InsertLatest)
            .SetParameters(parameters).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await db.Use("OptionVolatility.Latest.Advance", OptionVolatilityCql.AdvanceLatest)
            .SetParameters(AdvanceLatestValues(pointer)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        var current = await ReadLatestAsync(pointer.Scope, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Latest pointer was not durably advertised.");
        if (current.PublicationSequence == pointer.PublicationSequence && current != pointer)
            throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
        if (current.PublicationSequence < pointer.PublicationSequence)
            throw new InvalidOperationException("Latest pointer monotonic advance failed.");
    }

    public async Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnvironment(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        return await db.Use("OptionVolatility.Snapshot.Read", OptionVolatilityCql.SelectSnapshot)
            .SetParameters(new Values([environment, snapshotId]))
            .ExecuteSingleAsync(row =>
            {
                var value = Deserialize<OptionIvMetricRevision>(row.GetBytes(2));
                if (value.Snapshot.SnapshotId != snapshotId ||
                    value.Snapshot.SnapshotDigest != row.GetString(0) ||
                    value.PublicationSequence != row.GetLong(1))
                    throw new InvalidDataException("Stored snapshot identity is invalid.");
                return value;
            }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateLatest(request);
        var pointer = await ReadLatestAsync(request.Scope, cancellationToken).ConfigureAwait(false);
        if (pointer is null) return new(null, VolatilityFreshnessStatus.Unavailable);
        var metric = await GetSnapshotAsync(request.Scope.Environment, pointer.SnapshotId,
            cancellationToken).ConfigureAwait(false);
        if (metric is null || metric.Snapshot.SnapshotDigest != pointer.SnapshotDigest)
            throw new InvalidDataException("Latest pointer references a missing or conflicting sealed snapshot.");
        return new(metric, request.RequestedAtUtc - pointer.AvailableAtUtc <= request.MaximumAge
            ? VolatilityFreshnessStatus.Accepted : VolatilityFreshnessStatus.Stale);
    }

    public async Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePage(request);
        var page = await db.Use("OptionVolatility.Observation.History", OptionVolatilityCql.SelectObservationHistory)
            .SetParameters(HistoryValues(request, includePolicy: false))
            .ExecutePageAsync(row => Deserialize<OptionIvObservation>(row.GetBytes(0)), request.PageSize,
                request.PagingState, cancellationToken).ConfigureAwait(false);
        var candidates = page.Items.AsEnumerable();
        if (request.SamplingSlot is not null) candidates = candidates.Where(x => x.SamplingSlot == request.SamplingSlot);
        if (request.Mode == VolatilityHistoricalMode.AsKnown)
            candidates = candidates.Where(x => x.AvailableAtUtc <= request.KnownAtUtc!.Value);
        var items = candidates.GroupBy(x => (x.ExchangeValueDate, x.SamplingSlot))
            .Select(x => x.OrderByDescending(y => y.Revision).First()).ToImmutableArray();
        return new(items, page.PagingState);
    }

    public async Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePage(request);
        var page = await db.Use("OptionVolatility.Metric.History", OptionVolatilityCql.SelectMetricHistory)
            .SetParameters(HistoryValues(request, includePolicy: true))
            .ExecutePageAsync(row => Deserialize<OptionIvMetricRevision>(row.GetBytes(0)), request.PageSize,
                request.PagingState, cancellationToken).ConfigureAwait(false);
        var candidates = page.Items.AsEnumerable();
        if (request.SamplingSlot is not null) candidates = candidates.Where(x => x.Snapshot.SamplingSlot == request.SamplingSlot);
        if (request.Mode == VolatilityHistoricalMode.AsKnown)
            candidates = candidates.Where(x => x.Snapshot.AvailableAtUtc <= request.KnownAtUtc!.Value);
        var items = candidates.GroupBy(x => (x.Snapshot.ExchangeValueDate, x.Snapshot.SamplingSlot))
            .Select(x => x.OrderByDescending(y => y.Revision).ThenByDescending(y => y.PublicationSequence).First())
            .ToImmutableArray();
        return new(items, page.PagingState);
    }

    public async Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(
        IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var result = ImmutableDictionary.CreateBuilder<VolatilityStorageScope, OptionIvLatestPointer>();
        foreach (var scope in scopes.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pointer = await ReadLatestAsync(scope, cancellationToken).ConfigureAwait(false);
            if (pointer is not null) result[scope] = pointer;
        }
        return result.ToImmutable();
    }

    async Task<OptionIvObservation?> ReadObservationAsync(string environment, string id, CancellationToken token) =>
        await db.Use("OptionVolatility.Observation.Id.Read", OptionVolatilityCql.SelectObservationById)
            .SetParameters(new Values([environment, id]))
            .ExecuteSingleAsync(row => Deserialize<OptionIvObservation>(row.GetBytes(0)), token).ConfigureAwait(false);

    async Task<OptionIvLatestPointer?> ReadLatestAsync(VolatilityStorageScope scope, CancellationToken token) =>
        await db.Use("OptionVolatility.Latest.Read", OptionVolatilityCql.SelectLatest)
            .SetParameters(new Values([scope.Environment, scope.Series.SeriesId,
                scope.Series.MethodologyVersion, scope.MetricPolicyVersion]))
            .ExecuteSingleAsync(row => new OptionIvLatestPointer(scope, row.GetString(0), row.GetString(1),
                row.GetLong(2), new DateTimeOffset(DateTime.SpecifyKind(row.GetDateTime(3), DateTimeKind.Utc))), token)
            .ConfigureAwait(false);

    static Values LatestValues(OptionIvLatestPointer pointer) => new([pointer.Scope.Environment,
        pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion,
        pointer.Scope.MetricPolicyVersion, pointer.SnapshotId, pointer.SnapshotDigest,
        pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime]);

    static Values AdvanceLatestValues(OptionIvLatestPointer pointer) => new([pointer.SnapshotId,
        pointer.SnapshotDigest, pointer.PublicationSequence, pointer.AvailableAtUtc.UtcDateTime,
        pointer.Scope.Environment, pointer.Scope.Series.SeriesId, pointer.Scope.Series.MethodologyVersion,
        pointer.Scope.MetricPolicyVersion]);

    static Values HistoryValues(VolatilityHistoryPageRequest request, bool includePolicy) =>
        includePolicy
            ? new([request.Scope.Environment, request.Scope.Series.SeriesId,
                request.Scope.Series.MethodologyVersion, request.Scope.MetricPolicyVersion,
                request.Bucket.Value, request.FromValueDate, request.ToValueDate])
            : new([request.Scope.Environment, request.Scope.Series.SeriesId,
                request.Scope.Series.MethodologyVersion, request.Bucket.Value,
                request.FromValueDate, request.ToValueDate]);

    static OptionIvLatestPointer Pointer(OptionIvPublication publication)
    {
        var snapshot = publication.Metric.Snapshot;
        return new(new(publication.Environment, snapshot.Series, snapshot.MetricPolicyVersion),
            snapshot.SnapshotId, snapshot.SnapshotDigest, publication.Metric.PublicationSequence,
            snapshot.AvailableAtUtc);
    }

    static byte[] Serialize<T>(T value)
    {
        var payload = MessagePackBinarySerializer.Shared.Serialize(value)
            ?? throw new InvalidDataException("Option volatility serialization returned no payload.");
        if (payload.Length is 0 or > MaximumPayloadBytes)
            throw new ArgumentException("Option volatility payload exceeds its bounded size.");
        return payload;
    }
    static T Deserialize<T>(byte[] payload)
    {
        if (payload.Length is 0 or > MaximumPayloadBytes) throw new InvalidDataException("Invalid option volatility payload size.");
        return MessagePackBinarySerializer.Shared.Deserialize<T>(payload)
            ?? throw new InvalidDataException("Missing option volatility payload.");
    }

    static void ValidatePublication(OptionIvPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ValidateEnvironment(publication.Environment);
        if (publication.Metric.Revision < 1 || publication.Metric.PublicationSequence < 1 ||
            (publication.Metric.Revision == 1) != (publication.Metric.SupersedesSnapshotId is null) ||
            publication.SourceObservations.IsDefaultOrEmpty ||
            !publication.Metric.Snapshot.SourceObservationIds.Order(StringComparer.Ordinal).SequenceEqual(
                publication.SourceObservations.Select(x => x.ObservationId).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException("A complete immutable publication is required.", nameof(publication));
    }
    static void ValidateObservation(OptionIvObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.SchemaVersion != OptionIvObservation.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(observation.ObservationId) || observation.ExchangeValueDate == default ||
            observation.Revision < 1 || (observation.Revision == 1) != (observation.SupersedesObservationId is null) ||
            observation.RecordedAtUtc.Offset != TimeSpan.Zero || observation.AvailableAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid option-IV observation.", nameof(observation));
    }
    static void ValidatePage(VolatilityHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvironment(request.Scope.Environment);
        if (request.PageSize is < 1 or > 500 || request.FromValueDate > request.ToValueDate ||
            VolatilityCalendarBucket.From(request.FromValueDate) != request.Bucket ||
            VolatilityCalendarBucket.From(request.ToValueDate) != request.Bucket ||
            request.Mode == VolatilityHistoricalMode.AsKnown && request.KnownAtUtc is null ||
            request.Mode == VolatilityHistoricalMode.Restated && request.KnownAtUtc is not null)
            throw new ArgumentException("History query must be bounded to one calendar bucket.", nameof(request));
    }
    static void ValidateLatest(LatestVolatilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvironment(request.Scope.Environment);
        if (request.MaximumAge <= TimeSpan.Zero || request.RequestedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("A positive configured freshness requirement is required.", nameof(request));
    }
    static void ValidateEnvironment(string environment) => ArgumentException.ThrowIfNullOrWhiteSpace(environment);
    readonly record struct Values(object?[] Items) : IBindValue { public object Bind() => Items; }
}
