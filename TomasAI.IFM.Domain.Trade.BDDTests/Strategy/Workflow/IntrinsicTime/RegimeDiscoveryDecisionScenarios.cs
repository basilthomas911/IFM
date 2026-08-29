using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.BDDTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Executable RD20-25 market-language scenarios for the final Regime Discovery decision.</summary>
public sealed class RegimeDiscoveryDecisionScenarios
{
    [Fact]
    public void Given_an_established_aligned_bull_market_when_fused_then_the_decision_is_actionable_and_bullish()
    {
        var decision = Decide(
            RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable,
            MarketStructureClassification.Trending, RegimeDirection.Up);

        decision.IsComplete.Should().BeTrue();
        decision.Direction.Should().Be(RegimeDirection.Up);
        decision.TrendPhase.Should().Be(TrendRegimePhase.Established);
        decision.StructureClassification.Should().Be(MarketStructureClassification.Trending);
        decision.Restrictions.Should().BeEmpty();
    }

    [Fact]
    public void Given_an_exhausting_trend_and_expanding_volatility_when_fused_then_transition_risk_reduces_conviction()
    {
        var established = Decide(
            RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.High, VolatilityRegimeChange.Stable,
            MarketStructureClassification.Expanding, RegimeDirection.Up);
        var exhausting = Decide(
            RegimeDirection.Up, TrendRegimePhase.Exhausting,
            VolatilityRegimeLevel.High, VolatilityRegimeChange.Expanding,
            MarketStructureClassification.Expanding, RegimeDirection.Up);

        exhausting.Direction.Should().Be(RegimeDirection.Up);
        exhausting.Restrictions.Should().Contain(RegimeRestriction.Transition);
        exhausting.RiskAdjustedConviction.Should().BeLessThan(established.RiskAdjustedConviction);
    }

    [Fact]
    public void Given_extreme_volatility_when_fused_then_new_trades_are_blocked_without_erasing_direction()
    {
        var decision = Decide(
            RegimeDirection.Down, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Extreme, VolatilityRegimeChange.Expanding,
            MarketStructureClassification.Trending, RegimeDirection.Down,
            noNewTrade: true);

        decision.Direction.Should().Be(RegimeDirection.Down);
        decision.VolatilityLevel.Should().Be(VolatilityRegimeLevel.Extreme);
        decision.Restrictions.Should().Contain(RegimeRestriction.NoNewTrade);
    }

    [Fact]
    public void Given_opposing_trend_and_structure_when_fused_then_the_conflict_is_explicit()
    {
        var decision = Decide(
            RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable,
            MarketStructureClassification.Trending, RegimeDirection.Down);

        decision.Restrictions.Should().Contain(RegimeRestriction.DirectionConflict);
        decision.Direction.Should().Be(RegimeDirection.Up);
    }

    static RegimeDiscoveryDecision Decide(
        RegimeDirection trendDirection,
        TrendRegimePhase trendPhase,
        VolatilityRegimeLevel volatilityLevel,
        VolatilityRegimeChange volatilityChange,
        MarketStructureClassification structure,
        RegimeDirection structureDirection,
        bool noNewTrade = false)
    {
        var trendScore = trendDirection == RegimeDirection.Down ? -0.8m : 0.8m;
        var structureScore = structureDirection == RegimeDirection.Down ? -0.8m : 0.8m;
        return new MarketRegimeFusionModel().Calculate(
            new TrendRegimeResult
            {
                IsComplete = true,
                Direction = trendDirection,
                Strength = TrendRegimeStrength.Strong,
                Phase = trendPhase,
                Score = trendScore,
                Confidence = 0.85m,
                TimeFrameAgreement = 0.85m
            },
            new VolatilityRegimeResult
            {
                IsComplete = true,
                Level = volatilityLevel,
                Change = volatilityChange,
                TermStructure = volatilityLevel >= VolatilityRegimeLevel.High
                    ? VxTermStructureRegime.Backwardation
                    : VxTermStructureRegime.Contango,
                Score = volatilityLevel == VolatilityRegimeLevel.Extreme ? 0.85m : 0.4m,
                Confidence = 0.85m,
                NoNewTrade = noNewTrade
            },
            new MarketStructureRegimeResult
            {
                IsComplete = true,
                Classification = structure,
                Direction = structureDirection,
                Score = structureScore,
                Confidence = 0.85m
            },
            new MarketRegimeFusionConfiguration());
    }
}
