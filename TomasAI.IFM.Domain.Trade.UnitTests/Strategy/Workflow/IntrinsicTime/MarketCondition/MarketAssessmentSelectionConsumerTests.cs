using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Tests.Support;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

[Trait("Gate","MC-R06")]
public sealed class MarketAssessmentSelectionConsumerTests
{
    [Theory]
    [InlineData("directional")]
    [InlineData("poor")]
    public void Descriptive_valid_market_reaches_independent_exact_family_version_selection(string scenario)
    {
        var f=new MarketAssessmentTestScenario(scenario:scenario); var allowed=new AssessmentSelectionCandidate(Guid.NewGuid(),"Futures","Futures",new(1,2));
        var denied=allowed with { StrategyId=Guid.NewGuid(),StrategyFamily=new(1,1) };
        var r=new MarketAssessmentSelectionConsumer().Select(f.Selection,f.Mandate,MarketAssessmentSelectionConsumer.MandateHash(f.Mandate),[allowed,denied],f.Result.EvaluatedAtUtc);
        r.Candidates.Should().Equal(allowed); r.FundMandateVersion.Should().Be(7);
    }
    [Theory]
    [InlineData("horizon")][InlineData("condition")][InlineData("empty")]
    public void Selector_can_return_no_strategy_for_its_own_mandate_or_candidate_rules(string change)
    {
        var f=new MarketAssessmentTestScenario(); var mandate=change=="horizon"?f.Mandate with {DecisionHorizon="Monthly"}:change=="condition"?f.Mandate with {PermittedConditions=["RangeBound"]}:f.Mandate;
        var r=new MarketAssessmentSelectionConsumer().Select(f.Selection,mandate,MarketAssessmentSelectionConsumer.MandateHash(mandate),[],f.Result.EvaluatedAtUtc);
        r.NoStrategy.Should().BeTrue(); r.Reason.Should().StartWith("SELECTOR.");
    }
    [Theory]
    [InlineData("expired")][InlineData("restricted")][InlineData("unavailable")][InlineData("mandate-hash")][InlineData("fund")]
    public void Stale_or_unauthorized_consumer_inputs_are_rejected(string change)
    {
        var f=new MarketAssessmentTestScenario(scenario:change is "restricted" or "unavailable"?change:"directional");
        var mandate=change=="fund"?f.Mandate with {FundId=9999}:f.Mandate;
        var hash=change=="mandate-hash"?new string('0',64):MarketAssessmentSelectionConsumer.MandateHash(mandate);
        FluentActions.Invoking(()=>new MarketAssessmentSelectionConsumer().Select(f.Selection,mandate,hash,[],change=="expired"?f.Result.Assessment.ValidUntilUtc!.Value:f.Result.EvaluatedAtUtc)).Should().Throw<ArgumentException>();
    }
}
