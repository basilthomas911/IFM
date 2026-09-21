using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.OptionVolatility;

public sealed class VolatilityRankPercentileFeatureTests
{
    [Fact]
    public async Task GivenQualifiedConstruction_WhenMetricIsPublished_ThenLatestHistoryAndExactEvidenceAgree()
    {
        var now = new DateTimeOffset(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);
        var definition = Definition(now);
        var current = VolatilitySeriesConstructor.Construct(new(definition, new(2026, 9, 18),
            "daily-close", now, 1, null,
            [Input("call", true, 0.30m, now), Input("put", false, 0.40m, now)])).Observation;
        var prior1 = DurableObservation("prior-1", new(2026, 9, 16), 0.20m, now.AddDays(-2));
        var prior2 = DurableObservation("prior-2", new(2026, 9, 17), 0.30m, now.AddDays(-1));
        var calculation = VolatilityRankPercentileCalculator.Calculate(definition.Metrics,
            new(current.ExchangeValueDate, [prior1.ExchangeValueDate, prior2.ExchangeValueDate]),
            VolatilityCalculationObservation.From(current),
            new[] { prior1, prior2 }.Select(VolatilityCalculationObservation.From));
        var metric = Metric("snapshot-happy", 1, current, calculation, now);
        var repository = new InMemoryOptionVolatilityRepository();
        var service = new OptionVolatilityService(repository);

        await service.PublishAsync(new("simulation", metric, [prior1, prior2, current]));

        var scope = new VolatilityStorageScope("simulation", definition.Identity, definition.Metrics.PolicyVersion);
        var latest = await service.GetLatestAsync(new(scope, now.AddSeconds(30), TimeSpan.FromMinutes(1)));
        var history = await service.GetMetricHistoryAsync(History(scope, new(2026, 9, 16), new(2026, 9, 18)));
        var observations = await service.GetObservationHistoryAsync(History(scope, new(2026, 9, 16), new(2026, 9, 18)));
        var exact = await service.GetSnapshotAsync("simulation", "snapshot-happy");

        current.Status.Should().Be(VolatilityObservationStatus.Qualified);
        calculation.IvRank.Should().Be(100m);
        calculation.IvPercentile.Should().Be(100m);
        latest.Metric.Should().Be(metric);
        history.Items.Should().ContainSingle().Which.Should().Be(metric);
        observations.Items.Select(x => x.ObservationId).Should().BeEquivalentTo("prior-1", "prior-2", current.ObservationId);
        exact.Should().Be(metric);
        exact!.Snapshot.SourceObservationIds.Should().Equal(calculation.SourceObservationIds);
    }

