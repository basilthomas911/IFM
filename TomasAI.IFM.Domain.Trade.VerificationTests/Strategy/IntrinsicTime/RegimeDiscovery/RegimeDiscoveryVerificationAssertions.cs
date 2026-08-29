using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

public static class RegimeDiscoveryVerificationAssertions
{
    public static void MatchScenario(
        this RegimeDiscoveryResult result,
        RegimeDiscoveryScenario scenario,
        bool assertRuntimeConfidence = false,
        bool assertGoldenScores = true)
    {
        result.Trend.IsComplete.Should().BeTrue();
        result.Trend.Direction.Should().Be(scenario.TrendDirection);
        result.Trend.Strength.Should().Be(scenario.TrendStrength);
        result.Trend.Phase.Should().Be(scenario.TrendPhase);
        if (assertGoldenScores && scenario.TrendScore is { } trendScore)
            result.Trend.Score.Should().Be(trendScore);

        result.Volatility.IsComplete.Should().BeTrue();
        result.Volatility.Level.Should().Be(scenario.VolatilityLevel);
        result.Volatility.Change.Should().Be(scenario.VolatilityChange);
        result.Volatility.TermStructure.Should().Be(scenario.TermStructure);
        result.Volatility.NoNewTrade.Should().Be(scenario.NoNewTrade);
        if (assertGoldenScores && scenario.VolatilityScore is { } volatilityScore)
            result.Volatility.Score.Should().Be(volatilityScore);

        result.MarketStructure.IsComplete.Should().BeTrue();
        result.MarketStructure.Classification.Should().Be(scenario.StructureClassification);
        result.MarketStructure.Direction.Should().Be(scenario.StructureDirection);
        result.MarketStructure.Breakout.Should().Be(scenario.Breakout);
        if (assertGoldenScores && scenario.StructureScore is { } structureScore)
            result.MarketStructure.Score.Should().Be(structureScore);

        result.Decision.IsComplete.Should().BeTrue();
        result.Decision.Direction.Should().Be(scenario.FusionDirection);
        if (assertGoldenScores && scenario.FusionScore is { } fusionScore)
            result.Decision.DirectionalScore.Should().Be(fusionScore);
        if (assertGoldenScores && scenario.Conviction is { } conviction)
            result.Decision.RiskAdjustedConviction.Should().Be(conviction);
        result.Decision.Restrictions.Should().Equal(scenario.Restrictions.Order());
        result.Reasons.Select(reason => reason.Code).Should().Contain(scenario.RequiredReasonCodes);

        if (assertRuntimeConfidence && scenario == RegimeDiscoveryScenarioCatalog.TrendingUp)
        {
            result.Decision.ConfidenceBand.Should().Be(RegimeConfidenceBand.VeryHigh);
            result.OverallQuality.Should().Be(RegimeOverallQuality.High);
            result.OverallConfidence.Should().BeGreaterThan(0.90m);
        }
    }
}
