using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Framework.Serialization;
using Composer = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.VerificationTests;

[Trait("Category", "Verification")]
public sealed class OrderCompositionQualificationTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public async Task Accepted_selection_reservation_snapshot_and_candidate_remain_identical_across_transport_and_acceptance(TimeFrameType horizon)
    {
        var c = await CompositionFixture.Command("LongBullishIronCondor", horizon);
        var wire = MessagePackBinarySerializer.Shared.Serialize(c);
        var received = MessagePackBinarySerializer.Shared.Deserialize<ExecuteOrderCompositionPipelineCommand>(wire);
        Assert.Equal(c.InputSha256, received.Fingerprint());
        var f = new CompositionFunctionFixture(received);
        var reply = await f.Execute(); Assert.True(reply.IsCompleted, reply.Failed?.ErrorMessage);
        var result = reply.Completed!.Result.ReadCompositionResult();
        var command = CompositionFixture.Completion(received, result);
        var accepted = CompositionAcceptance.Validate(received, command, new Composer(new Black76ComposerPricer()), c.EvaluatedAtUtc);
        Assert.Equal(c.Reservation.Order.OrderId, accepted.Candidate!.OrderId);
        Assert.Equal(c.Reservation.Trades.Single().TradeId, accepted.Candidate.PrimaryTradeId);
        Assert.Equal(c.CompositionBinding.Selected.VariantKey, accepted.Candidate.VariantKey);
        Assert.Equal(c.MarketSnapshot.Digest, accepted.Candidate.SnapshotHash);
        Assert.Equal(c.CompositionBinding.BindingSha256, accepted.Candidate.BindingHash);
    }
}
