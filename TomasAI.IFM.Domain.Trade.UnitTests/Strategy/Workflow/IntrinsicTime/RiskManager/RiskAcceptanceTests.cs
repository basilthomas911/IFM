using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

[Trait("Category", "PortfolioFinancial")]
public sealed class RiskAcceptanceTests
{
    [Theory]
    [InlineData("quantity")] [InlineData("side")] [InlineData("priceHash")]
    [InlineData("source")] [InlineData("legacy")] [InlineData("expiry")]
    public async Task Rehashed_or_unbound_risk_results_cannot_authorize_execution(string change)
    {
        var request = await RiskFixture.Command();
        var result = new RiskEvaluator().Calculate(request);
        if (change == "quantity") result = result with { StrategyUnits=result.StrategyUnits+1 };
        if (change == "side") result = result with { Legs=result.Legs.SetItem(0, result.Legs[0] with { Side="Sell" }) };
        if (change == "priceHash") result = result with { SizedOrderHash=new('F',64) };
        var complete = Completion(request, result);
        if (change == "source") complete = complete with { SourceEventId=Guid.NewGuid() };
        var fixture = new Fixture(request, change == "legacy");
        if (change == "expiry") fixture.Clock.Now = request.ExpiresAtUtc;
        complete.Execute(fixture.Context, fixture.State);
        var view = fixture.State.CurrentView!;
        view.Status.Should().Be(change == "expiry" ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed);
        view.RiskManagement.Result.Should().BeNull();
        view.RiskManagement.ContinuationDecision.Should().NotBe(StrategyWorkflowContinuationDecision.Proceed);
    }

    [Fact]
    public async Task Approved_proposal_remains_pending_capacity_and_duplicate_reply_does_not_advance_revision()
    {
        var request = await RiskFixture.Command(); var fixture = new Fixture(request);
        var complete = Completion(request, new RiskEvaluator().Calculate(request));
        complete.Execute(fixture.Context, fixture.State);
        var view = fixture.State.CurrentView!;
        view.Status.Should().Be(WorkflowStrategyMachineStatus.Started);
        view.TerminalAtUtc.Should().BeNull();
        view.RiskManagement.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        view.RiskManagement.ContinuationDecision.Should().Be(StrategyWorkflowContinuationDecision.None);
        view.RiskManagement.Result!.ReadRiskResult().Outcome.Should().Be(RiskAssessmentOutcome.Approved);
        complete.Execute(fixture.Context, fixture.State);
        fixture.State.CurrentView!.WorkflowRevision.Should().Be(view.WorkflowRevision);
        fixture.State.CurrentView.RiskExecution!.InputSha256.Should().Be(request.InputSha256);
    }

    [Fact]
    public async Task No_feasible_size_is_a_completed_business_rejection()
    {
        var request = await RiskFixture.Command();
        request = request with { SizingAuthority=request.SizingAuthority with { AvailableCash=0 } };
        request = request with { InputSha256=request.Fingerprint() };
        var result = new RiskEvaluator().Calculate(request);
        result.Outcome.Should().Be(RiskAssessmentOutcome.Rejected);
        var fixture = new Fixture(request);
        Completion(request,result).Execute(fixture.Context,fixture.State);
        fixture.State.CurrentView!.Status.Should().Be(WorkflowStrategyMachineStatus.Completed);
        fixture.State.CurrentView.Outcome.Should().Be(StrategyWorkflowOutcome.NoTrade);
        fixture.State.CurrentView.RiskManagement.ContinuationDecision.Should().Be(StrategyWorkflowContinuationDecision.Stop);
    }

    internal static CompleteRiskManagementCommand Completion(ExecuteRiskManagementPipelineCommand request, RiskAssessmentResult result)
        => new() { CommandId=Guid.NewGuid(), EntityId=request.WorkflowEntityId, WorkflowId=request.WorkflowId,
            InputWorkflowRevision=request.InputWorkflowRevision, SourceEventId=request.CommandId,
            Result=StrategyStageResultEnvelope.CreateRisk(result), CorrelationId=request.CorrelationId,
            CausationId=request.CommandId, CompletedAtUtc=result.ProducedAtUtc };

    sealed class Fixture
    {
        public IntrinsicTimeStrategyWorkflowCommandState State { get; } = new();
        public IIntrinsicTimeStrategyWorkflowCommandContext Context { get; } = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        public Clock Clock { get; }
        public Fixture(ExecuteRiskManagementPipelineCommand request, bool legacy=false)
        {
            Clock=new(request.RequestedAtUtc.AddMilliseconds(1)); Context.TimeProvider.Returns(Clock);
            Context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
            var view=new IntrinsicTimeStrategyWorkflowView { EntityId=request.WorkflowEntityId, WorkflowId=request.WorkflowId,
                WorkflowRevision=request.InputWorkflowRevision, Status=WorkflowStrategyMachineStatus.Started,
                CurrentStage=StrategyWorkflowStage.RiskManagement, ExpiresAtUtc=request.ExpiresAtUtc,
                CorrelationId=request.CorrelationId, RiskExecution=legacy ? null : request,
                RiskManagement=new() { ProcessingStatus=StrategyActorProcessingStatus.Processing, InputWorkflowRevision=request.InputWorkflowRevision } };
            State.Apply(new WorkflowStrategyStateUpdatedEvent { State=view, EntityId=view.EntityId,
                WorkflowId=view.WorkflowId, WorkflowRevision=view.WorkflowRevision },false).Should().BeTrue();
        }
    }
    sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now=now;
        public override DateTimeOffset GetUtcNow()=>new(Now);
    }
}
