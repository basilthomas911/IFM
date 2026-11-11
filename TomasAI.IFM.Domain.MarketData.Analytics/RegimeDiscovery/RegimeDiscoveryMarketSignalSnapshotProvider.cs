using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;

namespace TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

/// <summary>Provides atomic, revision-stable Regime Discovery snapshots from process-local latest signals.</summary>
public sealed class RegimeDiscoveryMarketSignalSnapshotProvider(TimeProvider? timeProvider = null,
    IMarketSessionCalendar? sessionCalendar = null)
    : IRegimeDiscoveryMarketSignalSnapshotProvider,
      IRegimeDiscoveryMarketSignalCache
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    readonly IMarketSessionCalendar calendar = sessionCalendar ?? new CmeFuturesMarketSessionCalendar();
    static readonly ConcurrentDictionary<MarketAnalyticsSignalKey, RegimeDiscoverySignalObservation> Observations = new();
    static long revision;

    /// <inheritdoc />
    public long Revision => Interlocked.Read(ref revision);

    /// <inheritdoc />
    public void Upsert(RegimeDiscoverySignalObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var errors = new MarketAnalyticsSignalKeyValidationRules().Execute(observation.SignalKey);
        if (errors.Length != 0)
            throw new ArgumentException(string.Join("; ", errors.Select(value => value.ErrorMessage)),
                nameof(observation));
        // Historical replay and restarted live workers do not share a sequence
        // domain. Market time orders observations across those generations;
        // sequence and calculation time only resolve the same source timestamp.
        while (true)
        {
            if (!Observations.TryGetValue(observation.SignalKey, out var current))
            {
                if (!Observations.TryAdd(observation.SignalKey, observation)) continue;
            }
            else
            {
                if (observation == current || observation.MarketDataAsOfUtc < current.MarketDataAsOfUtc
                    || observation.MarketDataAsOfUtc == current.MarketDataAsOfUtc
                    && (observation.SourceSequence < current.SourceSequence
                        || observation.SourceSequence == current.SourceSequence
                        && observation.CalculatedAtUtc < current.CalculatedAtUtc)) return;
                if (!Observations.TryUpdate(observation.SignalKey, observation, current)) continue;
            }
            Interlocked.Increment(ref revision);
            return;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        Observations.Clear();
        Interlocked.Increment(ref revision);
    }

    /// <inheritdoc />
    public ValueTask<RegimeDiscoveryMarketSignalSnapshotResult> CaptureAsync(
        RegimeDiscoveryMarketSignalSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = new RegimeDiscoveryMarketSignalSnapshotRequestValidationRules().Execute(request);
        if (validation.Length != 0)
            throw new ArgumentException(string.Join("; ", validation.Select(value => value.ErrorMessage)),
                nameof(request));
        for (var attempt = 0; attempt < request.CaptureAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = Revision;
            var capturedAt = businessClock.GetUtcNow().UtcDateTime;
            var evaluated = request.Requirements
                .OrderBy(value => value.TimeFrame)
                .ThenBy(value => value.Metric)
                .Select(requirement => Evaluate(request, requirement, capturedAt))
                .ToArray();
            var after = Revision;
            if (before != after)
                continue;
            var issues = evaluated.Where(value => value.Availability != RegimeDiscoverySignalAvailability.Available)
                .ToArray();
            var requiredKeys = request.Requirements.Where(value => value.IsRequired)
                .Select(value => (value.Metric, value.TimeFrame)).ToHashSet();
            var success = issues.All(value => !requiredKeys.Contains((value.Metric, value.SignalKey.TimeFrame)));
            return ValueTask.FromResult(new RegimeDiscoveryMarketSignalSnapshotResult
            {
                IsSuccess = success,
                Snapshot = success ? new RegimeDiscoveryMarketSignalSnapshot
                {
                    SnapshotId = Guid.CreateVersion7(new DateTimeOffset(capturedAt)),
                    CacheRevision = after,
                    MarketSeriesIdentity = request.MarketSeriesIdentity,
                    TargetHorizon = request.TargetHorizon,
                    CapturedAtUtc = capturedAt,
                    MarketDataAsOfUtc = evaluated
                        .Where(value => value.Availability == RegimeDiscoverySignalAvailability.Available)
                        .Select(value => value.MarketDataAsOfUtc).DefaultIfEmpty(capturedAt).Max(),
                    Observations = evaluated
                } : null,
                Issues = issues
            });
        }
        var consistency = request.Requirements.Select(requirement => Missing(request, requirement,
            RegimeDiscoverySignalAvailability.SnapshotConsistencyFailure)).ToArray();
        return ValueTask.FromResult(new RegimeDiscoveryMarketSignalSnapshotResult
        {
            IsSuccess = false,
            Issues = consistency
        });
    }

    RegimeDiscoverySignalObservation Evaluate(
        RegimeDiscoveryMarketSignalSnapshotRequest request,
        RegimeDiscoverySignalRequirement requirement,
        DateTime capturedAtUtc)
    {
        var key = new MarketAnalyticsSignalKey(request.MarketSeriesIdentity, Kind(requirement.Metric),
            requirement.TimeFrame, requirement.CalculationConfigurationId);
        if (!Observations.TryGetValue(key, out var source) &&
            !((requirement.Metric is RegimeDiscoverySignalMetric.VxFrontSecondRatio or
                RegimeDiscoverySignalMetric.VixLevel or RegimeDiscoverySignalMetric.VxFrontLevel) &&
              TryGetExternal(requirement, out source)))
            return Missing(request, requirement, RegimeDiscoverySignalAvailability.Missing);
        var maximumAge = TimeSpan.FromSeconds(requirement.MaximumAgeSeconds);
        var age = MarketTradingTime.Elapsed(calendar, new DateTimeOffset(source.MarketDataAsOfUtc),
            new DateTimeOffset(capturedAtUtc), maximumAge);
        var availability = (source.Availability != RegimeDiscoverySignalAvailability.Available) switch
{
    true => source.Availability,
    false => (!source.IsWarm) switch
    {
        true => RegimeDiscoverySignalAvailability.NotWarm,
        false => (!source.IsValid) switch
        {
            true => RegimeDiscoverySignalAvailability.Invalid,
            false => (source.MarketDataAsOfUtc > capturedAtUtc.AddSeconds(request.FutureClockSkewSeconds)) switch
            {
                true => RegimeDiscoverySignalAvailability.FutureTimestamp,
                false => (age > maximumAge) switch
                {
                    true => RegimeDiscoverySignalAvailability.Stale,
                    false => (!request.SupportedSchemaVersions.Contains(source.SchemaVersion)) switch
                    {
                        true => RegimeDiscoverySignalAvailability.SchemaUnsupported,
                        false => !request.ApprovedCalculationVersions.Contains(source.CalculationVersion, StringComparer.Ordinal) ? RegimeDiscoverySignalAvailability.CalculationVersionMismatch : RegimeDiscoverySignalAvailability.Available
                    }}}}}};
        var freshness = availability == RegimeDiscoverySignalAvailability.Available
            ? Math.Clamp(1m - (decimal)age.TotalSeconds /
                requirement.MaximumAgeSeconds, 0m, 1m)
            : 0m;
        return source with { Availability = availability, FreshnessFactor = freshness };
    }

    static bool TryGetExternal(
        RegimeDiscoverySignalRequirement requirement,
        out RegimeDiscoverySignalObservation observation)
    {
        observation = Observations.Values
            .Where(value => value.Metric == requirement.Metric &&
                            value.SignalKey.TimeFrame == requirement.TimeFrame &&
                            string.Equals(value.SignalKey.CalculationConfigurationId,
                                requirement.CalculationConfigurationId, StringComparison.Ordinal))
            .OrderByDescending(value => value.MarketDataAsOfUtc)
            .ThenByDescending(value => value.SourceSequence)
            .FirstOrDefault()!;
        return observation is not null;
    }

    static RegimeDiscoverySignalObservation Missing(
        RegimeDiscoveryMarketSignalSnapshotRequest request,
        RegimeDiscoverySignalRequirement requirement,
        RegimeDiscoverySignalAvailability availability) => new()
        {
            Metric = requirement.Metric,
            SignalKey = new MarketAnalyticsSignalKey(request.MarketSeriesIdentity, Kind(requirement.Metric),
                requirement.TimeFrame, requirement.CalculationConfigurationId),
            Availability = availability,
            SignalIdentity = $"{request.MarketSeriesIdentity.Format()}.{requirement.Metric}.{requirement.TimeFrame}"
        };

    static MarketAnalyticsSignalKind Kind(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or
            RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema20Slope or
            RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Ema20Interaction => MarketAnalyticsSignalKind.Ema,
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => MarketAnalyticsSignalKind.Rsi,
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14 or
            RegimeDiscoverySignalMetric.MinusDi14 => MarketAnalyticsSignalKind.Adx,
        RegimeDiscoverySignalMetric.MacdHistogram => MarketAnalyticsSignalKind.Macd,
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio or
            RegimeDiscoverySignalMetric.AtrNormalizedRange => MarketAnalyticsSignalKind.Atr,
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio or
            RegimeDiscoverySignalMetric.BollingerPosition => MarketAnalyticsSignalKind.BollingerBand,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio or RegimeDiscoverySignalMetric.VixLevel or
            RegimeDiscoverySignalMetric.VxFrontLevel =>
            MarketAnalyticsSignalKind.VxTermStructure,
        RegimeDiscoverySignalMetric.ItiDirection or RegimeDiscoverySignalMetric.ItiBandLevel or
            RegimeDiscoverySignalMetric.ItiReversalLevel or RegimeDiscoverySignalMetric.CurrentPrice =>
            MarketAnalyticsSignalKind.Iti,
        RegimeDiscoverySignalMetric.Tdi => MarketAnalyticsSignalKind.Tdi,
        _ => MarketAnalyticsSignalKind.MarketStructure
    };
}
