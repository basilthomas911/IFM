using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.MarketCondition;

/// <summary>Pairwise qualification of the minimum reasonable Market Condition language and hint combinations.</summary>
[Trait("Category", "Verification")]
public sealed class MarketConditionDecisionCombinationVerificationTests
{
    [Fact]
    public void Generated_reference_catalog_preserves_all_minimum_reasonable_decision_and_hint_combinations()
    {
        var generated = new MarketConditionDecisionReferenceGenerator().Generate();

        generated.Should().HaveCount(12);
        generated.Select(value => value.Name).Should().Equal(
            MinimumDecisionCombinations.Cast<DecisionCase>().Select(value => value.Name));
        generated.Should().OnlyContain(value => !value.IsAuthoritative && !value.IsCompleteEnumeration &&
            value.CoverageKind == "RepresentativePairwise" && value.HintIsAdvisory &&
            value.HintSuitability != MarketConditionHintSuitability.Unknown);
    }

    public static TheoryData<DecisionCase> MinimumDecisionCombinations => new()
    {
        Case("Daily established bullish", TimeFrameType.Daily, RegimeDirection.Up,
            TrendRegimePhase.Established, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.Directional, MarketConditionDirection.Bullish, MarketConditionTradeType.Futures,
            MarketConditionHintSuitability.Preferred),
        Case("Daily established bearish", TimeFrameType.Daily, RegimeDirection.Down,
            TrendRegimePhase.Established, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.Directional, MarketConditionDirection.Bearish, MarketConditionTradeType.Futures,
            MarketConditionHintSuitability.Preferred),
        Case("Daily bullish breakout", TimeFrameType.Daily, RegimeDirection.Up,
            TrendRegimePhase.Emerging, VolatilityRegimeChange.Stable, MarketStructureClassification.BreakingOut,
            MarketConditionType.Directional, MarketConditionDirection.Bullish, MarketConditionTradeType.Futures,
            MarketConditionHintSuitability.Preferred, MarketBreakoutState.Up),
        Case("Weekly emerging bullish", TimeFrameType.Weekly, RegimeDirection.Up,
            TrendRegimePhase.Emerging, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.Directional, MarketConditionDirection.Bullish, MarketConditionTradeType.VerticalSpread,
            MarketConditionHintSuitability.Preferred),
        Case("Weekly volatility expansion", TimeFrameType.Weekly, RegimeDirection.Up,
            TrendRegimePhase.Established, VolatilityRegimeChange.Expanding, MarketStructureClassification.Expanding,
            MarketConditionType.VolatilityExpansion, MarketConditionDirection.Bullish,
            MarketConditionTradeType.VerticalSpread, MarketConditionHintSuitability.Preferred),
        Case("Weekly reversal transition", TimeFrameType.Weekly, RegimeDirection.Up,
            TrendRegimePhase.Reversing, VolatilityRegimeChange.Expanding, MarketStructureClassification.Transitioning,
            MarketConditionType.Transition, MarketConditionDirection.Bullish, MarketConditionTradeType.VerticalSpread,
            MarketConditionHintSuitability.Eligible),
        Case("Monthly stable range", TimeFrameType.Monthly, RegimeDirection.Neutral,
            TrendRegimePhase.RangeBound, VolatilityRegimeChange.Stable, MarketStructureClassification.Ranging,
            MarketConditionType.RangeBound, MarketConditionDirection.Neutral, MarketConditionTradeType.IronCondor,
            MarketConditionHintSuitability.Preferred),
        Case("Monthly compression", TimeFrameType.Monthly, RegimeDirection.Neutral,
            TrendRegimePhase.RangeBound, VolatilityRegimeChange.Contracting, MarketStructureClassification.Compressing,
            MarketConditionType.VolatilityContraction, MarketConditionDirection.Neutral,
            MarketConditionTradeType.IronCondor, MarketConditionHintSuitability.Preferred),
        Case("Monthly directional market", TimeFrameType.Monthly, RegimeDirection.Up,
            TrendRegimePhase.Established, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.Directional, MarketConditionDirection.Bullish, MarketConditionTradeType.IronCondor,
            MarketConditionHintSuitability.Eligible),
        Case("Daily trigger conflict", TimeFrameType.Daily, RegimeDirection.Down,
            TrendRegimePhase.Established, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.NoOpportunity, MarketConditionDirection.Bearish, MarketConditionTradeType.Futures,
            MarketConditionHintSuitability.Avoid, triggerConflict: true),
        Case("Weekly no-new-trade", TimeFrameType.Weekly, RegimeDirection.Up,
            TrendRegimePhase.Established, VolatilityRegimeChange.Expanding, MarketStructureClassification.Expanding,
            MarketConditionType.NoOpportunity, MarketConditionDirection.Bullish, MarketConditionTradeType.VerticalSpread,
            MarketConditionHintSuitability.Avoid, noNewTrade: true),
        Case("Daily option-quality blocker", TimeFrameType.Daily, RegimeDirection.Up,
            TrendRegimePhase.Established, VolatilityRegimeChange.Stable, MarketStructureClassification.Trending,
            MarketConditionType.NoOpportunity, MarketConditionDirection.Bullish, MarketConditionTradeType.Futures,
            MarketConditionHintSuitability.Avoid, optionBlocker: true)
    };

