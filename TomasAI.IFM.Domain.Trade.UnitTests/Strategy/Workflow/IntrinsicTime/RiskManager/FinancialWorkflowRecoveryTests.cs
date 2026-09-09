using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-05")]
public sealed class FinancialWorkflowRecoveryTests
{
    static readonly DateTime Now=new(2026,9,9,0,0,0,DateTimeKind.Utc);
    [Fact]
    public void Lost_notification_resumes_exact_workflow_revision_and_does_not_replace_financial_requests()
    {
        var snapshot=Snapshot();
        var command=FinancialWorkflowRecoveryModel.Create(snapshot,Now).Should().BeOfType<RedispatchCurrentStrategyPipelineCommand>().Subject;
        command.ExpectedWorkflowRevision.Should().Be(snapshot.WorkflowRevision);
        command.WorkflowId.Should().Be(snapshot.WorkflowId);
        command.ExpectedStage.Should().Be(StrategyWorkflowStage.RiskManagement);
        command.RequestedAtUtc.Should().Be(Now);
        snapshot.State.ExpiresAtUtc.Should().Be(Now.AddMinutes(1));
        FinancialWorkflowRecoveryModel.Create(snapshot,Now)!.CommandId.Should().NotBe(command.CommandId);
    }
    [Fact]
    public void Expired_workflow_gets_a_mapped_timeout_not_new_admission()
    {
        var snapshot=Snapshot(); snapshot=snapshot with { State=snapshot.State with { ExpiresAtUtc=Now } };
        var command=FinancialWorkflowRecoveryModel.Create(snapshot,Now).Should().BeOfType<TimeoutRiskManagementCommand>().Subject;
        command.TimeoutId.Should().Be(command.CommandId);command.TimedOutAtUtc.Should().Be(Now);
        command.ExpectedStage.Should().Be(StrategyWorkflowStage.RiskManagement);
    }
    [Theory]
    [InlineData(WorkflowStrategyMachineStatus.Cancelled)]
    [InlineData(WorkflowStrategyMachineStatus.Completed)]
    [InlineData(WorkflowStrategyMachineStatus.TimedOut)]
    public void Terminal_workflow_is_never_restarted(WorkflowStrategyMachineStatus status)
    {
        var snapshot=Snapshot();
        FinancialWorkflowRecoveryModel.Create(snapshot with { State=snapshot.State with { Status=status } },Now).Should().BeNull();
    }
    [Theory]
    [InlineData(RiskFinancialHandoffPhase.ConsumePending)]
    [InlineData(RiskFinancialHandoffPhase.Consumed)]
    [InlineData(RiskFinancialHandoffPhase.Submitted)]
    [InlineData(RiskFinancialHandoffPhase.Authorized)]
    public void Recovery_cannot_invent_execution_or_release_old_commitments(RiskFinancialHandoffPhase phase)
    {
        var snapshot=Snapshot();
        FinancialWorkflowRecoveryModel.Create(snapshot with { State=snapshot.State with
            { FinancialHandoff=new() { Phase=phase } } },Now).Should().BeNull();
    }
    [Fact]
    public void Recent_dispatch_is_given_time_to_complete()
        =>FinancialWorkflowRecoveryModel.Create(Snapshot() with { UpdatedAtUtc=Now.AddSeconds(-14) },Now).Should().BeNull();

    [Fact]
    public async Task Early_timeout_cannot_end_workflow_but_due_timeout_preserves_financial_checkpoint()
    {
        var input=await RiskFixture.Command(atUtc:Now);
        var view=Snapshot().State with { EntityId=input.WorkflowEntityId,WorkflowId=input.WorkflowId,
            RiskExecution=input,FinancialHandoff=new() { Phase=RiskFinancialHandoffPhase.ReservePending } };
        var state=new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State=view,EntityId=view.EntityId,
            WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision },false).Should().BeTrue();
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        var clock=new Clock(Now);context.TimeProvider.Returns(clock);
        context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        var command=new TimeoutRiskManagementCommand { CommandId=Guid.NewGuid(),TimeoutId=Guid.NewGuid(),EntityId=view.EntityId,
            WorkflowId=view.WorkflowId,ExpectedWorkflowRevision=view.WorkflowRevision,ExpectedStage=view.CurrentStage,TimedOutAtUtc=Now };
        command.Execute(context,state);state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        clock.Now=view.ExpiresAtUtc;
        command.Execute(context,state);
        state.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        state.CurrentView.FinancialHandoff!.Phase.Should().Be(RiskFinancialHandoffPhase.ReservePending);
        state.CurrentView.RiskManagement.Failure!.ErrorType.Should().Be("RiskManagementTimedOut");
    }
    static WorkflowStrategyStateUpdatedEvent Snapshot()
    {
        var view=new IntrinsicTimeStrategyWorkflowView { WorkflowId=StrategyWorkflowId.New(TimeProvider.System),WorkflowRevision=7,
            Status=WorkflowStrategyMachineStatus.Started,CurrentStage=StrategyWorkflowStage.RiskManagement,
            ExpiresAtUtc=Now.AddMinutes(1),UpdatedAtUtc=Now.AddSeconds(-20) };
        return new() { State=view,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,UpdatedAtUtc=view.UpdatedAtUtc };
    }
    sealed class Clock(DateTime now):TimeProvider
    { public DateTime Now=now;public override DateTimeOffset GetUtcNow()=>new(Now); }
}
