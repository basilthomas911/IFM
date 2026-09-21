using FluentAssertions;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class SyntheticVolatilityHistoryFixtureTests
{
    [Fact]
    public void Closed_market_fixture_has_full_window_roll_and_append_only_correction_under_virtual_time()
    {
        var clock = new ManualClock(new(2026, 9, 20, 16, 0, 0, TimeSpan.Zero));
        var definition = OptionVolatilityTestData.Definition() with
        {
            Metrics = OptionVolatilityTestData.Definition().Metrics with
            {
                HistoricalLookbackSessions = 10,
                MinimumValidObservations = 10
            }
        };

        var fixture = SyntheticVolatilityHistoryFixtureFactory.CreateClosedMarket(definition, clock);
        var beforeCorrection = fixture.AsKnownAt(clock.GetUtcNow().AddHours(-1));
        var restated = fixture.AsKnownAt(clock.GetUtcNow());

        fixture.IsSynthetic.Should().BeTrue();
        fixture.Environment.Should().Be("simulation");
        fixture.MarketState.Should().Be("Closed");
        beforeCorrection.Should().HaveCount(definition.Metrics.HistoricalLookbackSessions + 1);
        restated.Should().HaveCount(beforeCorrection.Length);
        fixture.AppendOnlyObservations.Should().HaveCount(beforeCorrection.Length + 1);
        fixture.Events.Should().ContainSingle(x => x.Kind == SyntheticVolatilityFixtureEventKind.FuturesRoll);
        fixture.Events.Should().ContainSingle(x => x.Kind == SyntheticVolatilityFixtureEventKind.Correction);
        fixture.AppendOnlyObservations.SelectMany(x => x.Provenance.Contributors)
            .Select(x => x.UnderlyingFuturesContractId).Distinct().Should().Equal("ESU6", "ESZ6");
        restated.Single(x => x.Revision == 2).SupersedesObservationId.Should().NotBeNull();
        fixture.Events.Should().OnlyContain(x => x.Description.Contains("SYNTHETIC", StringComparison.Ordinal));
    }

    [Fact]
    public void Fixture_calculation_uses_exact_configured_lookback_without_future_correction_leakage()
    {
        var clock = new ManualClock(new(2026, 9, 20, 16, 0, 0, TimeSpan.Zero));
        var definition = OptionVolatilityTestData.Definition();
        var fixture = SyntheticVolatilityHistoryFixtureFactory.CreateClosedMarket(definition, clock);
        var known = fixture.AsKnownAt(clock.GetUtcNow().AddHours(-1));
        var current = known[^1];
        var prior = known[..^1];

        var result = VolatilityRankPercentileCalculator.Calculate(definition.Metrics,
            new(current.ExchangeValueDate, prior.Select(x => x.ExchangeValueDate).ToImmutableArray()),
            VolatilityCalculationObservation.From(current), prior.Select(VolatilityCalculationObservation.From));

        result.ValidHistoricalObservationCount.Should().Be(definition.Metrics.HistoricalLookbackSessions);
        result.ExpectedHistoricalObservationCount.Should().Be(definition.Metrics.HistoricalLookbackSessions);
        result.RankStatus.Should().Be(VolatilityMetricStatus.Qualified);
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.Qualified);
        known.Should().NotContain(x => x.Revision == 2);
    }

    sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