    [Theory]
    [MemberData(nameof(MinimumDecisionCombinations))]
    public void Minimum_reasonable_decision_combination_is_complete_explainable_and_hint_safe(DecisionCase value)
    {
        var input = value.OptionBlocker
            ? MarketConditionVerificationScenario.Blocked("options")
            : MarketConditionVerificationScenario.Healthy(value.Horizon);
        var decision = input.RegimeResult.Decision with
        {
            Direction = value.Direction,
            DirectionalScore = value.Direction switch { RegimeDirection.Up => 0.8m, RegimeDirection.Down => -0.8m, _ => 0m },
            RiskAdjustedConviction = value.Direction == RegimeDirection.Neutral ? 0m : 0.75m,
            Confidence = 0.90m,
            TrendPhase = value.Phase,
            TrendStrength = value.Direction == RegimeDirection.Neutral ? TrendRegimeStrength.None : TrendRegimeStrength.Strong,
            TrendTimeFrameAgreement = 0.85m,
            VolatilityLevel = value.NoNewTrade ? VolatilityRegimeLevel.Extreme : VolatilityRegimeLevel.Normal,
            VolatilityChange = value.Volatility,
            TermStructure = value.Volatility == VolatilityRegimeChange.Expanding
                ? VxTermStructureRegime.Backwardation : VxTermStructureRegime.Contango,
            StructureClassification = value.Structure,
            Breakout = value.Breakout,
            Restrictions = value.NoNewTrade ? [RegimeRestriction.NoNewTrade] :
                value.Phase == TrendRegimePhase.Reversing ? [RegimeRestriction.Transition] : []
        };
        input = input with { RegimeResult = input.RegimeResult with { Decision = decision } };
        if (value.Direction == RegimeDirection.Down && !value.TriggerConflict)
            input = input with { TriggerEvent = input.TriggerEvent with { FuturesItiSignal =
                input.TriggerEvent.FuturesItiSignal! with { IntrinsicTimeTrend = IntrinsicTimeTrendType.DownTrend } } };

        var result = new MarketConditionCalculationModel().Calculate(input);

        result.ConditionType.Should().Be(value.ExpectedCondition, value.Name);
        result.Direction.Should().Be(value.ExpectedDirection, value.Name);
        result.OutputHints.Should().ContainSingle();
        result.OutputHints[0].TradeType.Should().Be(value.ExpectedTradeType);
        result.OutputHints[0].TimeFrame.Should().Be(value.Horizon);
        result.OutputHints[0].Suitability.Should().Be(value.ExpectedSuitability);
        result.OutputHints[0].IsAdvisory.Should().BeTrue();
        result.EvidenceItems.Should().Contain(x => x.FeatureCode == "RD.TrendPhase");
        result.EvidenceItems.Should().Contain(x => x.FeatureCode == "RD.VolatilityChange");
        result.EvidenceItems.Should().Contain(x => x.FeatureCode == "RD.Structure");
    }

    static DecisionCase Case(string name, TimeFrameType horizon, RegimeDirection direction, TrendRegimePhase phase,
        VolatilityRegimeChange volatility, MarketStructureClassification structure,
        MarketConditionType expectedCondition, MarketConditionDirection expectedDirection,
        MarketConditionTradeType expectedTradeType, MarketConditionHintSuitability expectedSuitability,
        MarketBreakoutState breakout = MarketBreakoutState.None, bool triggerConflict = false,
        bool noNewTrade = false, bool optionBlocker = false) => new(name, horizon, direction, phase, volatility,
        structure, expectedCondition, expectedDirection, expectedTradeType, expectedSuitability, breakout,
        triggerConflict, noNewTrade, optionBlocker);

    public sealed record DecisionCase(string Name, TimeFrameType Horizon, RegimeDirection Direction,
        TrendRegimePhase Phase, VolatilityRegimeChange Volatility, MarketStructureClassification Structure,
        MarketConditionType ExpectedCondition, MarketConditionDirection ExpectedDirection,
        MarketConditionTradeType ExpectedTradeType, MarketConditionHintSuitability ExpectedSuitability,
        MarketBreakoutState Breakout, bool TriggerConflict, bool NoNewTrade, bool OptionBlocker)
    {
        public override string ToString() => Name;
    }
}
