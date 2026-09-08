using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.BDDTests;

[Trait("Category", "BDD")]
public sealed class OrderCompositionBehaviorTests
{
    [Theory]
    [InlineData("LongBullishIronCondor", "Long", "Bullish", "Debit")]
    [InlineData("ShortBearishIronCondor", "Short", "Bearish", "Credit")]
    [InlineData("BullCallDebit", "Long", "Bullish", "Debit")]
    [InlineData("BearPutDebit", "Long", "Bearish", "Debit")]
    public async Task Given_exact_Weekly_selected_intent_when_Function_executes_then_the_one_unit_retains_its_meaning(string variant, string side, string bias, string premium)
    {
        var c = await CompositionFixture.Command(variant, TimeFrameType.Weekly);
        var f = new CompositionFunctionFixture(c);
        var reply = await f.Execute();
        Assert.True(reply.IsCompleted, reply.Failed?.ErrorMessage);
        var result = reply.Completed!.Result.ReadCompositionResult();
        Assert.Equal(CompositionOutcome.Composed, result.Outcome);
        Assert.Equal(side, result.Candidate!.Side); Assert.Equal(bias, result.Candidate.Bias); Assert.Equal(premium, result.Candidate.PremiumMode);
        Assert.Equal(1, result.Candidate.UnitQuantity); Assert.Equal("Unapproved", result.Candidate.ApprovalState);
    }
    [Fact]
    public async Task Given_a_lost_completed_reply_when_retried_after_expiry_then_the_original_result_returns_without_repricing()
    {
        var f = new CompositionFunctionFixture(await CompositionFixture.Command());
        var first = await f.Execute(); f.Clock.Now = f.Command.ExpiresAtUtc.AddDays(1);
        var replay = await f.Execute(); Assert.Same(first.Completed, replay.Completed);
        Assert.Equal(new[] { "load", "project", "persist", "load" }, f.Order);
    }
}