    [Fact]
    public async Task GivenClosedMarketFixture_WhenVirtualTimeAdvances_ThenFreshnessIsDeterministicWithoutLiveInfrastructure()
    {
        var clock = new ManualTimeProvider(new(2026, 9, 19, 16, 0, 0, TimeSpan.Zero));
        var source = DurableObservation("closed-market", new(2026, 9, 18), 0.25m, clock.GetUtcNow());
        var calculation = new VolatilityMetricCalculation(0.25m, VolatilityValueUnit.AnnualDecimal, 50m,
            VolatilityMetricStatus.Qualified, 50m, VolatilityMetricStatus.Qualified,
            VolatilityMetricUnit.PercentagePoints0To100, 0.10m, 0.40m, 1, 0, 2, 2, 1m,
            new(2026, 9, 16), new(2026, 9, 17), [source.ObservationId]);
        var metric = Metric("closed-snapshot", 1, source, calculation, clock.GetUtcNow());
        var service = new OptionVolatilityService(new InMemoryOptionVolatilityRepository());
        await service.PublishAsync(new("simulation", metric, [source]));
        var scope = new VolatilityStorageScope("simulation", source.Series, "metric-v1");

        clock.Advance(TimeSpan.FromMinutes(4));
        (await service.GetLatestAsync(new(scope, clock.GetUtcNow(), TimeSpan.FromMinutes(5))))
            .FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Accepted);
        clock.Advance(TimeSpan.FromMinutes(2));
        (await service.GetLatestAsync(new(scope, clock.GetUtcNow(), TimeSpan.FromMinutes(5))))
            .FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Stale);
        (await service.GetSnapshotAsync("simulation", "closed-snapshot")).Should().Be(metric);
    }

    [Fact]
    public void GivenSyntheticClosedMarketHistory_WhenRollAndLateCorrectionOccur_ThenAsKnownAndRestatedStayDistinct()
    {
        var clock = new ManualTimeProvider(new(2026, 9, 20, 16, 0, 0, TimeSpan.Zero));
        var fixture = SyntheticVolatilityHistoryFixtureFactory.CreateClosedMarket(Definition(clock.GetUtcNow()), clock);

        var asKnown = fixture.AsKnownAt(clock.GetUtcNow().AddHours(-1));
        var restated = fixture.AsKnownAt(clock.GetUtcNow());

        fixture.IsSynthetic.Should().BeTrue();
        fixture.MarketState.Should().Be("Closed");
        asKnown.Should().HaveCount(fixture.Definition.Metrics.HistoricalLookbackSessions + 1);
        asKnown.Should().NotContain(x => x.Revision == 2);
        restated.Should().ContainSingle(x => x.Revision == 2);
        fixture.Events.Should().ContainSingle(x => x.Kind == SyntheticVolatilityFixtureEventKind.FuturesRoll);
        fixture.Events.Should().ContainSingle(x => x.Kind == SyntheticVolatilityFixtureEventKind.Correction);
    }

    [Fact]
    public void GivenQualifiedPriorSessions_WhenCurrentIvIsCalculated_ThenReusableMetricsCarryExactEvidence()
    {
        var currentDate = new DateOnly(2026, 9, 18);
        ImmutableArray<DateOnly> priorDates =
        [
            new(2026, 9, 14),
            new(2026, 9, 15),
            new(2026, 9, 16),
            new(2026, 9, 17)
        ];
        var policy = new VolatilityMetricPolicy(
            "qualified-policy-v1",
            4,
            4,
            1m,
            VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions,
            VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent);
        var history = new[] { 0.10m, 0.20m, 0.30m, 0.40m }
            .Select((iv, index) => Observation($"prior-{index}", priorDates[index], iv));

        var result = VolatilityRankPercentileCalculator.Calculate(
            policy,
            new(currentDate, priorDates),
            Observation("current", currentDate, 0.35m),
            history);

        result.RankStatus.Should().Be(VolatilityMetricStatus.Qualified);
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.Qualified);
        result.IvRank.Should().Be(250m / 3m);
        result.IvPercentile.Should().Be(75m);
        result.ValidHistoricalObservationCount.Should().Be(4);
        result.ExpectedHistoricalObservationCount.Should().Be(4);
        result.SourceObservationIds.Should().BeEquivalentTo(
            "current", "prior-0", "prior-1", "prior-2", "prior-3");
    }

    [Fact]
    public void GivenOptionalOrRequiredPolicy_WhenEvidenceIsRecorded_ThenRequirementIsExplicitNotNumericFallback()
    {
        var series = new VolatilitySeriesIdentity("ES-ATM-CM", "methodology-v1");
        var required = new VolatilityWorkflowDependencyPolicy(
            1, "selection-volatility", "v1", series, "metric-v1",
            VolatilityDependencyRequirement.Required);
        var optional = required with
        {
            DependencyPolicyVersion = "v2",
            Requirement = VolatilityDependencyRequirement.Optional
        };

        required.Requirement.Should().Be(VolatilityDependencyRequirement.Required);
        optional.Requirement.Should().Be(VolatilityDependencyRequirement.Optional);
        optional.Should().NotBe(required);
    }

    static VolatilityCalculationObservation Observation(string id, DateOnly date, decimal iv) =>
        new(id, date, iv, VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified);

    static VolatilitySeriesDefinition Definition(DateTimeOffset now) => new(
        1, new("ES-ATM-30D", "method-v1"), "ES", "XCME", "USD",
        new("simulation", "fixture", "quotes-v1"),
        new(30, VolatilityMoneynessConvention.AtTheMoneyForward,
            VolatilityOptionSideSelection.CallPutCombined, ["EW"], VolatilitySideCombinationMethod.ArithmeticMean),
        new("European", "FuturesStyle", "DeliveryOfFuture", ["black76-v1"], "solver-v1"),
        new("mid", "quality-v1", true, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)),
        new("bracket-v1", VolatilityInterpolationMethod.LinearTotalVariance, "roll-v1", "calendar-v1",
            "America/Chicago", "daily-v1", TimeSpan.FromMinutes(5), 2),
        new("metric-v1", 2, 2, 1m, VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions,
            VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent),
        new(now.AddMonths(-1), null, "config-v1", "market-data", "evidence-v1", []));

    static QualifiedOptionIvInput Input(string id, bool call, decimal iv, DateTimeOffset now) =>
        new(id, "ESZ6", "ES", "simulation", "EW", "XCME", "USD", "fixture", "quotes-v1",
            "European", "FuturesStyle", "DeliveryOfFuture", "black76-v1", "solver-v1", now.AddDays(30),
            5000m, call, iv, 5000m, now.AddSeconds(-2), now.AddSeconds(-3), 1, 2,
            Guid.Parse("11111111-1111-1111-1111-111111111111"), $"digest-{id}");

    static OptionIvObservation DurableObservation(string id, DateOnly date, decimal iv, DateTimeOffset available) =>
        new(1, id, new("ES-ATM-30D", "method-v1"), date, "daily-close", iv,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, string.Empty,
            available.AddMinutes(-1), available.AddSeconds(-1), available, 1, null,
            new([], "black76-v1", $"digest-{id}", "evidence-v1"));

    static OptionIvMetricRevision Metric(string id, long sequence, OptionIvObservation current,
        VolatilityMetricCalculation calculation, DateTimeOffset available) =>
        new(new(1, id, $"digest-{id}", current.Series, "metric-v1", current.ExchangeValueDate,
                current.SamplingSlot, calculation.CurrentImpliedVolatility, calculation.ImpliedVolatilityUnit,
                calculation.IvRank, calculation.RankStatus, calculation.IvPercentile,
                calculation.PercentileStatus, calculation.MetricUnit, calculation.RankLowImpliedVolatility,
                calculation.RankHighImpliedVolatility, calculation.HistoricalBelowCurrentCount,
                calculation.HistoricalTieCount, calculation.ValidHistoricalObservationCount,
                calculation.ExpectedHistoricalObservationCount, calculation.CoverageRatio,
                calculation.WindowStartValueDate, calculation.WindowEndValueDate,
                calculation.SourceObservationIds, $"sources-{id}", "calculator-v1", current.ObservedAtUtc,
                available.AddSeconds(-2), available.AddSeconds(-1), available), 1, null, sequence);

    static VolatilityHistoryPageRequest History(VolatilityStorageScope scope, DateOnly from, DateOnly to) =>
        new(scope, VolatilityCalendarBucket.From(from), from, to, null, VolatilityHistoricalMode.Restated,
            null, 50, null);

    sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset current = utcNow;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current += amount;
    }
}
