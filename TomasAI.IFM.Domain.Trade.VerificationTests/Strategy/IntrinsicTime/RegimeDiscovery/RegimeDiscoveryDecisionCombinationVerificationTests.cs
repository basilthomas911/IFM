using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

/// <summary>Verifies the minimum reasonable pairwise decision combinations across all specialist dimensions.</summary>
[Trait("Category", "Verification")]
public sealed class RegimeDiscoveryDecisionCombinationVerificationTests
{
    public static TheoryData<DecisionCase> MinimumDecisionCombinations => new()
    {
        Case("Established bullish trend", RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.Trending, RegimeDirection.Up, RegimeDirection.Up),
        Case("Established bearish trend", RegimeDirection.Down, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.Trending, RegimeDirection.Down, RegimeDirection.Down),
        Case("Quiet range contraction", RegimeDirection.Neutral, TrendRegimePhase.RangeBound,
            VolatilityRegimeLevel.Low, VolatilityRegimeChange.Contracting, VxTermStructureRegime.Contango,
            MarketStructureClassification.Ranging, RegimeDirection.Neutral, RegimeDirection.Neutral),
        Case("Emerging bullish structure", RegimeDirection.Up, TrendRegimePhase.Emerging,
            VolatilityRegimeLevel.Low, VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Trending, RegimeDirection.Up, RegimeDirection.Up),
        Case("Exhausting trend with expansion", RegimeDirection.Up, TrendRegimePhase.Exhausting,
            VolatilityRegimeLevel.High, VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Expanding, RegimeDirection.Up, RegimeDirection.Up,
            RegimeRestriction.Transition),
        Case("Bearish reversal with expansion", RegimeDirection.Down, TrendRegimePhase.Reversing,
            VolatilityRegimeLevel.High, VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Trending, RegimeDirection.Down, RegimeDirection.Down,
            RegimeRestriction.Transition),
        Case("Directional specialist conflict", RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Trending, RegimeDirection.Down, RegimeDirection.Up,
            RegimeRestriction.DirectionConflict),
        Case("Neutral trend with bullish breakout", RegimeDirection.Neutral, TrendRegimePhase.RangeBound,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Contango,
            MarketStructureClassification.BreakingOut, RegimeDirection.Up, RegimeDirection.Up),
        Case("Directional transition", RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Transitioning, RegimeDirection.Neutral, RegimeDirection.Up,
            RegimeRestriction.Transition),
        Case("Extreme volatility blocker", RegimeDirection.Up, TrendRegimePhase.Established,
            VolatilityRegimeLevel.Extreme, VolatilityRegimeChange.Expanding, VxTermStructureRegime.Backwardation,
            MarketStructureClassification.Expanding, RegimeDirection.Up, RegimeDirection.Up,
            RegimeRestriction.NoNewTrade),
        Case("Low-confidence mixed market", RegimeDirection.Up, TrendRegimePhase.Emerging,
            VolatilityRegimeLevel.Normal, VolatilityRegimeChange.Stable, VxTermStructureRegime.Flat,
            MarketStructureClassification.Ranging, RegimeDirection.Neutral, RegimeDirection.Up,
            RegimeRestriction.LowConfidence),
        Case("Neutral compression", RegimeDirection.Neutral, TrendRegimePhase.RangeBound,
            VolatilityRegimeLevel.Low, VolatilityRegimeChange.Contracting, VxTermStructureRegime.Contango,
            MarketStructureClassification.Compressing, RegimeDirection.Neutral, RegimeDirection.Neutral)
    };

    [Theory]
    [MemberData(nameof(MinimumDecisionCombinations))]
    public void Minimum_reasonable_decision_combination_is_complete_and_explainable(DecisionCase value)
    {
        var trendScore = value.TrendDirection switch
        {
            RegimeDirection.Up => 0.8m,
            RegimeDirection.Down => -0.8m,
            _ => 0m
        };
        var structureScore = value.StructureDirection switch
        {
            RegimeDirection.Up => 0.8m,
            RegimeDirection.Down => -0.8m,
            _ => 0m
        };
        var confidence = value.ExpectedRestrictions.Contains(RegimeRestriction.LowConfidence) ? 0.30m : 0.85m;
        var decision = new MarketRegimeFusionModel().Calculate(
            new TrendRegimeResult
            {
                IsComplete = true, Direction = value.TrendDirection, Phase = value.TrendPhase,
                Strength = trendScore == 0m ? TrendRegimeStrength.None : TrendRegimeStrength.Strong,
                Score = trendScore, Confidence = confidence, TimeFrameAgreement = confidence
            },
            new VolatilityRegimeResult
            {
                IsComplete = true, Level = value.VolatilityLevel, Change = value.VolatilityChange,
                TermStructure = value.TermStructure, Score = VolatilityScore(value.VolatilityLevel),
                Confidence = confidence, NoNewTrade = value.VolatilityLevel == VolatilityRegimeLevel.Extreme
            },
            new MarketStructureRegimeResult
            {
                IsComplete = true, Classification = value.StructureClassification,
                Direction = value.StructureDirection, Breakout = value.StructureClassification ==
                    MarketStructureClassification.BreakingOut ? MarketBreakoutState.Up : MarketBreakoutState.None,
                Score = structureScore, Confidence = confidence
            }, new MarketRegimeFusionConfiguration());

        decision.IsComplete.Should().BeTrue(value.Name);
        decision.Direction.Should().Be(value.ExpectedDirection, value.Name);
        decision.TrendPhase.Should().Be(value.TrendPhase);
        decision.VolatilityLevel.Should().Be(value.VolatilityLevel);
        decision.VolatilityChange.Should().Be(value.VolatilityChange);
        decision.TermStructure.Should().Be(value.TermStructure);
        decision.StructureClassification.Should().Be(value.StructureClassification);
        decision.Confidence.Should().BeInRange(0m, 1m);
        decision.RiskAdjustedConviction.Should().BeInRange(0m, Math.Abs(decision.DirectionalScore));
        if (value.ExpectedRestrictions.Length == 0)
            decision.Restrictions.Should().BeEmpty();
        else
            decision.Restrictions.Should().Contain(value.ExpectedRestrictions);
    }

    static DecisionCase Case(string name, RegimeDirection trendDirection, TrendRegimePhase trendPhase,
        VolatilityRegimeLevel volatilityLevel, VolatilityRegimeChange volatilityChange,
        VxTermStructureRegime termStructure, MarketStructureClassification structureClassification,
        RegimeDirection structureDirection, RegimeDirection expectedDirection,
        params RegimeRestriction[] restrictions) => new(name, trendDirection, trendPhase, volatilityLevel,
        volatilityChange, termStructure, structureClassification, structureDirection, expectedDirection, restrictions);

    static decimal VolatilityScore(VolatilityRegimeLevel level) => level switch
    {
        VolatilityRegimeLevel.Low => 0.15m,
        VolatilityRegimeLevel.Normal => 0.40m,
        VolatilityRegimeLevel.High => 0.65m,
        VolatilityRegimeLevel.Extreme => 0.85m,
        _ => 0m
    };

    public sealed record DecisionCase(string Name, RegimeDirection TrendDirection, TrendRegimePhase TrendPhase,
        VolatilityRegimeLevel VolatilityLevel, VolatilityRegimeChange VolatilityChange,
        VxTermStructureRegime TermStructure, MarketStructureClassification StructureClassification,
        RegimeDirection StructureDirection, RegimeDirection ExpectedDirection,
        RegimeRestriction[] ExpectedRestrictions)
    {
        public override string ToString() => Name;
    }
}
