using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
using Composer = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CompositionCalculationTests
{
    public static IEnumerable<object[]> Matrix => from variant in CompositionFixture.Variants
        from horizon in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly } select new object[] { variant, horizon };

    [Theory, MemberData(nameof(Matrix)), Trait("Gate", "OC-04")]
    public async Task Every_selected_variant_constructs_one_unit_on_each_triggering_horizon(string variant, TimeFrameType horizon)
    {
        var c = await CompositionFixture.Command(variant, horizon);
        var result = new Composer(new Black76ComposerPricer()).Calculate(c);
        Assert.True(result.Outcome == CompositionOutcome.Composed, string.Join(",", result.CandidateDiagnostics.Select(x => $"{x.ReasonCode}={x.Count}")));
        var candidate = result.Candidate!;
        Assert.Equal(c.CompositionBinding.Selected.VariantKey, candidate.VariantKey);
        Assert.Equal(horizon, candidate.TargetHorizon); Assert.Equal(1, candidate.UnitQuantity);
        Assert.Equal(variant.Contains("Future") ? 1 : variant.Contains("Condor") ? 4 : 2, candidate.Legs.Length);
        Assert.All(candidate.Legs, x => Assert.Equal(1, x.Ratio));
        Assert.Equal("Unapproved", candidate.ApprovalState);
        Assert.Equal(7001, candidate.OrderId); Assert.Equal(8001, candidate.PrimaryTradeId);
        if (candidate.PremiumMode == "Credit") Assert.True(candidate.Pricing.LimitDebit < 0);
        if (candidate.PremiumMode == "Debit") Assert.True(candidate.Pricing.LimitDebit > 0);
        Assert.Equal(CompositionHash.Candidate(candidate), candidate.CandidateHash);
    }
    [Theory]
    [InlineData(2, 1, 105, 395)] [InlineData(-2, -1, 405, 95)]
    public void Piecewise_payoff_matches_vertical_closed_forms(decimal premium, int side, decimal loss, decimal profit)
    {
        var result = Composer.Payoff([(100m, true, side), (110m, true, -side)], premium, 50, 5);
        Assert.Equal(loss, result.MaximumLoss); Assert.Equal(profit, result.MaximumProfit);
    }
    [Fact]
    public void Unequal_wings_use_larger_tail_and_costs_once()
    {
        var shortCondor = Composer.Payoff([(90, false, 1), (100, false, -1), (110, true, -1), (130, true, 1)], -3, 50, 10);
        Assert.Equal(860m, shortCondor.MaximumLoss); Assert.Equal(140m, shortCondor.MaximumProfit);
        var longCondor = Composer.Payoff([(90, false, -1), (100, false, 1), (110, true, 1), (130, true, -1)], 3, 50, 10);
        Assert.Equal(160m, longCondor.MaximumLoss); Assert.Equal(840m, longCondor.MaximumProfit);
    }
    [Fact]
    public async Task Complete_but_empty_scope_is_NoCandidate_and_missing_quotes_are_failures()
    {
        var c = await CompositionFixture.Command(); var model = new Composer(new Black76ComposerPricer());
        var result = model.Calculate(c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [] } });
        Assert.Equal(CompositionOutcome.NoCandidate, result.Outcome); Assert.Null(result.Candidate);
        var item = c.MarketSnapshot.Instruments[0];
        c = c with { MarketSnapshot = c.MarketSnapshot with { Instruments = [item with { Instrument = item.Instrument with { Quote = item.Instrument.Quote with { Ask = 0 } } }] } };
        Assert.Throws<CompositionException>(() => model.Calculate(c));
    }
    [Fact]
    public async Task Typed_envelope_roundtrips_without_nested_MessagePack_and_rejects_tampering()
    {
        var c = await CompositionFixture.Command(); var r = new Composer(new Black76ComposerPricer()).Calculate(c);
        var e = Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope.CreateComposition(r);
        var restored = MessagePackBinarySerializer.Shared.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope>(MessagePackBinarySerializer.Shared.Serialize(e));
        Assert.True(restored.Payload.IsEmpty); Assert.True(restored.HasValidPayloadSha256());
        Assert.Equal(r.Candidate!.CandidateHash, restored.ReadCompositionResult().Candidate!.CandidateHash);
        Assert.False((restored with { CompositionResult = r with { SummaryText = "changed" } }).HasValidPayloadSha256());
    }
    [Fact]
    public async Task Reordered_complete_chain_retains_selected_economics_and_candidate_hash()
    {
        var c = await CompositionFixture.Command("ShortBalancedIronCondor"); var model = new Composer(new Black76ComposerPricer());
        var before = model.Calculate(c); var after = model.Calculate(c with { MarketSnapshot = c.MarketSnapshot with { Instruments = c.MarketSnapshot.Instruments.Reverse().ToImmutableArray() } });
        Assert.Equal(before.Candidate!.CandidateHash, after.Candidate!.CandidateHash); Assert.Equal(before.CandidateCounts, after.CandidateCounts);
    }
}
