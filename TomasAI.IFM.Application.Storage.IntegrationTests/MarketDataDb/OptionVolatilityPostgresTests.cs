using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class OptionVolatilityPostgresTests
{
    [Fact]
    public async Task Postgres_PreservesIdempotentAppendOnlyPointInTimePublicationSemantics()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("option-volatility-schema");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync(new[] { "market_data" });
        var environment = $"option-volatility-{Guid.NewGuid():N}";
        var scope = new VolatilityStorageScope(environment, new("ES-ATM-30D", "method-v1"), "metric-v1");
        var firstSource = Observation("observation-1", scope.Series, 1, null, At(0));
        var correctionSource = Observation("observation-2", scope.Series, 2, firstSource.ObservationId, At(2));
        var first = Publication(environment, "snapshot-1", 1, firstSource, 1, null);
        var correction = Publication(environment, "snapshot-2", 2, correctionSource, 2, "snapshot-1");
        var repository = new MarketDataDbContext(settings, Substitute.For<IDbContextFactory>(),
            Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);

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

}
