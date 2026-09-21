using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

public enum VolatilityPublicationStage
{
    BeforeSources = 1,
    BeforeSnapshotSeal = 2,
    BeforeMetricHistory = 3,
    BeforeLatestAdvertisement = 4
}

/// <summary>Deterministic repository used for closed-market and publication-failure qualification.</summary>
public sealed class InMemoryOptionVolatilityRepository(Action<VolatilityPublicationStage>? beforeStage = null)
    : IOptionVolatilityRepository
{
    readonly object gate = new();
    readonly Dictionary<(string Environment, string Id), OptionIvObservation> observations = new();
    readonly Dictionary<(string Environment, string Id), OptionIvMetricRevision> snapshots = new();
    readonly Dictionary<(string Environment, string Id), OptionIvMetricRevision> metrics = new();
    readonly Dictionary<VolatilityStorageScope, OptionIvLatestPointer> durableLatest = new();
    readonly Dictionary<VolatilityStorageScope, OptionIvLatestPointer> latestCache = new();

    public Task AppendObservationAsync(string environment, OptionIvObservation observation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateEnvironment(environment);
        ValidateObservation(observation);
        lock (gate) AppendObservationCore(environment, observation);
        return Task.CompletedTask;
    }

    public Task PublishAsync(OptionIvPublication publication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePublication(publication);
        lock (gate)
        {
            beforeStage?.Invoke(VolatilityPublicationStage.BeforeSources);
            foreach (var source in publication.SourceObservations)
                AppendObservationCore(publication.Environment, source);

            beforeStage?.Invoke(VolatilityPublicationStage.BeforeSnapshotSeal);
            if (publication.Metric.Revision > 1 &&
                (!snapshots.TryGetValue((publication.Environment, publication.Metric.SupersedesSnapshotId!), out var superseded) ||
                 superseded.Snapshot.Series != publication.Metric.Snapshot.Series ||
                 superseded.Snapshot.MetricPolicyVersion != publication.Metric.Snapshot.MetricPolicyVersion))
                throw new InvalidOperationException("A metric revision must supersede existing evidence in the same series policy.");
            AppendImmutable(snapshots, (publication.Environment, publication.Metric.Snapshot.SnapshotId),
                publication.Metric, "snapshot");

            beforeStage?.Invoke(VolatilityPublicationStage.BeforeMetricHistory);
            AppendImmutable(metrics, (publication.Environment, publication.Metric.Snapshot.SnapshotId),
                publication.Metric, "metric revision");

            beforeStage?.Invoke(VolatilityPublicationStage.BeforeLatestAdvertisement);
            var scope = Scope(publication);
            var pointer = new OptionIvLatestPointer(scope, publication.Metric.Snapshot.SnapshotId,
                publication.Metric.Snapshot.SnapshotDigest, publication.Metric.PublicationSequence,
                publication.Metric.Snapshot.AvailableAtUtc);
            if (!durableLatest.TryGetValue(scope, out var current) ||
                pointer.PublicationSequence > current.PublicationSequence)
            {
                durableLatest[scope] = pointer;
                latestCache[scope] = pointer;
            }
            else if (pointer.PublicationSequence == current.PublicationSequence && pointer != current)
                throw new InvalidOperationException("Publication sequence conflicts with a different latest pointer.");
        }
        return Task.CompletedTask;
    }

    public Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateEnvironment(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        lock (gate) return Task.FromResult(snapshots.GetValueOrDefault((environment, snapshotId)));
    }

    public Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLatestRequest(request);
        lock (gate)
        {
            if (!latestCache.TryGetValue(request.Scope, out var pointer))
                return Task.FromResult(new LatestVolatilityResult(null, VolatilityFreshnessStatus.Unavailable));
            var metric = snapshots[(request.Scope.Environment, pointer.SnapshotId)];
            var status = request.RequestedAtUtc - pointer.AvailableAtUtc <= request.MaximumAge
                ? VolatilityFreshnessStatus.Accepted
                : VolatilityFreshnessStatus.Stale;
            return Task.FromResult(new LatestVolatilityResult(metric, status));
        }
    }

    public Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePage(request);
        lock (gate)
        {
            var candidates = observations.Where(x => x.Key.Environment == request.Scope.Environment)
                .Select(x => x.Value)
                .Where(x => x.Series == request.Scope.Series && InRange(request, x.ExchangeValueDate) &&
                    (request.SamplingSlot is null || x.SamplingSlot == request.SamplingSlot));
            var selected = SelectObservationVintage(candidates, request)
                .OrderBy(x => x.ExchangeValueDate).ThenBy(x => x.SamplingSlot, StringComparer.Ordinal)
                .ThenBy(x => x.Revision).ThenBy(x => x.ObservationId, StringComparer.Ordinal).ToArray();
            return Task.FromResult(Page(selected, request));
        }
    }

    public Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePage(request);
        lock (gate)
        {
            var candidates = metrics.Where(x => x.Key.Environment == request.Scope.Environment)
                .Select(x => x.Value).Where(x => x.Snapshot.Series == request.Scope.Series &&
                    x.Snapshot.MetricPolicyVersion == request.Scope.MetricPolicyVersion &&
                    InRange(request, x.Snapshot.ExchangeValueDate) &&
                    (request.SamplingSlot is null || x.Snapshot.SamplingSlot == request.SamplingSlot));
            if (request.Mode == VolatilityHistoricalMode.AsKnown)
                candidates = candidates.Where(x => x.Snapshot.AvailableAtUtc <= request.KnownAtUtc);
            var selected = candidates.GroupBy(x => (x.Snapshot.ExchangeValueDate, x.Snapshot.SamplingSlot))
                .Select(group => group.OrderByDescending(x => x.Revision)
                    .ThenByDescending(x => x.PublicationSequence).First())
                .OrderBy(x => x.Snapshot.ExchangeValueDate)
                .ThenBy(x => x.Snapshot.SamplingSlot, StringComparer.Ordinal).ToArray();
            return Task.FromResult(Page(selected, request));
        }
    }

    public Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(
        IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            latestCache.Clear();
            foreach (var scope in scopes.Distinct())
                if (durableLatest.TryGetValue(scope, out var pointer)) latestCache[scope] = pointer;
            return Task.FromResult<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>>(
                latestCache.ToImmutableDictionary());
        }
    }

    public void SimulateCacheLoss() { lock (gate) latestCache.Clear(); }

    void AppendObservationCore(string environment, OptionIvObservation observation)
    {
        ValidateObservation(observation);
        if (observation.Revision > 1)
        {
            if (observation.SupersedesObservationId is null ||
                !observations.TryGetValue((environment, observation.SupersedesObservationId), out var superseded) ||
                superseded.Series != observation.Series ||
                superseded.ExchangeValueDate != observation.ExchangeValueDate ||
                superseded.SamplingSlot != observation.SamplingSlot ||
                superseded.Revision >= observation.Revision)
                throw new InvalidOperationException("An observation revision must supersede existing immutable evidence.");
        }
        AppendImmutable(observations, (environment, observation.ObservationId), observation, "observation");
    }

    static IEnumerable<OptionIvObservation> SelectObservationVintage(IEnumerable<OptionIvObservation> values,
        VolatilityHistoryPageRequest request)
    {
        if (request.Mode == VolatilityHistoricalMode.AsKnown)
            values = values.Where(x => x.AvailableAtUtc <= request.KnownAtUtc);
        return values.GroupBy(x => (x.ExchangeValueDate, x.SamplingSlot))
            .Select(group => group.OrderByDescending(x => x.Revision).ThenByDescending(x => x.AvailableAtUtc).First());
    }

    static VolatilityPage<T> Page<T>(T[] values, VolatilityHistoryPageRequest request)
    {
        var offset = request.PagingState is { Length: 4 } state ? BitConverter.ToInt32(state) : 0;
        if (offset < 0 || offset > values.Length) throw new ArgumentException("Invalid paging state.");
        var items = values.Skip(offset).Take(request.PageSize).ToImmutableArray();
        var next = offset + items.Length < values.Length ? BitConverter.GetBytes(offset + items.Length) : null;
        return new(items, next);
    }

    static bool InRange(VolatilityHistoryPageRequest request, DateOnly date) =>
        date >= request.FromValueDate && date <= request.ToValueDate;

    static VolatilityStorageScope Scope(OptionIvPublication publication) =>
        new(publication.Environment, publication.Metric.Snapshot.Series,
            publication.Metric.Snapshot.MetricPolicyVersion);

    static void AppendImmutable<TKey, TValue>(Dictionary<TKey, TValue> store, TKey key, TValue value, string kind)
        where TKey : notnull
    {
        if (store.TryGetValue(key, out var current))
        {
            if (!EqualityComparer<TValue>.Default.Equals(current, value))
                throw new InvalidOperationException($"Immutable {kind} identity collision.");
            return;
        }
        store[key] = value;
    }

    static void ValidatePublication(OptionIvPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ValidateEnvironment(publication.Environment);
        var metric = publication.Metric;
        var snapshot = metric.Snapshot;
        if (metric.Revision < 1 || metric.PublicationSequence < 1 ||
            (metric.Revision == 1) != (metric.SupersedesSnapshotId is null) ||
            string.IsNullOrWhiteSpace(snapshot.SnapshotId) || string.IsNullOrWhiteSpace(snapshot.SnapshotDigest) ||
            publication.SourceObservations.IsDefaultOrEmpty ||
            !snapshot.SourceObservationIds.Order(StringComparer.Ordinal).SequenceEqual(
                publication.SourceObservations.Select(x => x.ObservationId).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            publication.SourceObservations.Any(x => x.Series != snapshot.Series))
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
            throw new ArgumentException("History queries must be bounded to one declared calendar bucket.", nameof(request));
    }

    static void ValidateLatestRequest(LatestVolatilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvironment(request.Scope.Environment);
        if (request.MaximumAge <= TimeSpan.Zero || request.RequestedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("A positive configured freshness requirement is required.", nameof(request));
    }

    static void ValidateEnvironment(string environment) => ArgumentException.ThrowIfNullOrWhiteSpace(environment);
}
