using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class VolatilityRankPercentileCalculatorTests
{
    static readonly DateOnly CurrentDate = new(2026, 9, 18);
    static readonly ImmutableArray<DateOnly> PriorDates =
    [
        new(2026, 9, 14),
        new(2026, 9, 15),
        new(2026, 9, 16),
        new(2026, 9, 17)
    ];

    [Fact]
    public void Calculate_AppliesReferenceFormulasWithCurrentIncludedOnlyInRankBounds()
    {
        var result = Calculate(0.25m, 0.10m, 0.20m, 0.30m, 0.40m);

        result.IvRank.Should().Be(50m);
        result.IvPercentile.Should().Be(50m);
        result.RankLowImpliedVolatility.Should().Be(0.10m);
        result.RankHighImpliedVolatility.Should().Be(0.40m);
        result.HistoricalBelowCurrentCount.Should().Be(2);
        result.HistoricalTieCount.Should().Be(0);
    }

    [Fact]
    public void Calculate_UsesStrictLessThanForPercentileTies()
    {
        var result = Calculate(0.20m, 0.10m, 0.20m, 0.20m, 0.30m);

        result.IvRank.Should().Be(50m);
        result.IvPercentile.Should().Be(25m);
        result.HistoricalBelowCurrentCount.Should().Be(1);
        result.HistoricalTieCount.Should().Be(2);
    }

    [Fact]
    public void Calculate_FlatRangeHasIndependentUndefinedRankAndQualifiedPercentile()
    {
        var result = Calculate(0.20m, 0.20m, 0.20m, 0.20m, 0.20m);

        result.RankStatus.Should().Be(VolatilityMetricStatus.UndefinedRange);
        result.IvRank.Should().BeNull();
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.Qualified);
        result.IvPercentile.Should().Be(0m);
        result.HistoricalTieCount.Should().Be(4);
    }

    [Theory]
    [InlineData(0.50, 100, 100)]
    [InlineData(0.05, 0, 0)]
    public void Calculate_CurrentNewExtremumRemainsBounded(
        double current,
        double expectedRank,
        double expectedPercentile)
    {
        var result = Calculate((decimal)current, 0.10m, 0.20m, 0.30m, 0.40m);

        result.IvRank.Should().Be((decimal)expectedRank);
        result.IvPercentile.Should().Be((decimal)expectedPercentile);
        result.IvRank.Should().BeInRange(0m, 100m);
    }

    [Fact]
    public void Calculate_RollingWindowExpiresOldObservationWithoutExpandingBackward()
    {
        var oldExtreme = Observation(new(2026, 9, 11), 0.90m, "expired");
        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, 0.35m, "current"),
            new[]
            {
                oldExtreme,
                Observation(PriorDates[0], 0.10m),
                Observation(PriorDates[1], 0.20m),
                Observation(PriorDates[2], 0.30m),
                Observation(PriorDates[3], 0.40m)
            });

        result.IvRank.Should().Be(250m / 3m);
        result.IvPercentile.Should().Be(75m);
        result.SourceObservationIds.Should().NotContain("expired");
        result.ExpectedHistoricalObservationCount.Should().Be(4);
    }

    [Fact]
    public void Calculate_MissingSessionsReturnInsufficientHistoryAndPersistCoverageCounts()
    {
        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(minimumValid: 3, minimumCoverage: 0.75m),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, 0.30m, "current"),
            new[]
            {
                Observation(PriorDates[0], 0.10m),
                Observation(PriorDates[1], 0.20m)
            });

        result.RankStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
        result.IvRank.Should().BeNull();
        result.IvPercentile.Should().BeNull();
        result.ValidHistoricalObservationCount.Should().Be(2);
        result.ExpectedHistoricalObservationCount.Should().Be(4);
        result.CoverageRatio.Should().Be(0.5m);
    }

    [Fact]
    public void Calculate_InvalidHistoricalValuesAreNotValidOrReplacedByOlderValues()
    {
        var nonFinite = VolatilityCalculationObservation.FromFloatingPoint(
            "nan",
            PriorDates[2],
            double.NaN);
        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(minimumValid: 4, minimumCoverage: 1m),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, 0.30m, "current"),
            new[]
            {
                Observation(new(2026, 9, 11), 0.25m, "older-substitute"),
                Observation(PriorDates[0], 0.10m),
                Observation(PriorDates[1], 0.20m),
                nonFinite,
                Observation(PriorDates[3], 0.40m)
            });

        nonFinite.Status.Should().Be(VolatilityObservationStatus.Invalid);
        nonFinite.ImpliedVolatility.Should().BeNull();
        result.ValidHistoricalObservationCount.Should().Be(3);
        result.ExpectedHistoricalObservationCount.Should().Be(4);
        result.RankStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
        result.SourceObservationIds.Should().NotContain("older-substitute");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calculate_NonFiniteCurrentValueIsInvalid(double value)
    {
        var current = VolatilityCalculationObservation.FromFloatingPoint("current", CurrentDate, value);

        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(),
            new(CurrentDate, PriorDates),
            current,
            History(0.10m, 0.20m, 0.30m, 0.40m));

        result.RankStatus.Should().Be(VolatilityMetricStatus.InvalidCurrentObservation);
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.InvalidCurrentObservation);
        result.IvRank.Should().BeNull();
        result.IvPercentile.Should().BeNull();
    }

    [Fact]
    public void Calculate_NegativeCurrentValueIsInvalid()
    {
        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, -0.01m, "current"),
            History(0.10m, 0.20m, 0.30m, 0.40m));

        result.RankStatus.Should().Be(VolatilityMetricStatus.InvalidCurrentObservation);
        result.PercentileStatus.Should().Be(VolatilityMetricStatus.InvalidCurrentObservation);
    }

    [Fact]
    public void Calculate_UsesAnnualDecimalInputsAndPercentagePointOutputs()
    {
        var result = Calculate(0.20m, 0.10m, 0.15m, 0.25m, 0.30m);

        result.CurrentImpliedVolatility.Should().Be(0.20m);
        result.ImpliedVolatilityUnit.Should().Be(VolatilityValueUnit.AnnualDecimal);
        result.IvRank.Should().Be(50m);
        result.IvPercentile.Should().Be(50m);
        result.MetricUnit.Should().Be(VolatilityMetricUnit.PercentagePoints0To100);
    }

    [Fact]
    public void Calculate_WrongUnitIsNotSilentlyConverted()
    {
        var history = History(0.10m, 0.20m, 0.30m, 0.40m).ToArray();
        history[0] = history[0] with { Unit = VolatilityValueUnit.Unspecified };

        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(minimumValid: 4, minimumCoverage: 1m),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, 0.25m, "current"),
            history);

        result.ValidHistoricalObservationCount.Should().Be(3);
        result.RankStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
    }

    [Fact]
    public void Calculate_DuplicateSessionIsNotSilentlySelected()
    {
        var history = History(0.10m, 0.20m, 0.30m, 0.40m).ToList();
        history.Add(Observation(PriorDates[0], 0.11m, "duplicate"));

        var result = VolatilityRankPercentileCalculator.Calculate(
            Policy(minimumValid: 4, minimumCoverage: 1m),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, 0.25m, "current"),
            history);

        result.ValidHistoricalObservationCount.Should().Be(3);
        result.RankStatus.Should().Be(VolatilityMetricStatus.InsufficientHistory);
    }

    [Fact]
    public void Calculate_RejectsWindowThatDoesNotEqualConfiguredLookback()
    {
        var act = () => VolatilityRankPercentileCalculator.Calculate(
            Policy(),
            new(CurrentDate, PriorDates.RemoveAt(0)),
            Observation(CurrentDate, 0.25m, "current"),
            History(0.10m, 0.20m, 0.30m, 0.40m));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exact prior-session window*");
    }

    static VolatilityMetricCalculation Calculate(decimal current, params decimal[] history) =>
        VolatilityRankPercentileCalculator.Calculate(
            Policy(),
            new(CurrentDate, PriorDates),
            Observation(CurrentDate, current, "current"),
            History(history));

    static IEnumerable<VolatilityCalculationObservation> History(params decimal[] values) =>
        values.Select((value, index) => Observation(PriorDates[index], value));

    static VolatilityCalculationObservation Observation(
        DateOnly date,
        decimal value,
        string? id = null) =>
        new(
            id ?? $"observation-{date:yyyyMMdd}",
            date,
            value,
            VolatilityValueUnit.AnnualDecimal,
            VolatilityObservationStatus.Qualified);

    static VolatilityMetricPolicy Policy(
        int minimumValid = 4,
        decimal minimumCoverage = 1m) =>
        new(
            "metric-policy-test-v1",
            4,
            minimumValid,
            minimumCoverage,
            VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions,
            VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent);
}
