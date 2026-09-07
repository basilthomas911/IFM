using FluentAssertions;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
namespace TomasAI.IFM.Domain.Trade.BDDTests;
[Trait("Category","BDD")]
public sealed class TradeSelectionBehaviorTests
{
    [Theory]
    [InlineData("LongFuture","Long","Bullish","None")]
    [InlineData("ShortFuture","Short","Bearish","None")]
    [InlineData("BullCallDebit","Long","Bullish","Debit")]
    [InlineData("BearCallCredit","Short","Bearish","Credit")]
    [InlineData("ShortBalancedIronCondor","Short","Balanced","Credit")]
    [InlineData("LongBalancedIronCondor","Long","Balanced","Debit")]
    public async Task Given_an_authorized_variant_and_compatible_market_when_selected_then_intent_preserves_side_bias_and_premium(string variant,string side,string bias,string premium)
    {
        var given=await TradeSelectionFixture.Command(variant);
        var when=TradeSelectionEvaluator.Evaluate(given);
        when.Outcome.Should().Be(SelectionOutcome.Selected);when.SelectedCandidate!.Side.Should().Be(side);when.SelectedCandidate.Bias.Should().Be(bias);when.SelectedCandidate.PremiumMode.Should().Be(premium);
    }
    [Fact]
    public async Task Given_no_Fund_permissions_when_evaluated_then_NoTrade_with_no_synthesized_default()
    {
        var given=await TradeSelectionFixture.Command();var authority=given.SelectionBinding.PortfolioSnapshot;
        given=TradeSelectionTestInputs.Authority(given,authority with {Fund=authority.Fund with {PermittedTradeStrategyFamilies=[]}});
        var when=TradeSelectionEvaluator.Evaluate(given);when.Outcome.Should().Be(SelectionOutcome.NoTrade);when.PrimaryReasonCode.Should().Be("TS.NO_AUTHORIZED_CANDIDATE");
    }
    [Fact]
    public async Task Given_neutral_regime_when_only_futures_are_authorized_then_no_directional_future_is_selected()
    {
        var given=await TradeSelectionFixture.Command();given=TradeSelectionTestInputs.Evidence(given,regimeChange:x=>TradeSelectionTestInputs.Set(x,"Direction",RegimeDirection.Neutral));
        var when=TradeSelectionEvaluator.Evaluate(given);when.Outcome.Should().Be(SelectionOutcome.NoTrade);when.CandidateDecisions.Single().ReasonCodes.Should().Contain("TS.PERMISSION.DIRECTION");
    }
}
