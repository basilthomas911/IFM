using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
namespace TomasAI.IFM.Domain.Trade.VerificationTests;
[Trait("Category","Verification")]
public sealed class TradeSelectionQualificationTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public async Task Source_to_selected_intent_to_Portfolio_reservation_retains_all_exact_identity_and_hash_evidence(TimeFrameType horizon)
    {
        var source=await TradeSelectionFixture.Command("LongBalancedIronCondor",horizon);
        var received=MessagePackSerializer.Deserialize<ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(source));
        source.Fingerprint().Should().Be(received.Fingerprint());
        var result=TradeSelectionEvaluator.Evaluate(received);var envelope=TradeSelectionTestInputs.Envelope(result);TradeSelectionContracts.ReadResult(envelope);
        var pending=TradeSelectionHandoff.Pending(result,envelope,received.InputWorkflowRevision+1,result.ResultId,result.EvaluatedAtUtc.AddMilliseconds(1));
        var aggregate=new PortfolioFundCompositionAggregate();var reserved=aggregate.Reserve(pending.Request,received.SelectionBinding.PortfolioSnapshot,101,[201],pending.UpdatedAtUtc,"verification");
        TradeSelectionHandoff.ValidateReservation(pending,reserved);
        reserved.Order.TradeTemplateId.Should().Be(result.SelectedCandidate!.DeploymentKey.Id);reserved.Order.StrategySnapshotHash.Should().Be(received.SelectionBinding.PortfolioSnapshot.PayloadSha256);
        reserved.Trades.Should().ContainSingle();reserved.Trades[0].InstructionReference.Should().Be(result.SelectedCandidate.CandidateHash);
        var restart=new PortfolioFundCompositionAggregate();restart.Restore(MessagePackSerializer.Deserialize<FundCompositionReservationResult[]>(MessagePackSerializer.Serialize(aggregate.CaptureState().ToArray())));
        restart.Reserve(pending.Request,received.SelectionBinding.PortfolioSnapshot,999,[999],result.ValidUntilUtc.AddHours(1),"verification").Order.OrderId.Should().Be(101);
    }
}
