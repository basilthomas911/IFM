using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

[Collection(ScyllaStorageProviderCollection.Name)]
[Trait("Category", "ScyllaDBIntegration")]
public sealed class OptionVolatilityScyllaTests(ScyllaStorageProviderFixture fixture)
{
    [Fact]
    public async Task RealScylla_PreservesIdempotentAppendOnlyPointInTimePublicationSemantics()
    {
        await EnsureSchemaAsync();
        var environment = $"option-volatility-{Guid.NewGuid():N}";
        var scope = new VolatilityStorageScope(environment, new("ES-ATM-30D", "method-v1"), "metric-v1");
        var firstSource = Observation("observation-1", scope.Series, 1, null, At(0));
        var correctionSource = Observation("observation-2", scope.Series, 2, firstSource.ObservationId, At(2));
        var first = Publication(environment, "snapshot-1", 1, firstSource, 1, null);
        var correction = Publication(environment, "snapshot-2", 2, correctionSource, 2, "snapshot-1");
        var repository = new ScyllaOptionVolatilityRepository(fixture.Repository);

        try
        {
            await repository.PublishAsync(first);
            await repository.PublishAsync(first);
            await repository.PublishAsync(correction);

            var latest = await repository.GetLatestAsync(new(scope, At(3), TimeSpan.FromMinutes(2)));
            var exact = await repository.GetSnapshotAsync(environment, "snapshot-1");
            var asKnown = await repository.GetMetricHistoryAsync(History(scope,
                VolatilityHistoricalMode.AsKnown, At(1)));
            var restated = await repository.GetMetricHistoryAsync(History(scope,
                VolatilityHistoricalMode.Restated, null));

            latest.Metric!.Snapshot.SnapshotId.Should().Be("snapshot-2");
            latest.FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Accepted);
            exact!.Snapshot.SnapshotId.Should().Be("snapshot-1");
            asKnown.Items.Should().ContainSingle().Which.Snapshot.SnapshotId.Should().Be("snapshot-1");
            restated.Items.Should().ContainSingle().Which.Snapshot.SnapshotId.Should().Be("snapshot-2");
        }
        finally
        {
            await CleanupAsync(scope, [firstSource.ObservationId, correctionSource.ObservationId],
                ["snapshot-1", "snapshot-2"]);
        }
    }

    async Task EnsureSchemaAsync()
    {
        foreach (var (name, cql) in new[]
                 {
                     ("ObservationHistory", OptionVolatilitySchemaCql.CreateObservationHistory),
                     ("ObservationById", OptionVolatilitySchemaCql.CreateObservationById),
                     ("MetricHistory", OptionVolatilitySchemaCql.CreateMetricHistory),
                     ("SnapshotById", OptionVolatilitySchemaCql.CreateSnapshotById),
                     ("Latest", OptionVolatilitySchemaCql.CreateLatest)
                 })
            await fixture.Repository.Use($"OptionVolatility.Schema.{name}", cql).ExecuteCommandAsync();
    }

    async Task CleanupAsync(VolatilityStorageScope scope, IEnumerable<string> observationIds,
        IEnumerable<string> snapshotIds)
    {
        const string deleteObservationHistory = "DELETE FROM option_iv_observation_history WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology AND calendar_bucket=:bucket;";
        const string deleteObservation = "DELETE FROM option_iv_observation_by_id WHERE environment=:environment AND observation_id=:id;";
        const string deleteMetricHistory = "DELETE FROM option_iv_metric_history WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology AND metric_policy_version=:policy AND calendar_bucket=:bucket;";
        const string deleteSnapshot = "DELETE FROM option_iv_snapshot_by_id WHERE environment=:environment AND snapshot_id=:id;";
        const string deleteLatest = "DELETE FROM option_iv_latest WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology AND metric_policy_version=:policy;";
        var bucket = VolatilityCalendarBucket.From(new(2026, 9, 18)).Value;
        await fixture.Repository.Use("OptionVolatility.Cleanup.ObservationHistory", deleteObservationHistory)
            .SetParameters(new Values([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion, bucket]))
            .ExecuteCommandAsync();
        foreach (var id in observationIds)
            await fixture.Repository.Use("OptionVolatility.Cleanup.Observation", deleteObservation)
                .SetParameters(new Values([scope.Environment, id])).ExecuteCommandAsync();
        await fixture.Repository.Use("OptionVolatility.Cleanup.MetricHistory", deleteMetricHistory)
            .SetParameters(new Values([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion,
                scope.MetricPolicyVersion, bucket])).ExecuteCommandAsync();
        foreach (var id in snapshotIds)
            await fixture.Repository.Use("OptionVolatility.Cleanup.Snapshot", deleteSnapshot)
                .SetParameters(new Values([scope.Environment, id])).ExecuteCommandAsync();
        await fixture.Repository.Use("OptionVolatility.Cleanup.Latest", deleteLatest)
            .SetParameters(new Values([scope.Environment, scope.Series.SeriesId, scope.Series.MethodologyVersion,
                scope.MetricPolicyVersion])).ExecuteCommandAsync();
    }

    static OptionIvObservation Observation(string id, VolatilitySeriesIdentity series, int revision,
        string? supersedes, DateTimeOffset available) =>
        new(1, id, series, new(2026, 9, 18), "daily-close", 0.20m + revision / 100m,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, string.Empty,
            available.AddMinutes(-1), available.AddSeconds(-1), available, revision, supersedes,
            new([], "black76-v1", $"digest-{id}", "evidence-v1"));

    static OptionIvPublication Publication(string environment, string id, long sequence,
        OptionIvObservation source, int revision, string? supersedes) =>
        new(environment,
            new(new(1, id, $"digest-{id}", source.Series, "metric-v1", source.ExchangeValueDate,
                    source.SamplingSlot, source.ImpliedVolatility, VolatilityValueUnit.AnnualDecimal,
                    50m, VolatilityMetricStatus.Qualified, 50m, VolatilityMetricStatus.Qualified,
                    VolatilityMetricUnit.PercentagePoints0To100, 0.10m, 0.40m, 1, 0, 2, 2, 1m,
                    new(2026, 9, 16), new(2026, 9, 17), [source.ObservationId], $"sources-{id}",
                    "calculator-v1", source.ObservedAtUtc, source.AvailableAtUtc.AddSeconds(-2),
                    source.AvailableAtUtc.AddSeconds(-1), source.AvailableAtUtc),
                revision, supersedes, sequence),
            [source]);

    static VolatilityHistoryPageRequest History(VolatilityStorageScope scope,
        VolatilityHistoricalMode mode, DateTimeOffset? knownAt) =>
        new(scope, new(2026, 9), new(2026, 9, 18), new(2026, 9, 18), null, mode, knownAt, 50, null);

    static DateTimeOffset At(int minute) => new(2026, 9, 18, 20, minute, 0, TimeSpan.Zero);

    readonly record struct Values(object?[] Items) : IBindValue
    {
        public object Bind() => Items;
    }
}
