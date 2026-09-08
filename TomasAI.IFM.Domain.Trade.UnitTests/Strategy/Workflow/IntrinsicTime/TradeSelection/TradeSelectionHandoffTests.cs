using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionTestInputs;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-06")]
public sealed class TradeSelectionHandoffTests
{
    [Theory]
    [InlineData("LongFuture")] [InlineData("BullCallDebit")] [InlineData("ShortBalancedIronCondor")] [InlineData("LongBearishIronCondor")]
    public async Task Selection_commits_pending_then_one_actual_Portfolio_order_and_primary_trade(string variant)
    {
        var f=await Fixture.Create(variant);f.Accept();var pending=f.State.CurrentView!.CompositionHandoff!;
        pending.Status.Should().Be(CompositionHandoffStatus.ReservationPending);f.State.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.TradeSelection);
        pending.Request.WorkflowRevision.Should().Be(f.Command.SelectionBinding.PortfolioSnapshot.WorkflowRevision);
        pending.Request.TradeTemplateId.Should().Be(f.Result.SelectedCandidate!.DeploymentKey.Id);
        pending.Request.TradeInstructions.Should().ContainSingle();
        var aggregate=new PortfolioFundCompositionAggregate();var reserved=aggregate.Reserve(pending.Request,f.Command.SelectionBinding.PortfolioSnapshot,7001,[8001],f.Clock.Now,"selector-test");
        TradeSelectionHandoff.ValidateReservation(pending,reserved);
        var restart=new PortfolioFundCompositionAggregate();restart.Restore(aggregate.CaptureState());
        var replay=restart.Reserve(pending.Request,f.Command.SelectionBinding.PortfolioSnapshot,7999,[8999],f.Clock.Now,"selector-test");
        replay.Disposition.Should().Be(ReservationDisposition.IdempotentReplay);replay.Order.OrderId.Should().Be(7001);replay.Trades.Single().TradeId.Should().Be(8001);
        f.Reserve(replay);f.State.CurrentView!.CurrentStage.Should().Be(StrategyWorkflowStage.OrderComposition);f.State.CurrentView.CompositionHandoff!.Status.Should().Be(CompositionHandoffStatus.Reserved);
        var bytes=MessagePackSerializer.Serialize(f.State.CurrentView);f.Reserve(replay);MessagePackSerializer.Serialize(f.State.CurrentView).Should().Equal(bytes);
        // Four option legs still reserve exactly one primary Trade, not four Trade IDs.
        aggregate.Orders.Should().ContainSingle();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Expiry_or_cancel_before_reservation_callback_preserves_IDs_without_advancing(bool cancelled)
    {
        var f=await Fixture.Create();f.Accept();var pending=f.State.CurrentView!.CompositionHandoff!;
        var reserved=new PortfolioFundCompositionAggregate().Reserve(pending.Request,f.Command.SelectionBinding.PortfolioSnapshot,7001,[8001],f.Clock.Now,"selector-test");
        if(cancelled)
        {
            var view=f.State.CurrentView! with {Status=WorkflowStrategyMachineStatus.Cancelled,WorkflowRevision=f.State.CurrentView.WorkflowRevision+1,TerminalAtUtc=f.Clock.Now};f.Restore(view);
        }
        else f.Clock.Now=pending.Request.ExpiresAtUtc;
        f.Reserve(reserved);f.State.CurrentView!.CurrentStage.Should().Be(StrategyWorkflowStage.TradeSelection);
        f.State.CurrentView.CompositionHandoff!.Status.Should().Be(CompositionHandoffStatus.Stopped);f.State.CurrentView.CompositionHandoff.Reservation!.Order.OrderId.Should().Be(7001);
        f.State.CurrentView.Status.Should().Be(cancelled?WorkflowStrategyMachineStatus.Cancelled:WorkflowStrategyMachineStatus.TimedOut);
    }
    [Fact]
    public async Task NoTrade_completes_without_a_reservation_or_composer_start()
    {
        var c=await TradeSelectionFixture.Command();c=Authority(c,c.SelectionBinding.PortfolioSnapshot with {Assignments=[]});
        var f=new Fixture(c);f.Accept();f.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Completed);f.State.CurrentView.Outcome.Should().Be(StrategyWorkflowOutcome.NoTrade);f.State.CurrentView.CompositionHandoff.Should().BeNull();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Expired_completion_is_not_recorded_as_an_accepted_selection(bool workflowExpired)
    {
        var f = await Fixture.Create();
        f.Clock.Now = workflowExpired ? f.Command.WorkflowView.ExpiresAtUtc : f.Result.ValidUntilUtc;
        f.Accept();
        f.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        f.State.CurrentView.Outcome.Should().Be(StrategyWorkflowOutcome.TimedOut);
        f.State.CurrentView.TradeSelection.Result.Should().BeNull();
        f.State.CurrentView.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.TimedOut);
        f.State.CurrentView.TradeSelection.Failure!.ErrorType.Should().Be("TradeSelectionTimedOut");
        f.State.CurrentView.CompositionHandoff.Should().BeNull();
    }
    [Fact]
    public async Task Forged_selected_result_fails_the_workflow()
    {
        var f=await Fixture.Create();f.Accept(f.Result with {SelectedCandidate=f.Result.SelectedCandidate! with {Side="Short"}});
        f.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Failed);f.State.CurrentView.StopReasonCode.Should().Be("TS.RESULT.INVALID");f.State.CurrentView.CompositionHandoff.Should().BeNull();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Redispatch_preserves_exact_saved_request_and_revision(bool pending)
    {
        var f=await Fixture.Create();if(pending)f.Accept();var before=f.State.CurrentView!;
        var command=new RedispatchCurrentStrategyPipelineCommand {CommandId=Guid.NewGuid(),EntityId=before.EntityId,WorkflowId=before.WorkflowId,ExpectedWorkflowRevision=before.WorkflowRevision,ExpectedStage=before.CurrentStage,RequestedAtUtc=f.Clock.Now,RequestedBy="recovery"};
        command.Execute(f.Context,f.State);MessagePackSerializer.Serialize(f.State.CurrentView).Should().Equal(MessagePackSerializer.Serialize(before));
        f.State.Events.Should().NotBeEmpty();
    }
    [Fact]
    public async Task Handoff_nested_instructions_are_defensively_copied()
    {
        var f=await Fixture.Create();f.Accept();var handoff=f.State.CurrentView!.CompositionHandoff!;var hash=handoff.ReservationRequestSha256;
        handoff.Request.TradeInstructions[0]=new();PortfolioCanonicalHash.Compute(handoff.Request).Should().Be(hash);
    }
    internal sealed class Fixture
    {
        internal ExecuteTradeSelectionPipelineCommand Command;internal TradeSelectionResult Result;internal IntrinsicTimeStrategyWorkflowCommandState State=new();
        internal IIntrinsicTimeStrategyWorkflowCommandContext Context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();internal Clock Clock;
        internal static async Task<Fixture> Create(string variant="LongFuture")=>new(await TradeSelectionFixture.Command(variant));
        internal Fixture(ExecuteTradeSelectionPipelineCommand c)
        {
            Command=c;Result=TradeSelectionEvaluator.Evaluate(c);Clock=new(c.EvaluatedAtUtc.AddMilliseconds(1));Context.TimeProvider.Returns(Clock);Context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
            Restore(c.WorkflowView with {SelectionDispatch=c});
        }
        internal void Restore(IntrinsicTimeStrategyWorkflowView view){State=new();State.Apply(new WorkflowStrategyStateUpdatedEvent {State=view,EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision},false).Should().BeTrue();}
        internal void Accept(TradeSelectionResult? r=null)=>new CompleteTradeSelectionCommand {CommandId=Guid.NewGuid(),EntityId=Command.WorkflowEntityId,WorkflowId=Command.WorkflowId,InputWorkflowRevision=Command.InputWorkflowRevision,SourceEventId=Result.ResultId,Result=Envelope(r??Result),CompletedAtUtc=Clock.Now}.Execute(Context,State);
        internal void Reserve(FundCompositionReservationResult r)
        {
            var h=State.CurrentView!.CompositionHandoff!;
            new CompleteTradeSelectionReservationCommand {CommandId=Guid.NewGuid(),EntityId=Command.WorkflowEntityId,WorkflowId=Command.WorkflowId,InputWorkflowRevision=h.AcceptedSelectionRevision,SourceEventId=h.SelectionSourceEventId,ReservationRequestSha256=h.ReservationRequestSha256,Reservation=r,CompletedAtUtc=Clock.Now}.Execute(Context,State);
        }
    }
    internal sealed class Clock(DateTime at):TimeProvider {internal DateTime Now=at;public override DateTimeOffset GetUtcNow()=>new(Now);}
}
