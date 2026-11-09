using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Tests.Support;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

public sealed class MarketAssessmentBehaviorTests
{
    /// <summary>Every horizon consumes a completed minute without treating it as a stale tick.</summary>
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Given_a_completed_minute_atr_when_a_signal_arrives_54_seconds_later_then_stress_is_known(TimeFrameType horizon)
    {
        var sample = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model.MarketConditionAssessmentReferenceGenerator.CreateScenario(horizon, "directional");
        var snapshot = sample.Snapshot with { Observations = sample.Snapshot.Observations.Select(x => x.SourceId == "NormalizedMovement"
            ? x with { ObservedAtUtc = sample.Snapshot.EvaluatedAtUtc.AddSeconds(-54), ReceivedAtUtc = sample.Snapshot.EvaluatedAtUtc.AddSeconds(-54), Value = 0.5m } : x).ToArray() };
        var result = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model.MarketConditionAssessmentCalculator()
            .Calculate(sample.Command, snapshot.Seal(), sample.Command.CommandId);
        result.Assessment.LimitationReasons.Should().NotContain("MC.ASSESSMENT.NORMALIZEDMOVEMENT.STALE");
        result.Assessment.StressState.Should().NotBe(AssessmentStress.Unknown);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Given_one_accepted_timeframe_when_liquidity_is_poor_then_market_is_described_and_selector_owns_suitability(TimeFrameType horizon)
    {
        var f = new MarketAssessmentTestScenario(horizon, "poor");
        f.Result.TargetHorizon.Should().Be(horizon); f.Result.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        f.Result.Assessment.LiquidityCondition.Should().Be(AssessmentLiquidity.Poor);
        var selected = new MarketAssessmentSelectionConsumer().Select(f.Selection, f.Mandate, MarketAssessmentSelectionConsumer.MandateHash(f.Mandate), [], f.Result.EvaluatedAtUtc);
        selected.Reason.Should().Be("SELECTOR.NO_SUITABLE_STRATEGY");
    }
    [Fact]
    public void Given_inherited_no_new_trade_when_selector_receives_a_result_then_the_restriction_cannot_be_bypassed()
    {
        var f = new MarketAssessmentTestScenario(scenario: "restricted");
        FluentActions.Invoking(() => MarketConditionAssessmentContracts.ValidateForSelection(f.Selection, f.Result.EvaluatedAtUtc)).Should().Throw<ArgumentException>();
    }
}
