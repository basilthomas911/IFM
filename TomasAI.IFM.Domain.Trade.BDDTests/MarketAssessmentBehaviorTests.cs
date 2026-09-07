using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Tests.Support;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

public sealed class MarketAssessmentBehaviorTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)][InlineData(TimeFrameType.Weekly)][InlineData(TimeFrameType.Monthly)]
    public void Given_one_accepted_timeframe_when_liquidity_is_poor_then_market_is_described_and_selector_owns_suitability(TimeFrameType horizon)
    {
        var f=new MarketAssessmentTestScenario(horizon,"poor");
        f.Result.TargetHorizon.Should().Be(horizon); f.Result.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        f.Result.Assessment.LiquidityCondition.Should().Be(AssessmentLiquidity.Poor);
        var selected=new MarketAssessmentSelectionConsumer().Select(f.Selection,f.Mandate,MarketAssessmentSelectionConsumer.MandateHash(f.Mandate),[],f.Result.EvaluatedAtUtc);
        selected.Reason.Should().Be("SELECTOR.NO_SUITABLE_STRATEGY");
    }
    [Fact]
    public void Given_inherited_no_new_trade_when_selector_receives_a_result_then_the_restriction_cannot_be_bypassed()
    {
        var f=new MarketAssessmentTestScenario(scenario:"restricted");
        FluentActions.Invoking(()=>MarketConditionAssessmentContracts.ValidateForSelection(f.Selection,f.Result.EvaluatedAtUtc)).Should().Throw<ArgumentException>();
    }
}
