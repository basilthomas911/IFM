using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition.MarketConditionAssessmentCalculationTests;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

[Trait("Gate","MC-R06")]
public sealed class MarketConditionAssessmentLifecycleTests
{
    [Theory]
    [InlineData("available",WorkflowStrategyMachineStatus.Started)]
    [InlineData("poor",WorkflowStrategyMachineStatus.Started)]
    [InlineData("unavailable",WorkflowStrategyMachineStatus.Completed)]
    [InlineData("restricted",WorkflowStrategyMachineStatus.Completed)]
    [InlineData("expired",WorkflowStrategyMachineStatus.TimedOut)]
    [InlineData("wrong-horizon",WorkflowStrategyMachineStatus.Failed)]
    public void Accepted_assessment_controls_one_workflow_transition(string scenario,WorkflowStrategyMachineStatus expected)
    {
        var c = AssessmentFixture.Command();
        if(scenario=="restricted") c=WithDecision(c,MarketConditionAssessmentContracts.ValidateRequest(c).Decision with { Restrictions=[RegimeRestriction.NoNewTrade] });
        var s=Snapshot(c);
        if(scenario=="poor") s=s with { Quote=new(5000,5010,1,1),SessionState=MarketSessionStatus.Closed,EventContext=AssessmentEventContext.Elevated };
        if(scenario=="unavailable") s=s with { Observations=s.Observations.Select(x=>x.SourceId=="FeedHealth"?x with { Availability=MarketSourceAvailability.Unavailable }:x).ToArray() };
        var r=Calculate(c,s);
        if(scenario=="wrong-horizon") r=r with { TargetHorizon=TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly };
        var state=new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent { State=c.WorkflowView,WorkflowId=c.WorkflowId,WorkflowRevision=c.InputWorkflowRevision,EntityId=c.WorkflowEntityId },addEvent:false).Should().BeTrue();
        var complete=new CompleteMarketConditionCommand
        {
            CommandId=Guid.NewGuid(),EntityId=c.WorkflowEntityId,WorkflowId=c.WorkflowId,InputWorkflowRevision=c.InputWorkflowRevision,
            Subject=new(ActorType.Command,CompleteMarketConditionCommand.Actor,CompleteMarketConditionCommand.Verb,c.WorkflowEntityId.Format()),
            SourceEventId=r.ResultId,CompletedAtUtc=c.RequestedAtUtc,
            Result=StrategyStageResultEnvelope.Create(r.ResultId,nameof(MarketConditionAssessmentResult),1,MessagePackSerializer.Serialize(r),r.EvaluatedAtUtc,r.EvaluatedAtUtc)
        };
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        if(scenario!="wrong-horizon") MarketConditionAssessmentContracts.ValidateAcceptance(MarketConditionAssessmentContracts.ReadResult(complete.Result),state.CurrentView!,c.InputWorkflowRevision);
        context.TimeProvider.Returns(new Clock(scenario=="expired"?c.RequestedAtUtc.AddSeconds(2):c.RequestedAtUtc));
        context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        complete.Execute(context,state);
        state.CurrentView!.Status.Should().Be(expected);
        if(expected==WorkflowStrategyMachineStatus.Started) state.CurrentView.CurrentStage.Should().Be(StrategyWorkflowStage.TradeSelection);
        if(expected==WorkflowStrategyMachineStatus.Completed) state.CurrentView.Outcome.Should().Be(StrategyWorkflowOutcome.NoTrade);
        var accepted=MessagePackSerializer.Serialize(state.CurrentView);
        state.Events.Clear(); complete.Execute(context,state);
        MessagePackSerializer.Serialize(state.CurrentView).Should().Equal(accepted); state.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Completed_state_replays_without_capture_and_conflicting_duplicate_is_rejected()
    {
        var f=new FunctionFixture();
        var result=await f.Execute(); result.IsCompleted.Should().BeTrue(); f.Order.Should().Equal("capture","project","persist");
        var replay=await f.Execute(); MessagePackSerializer.Serialize(replay.Completed).Should().Equal(MessagePackSerializer.Serialize(result.Completed));
        f.Order.Should().HaveCount(3);
        var conflict=await f.Execute(f.Command with { CorrelationId=Guid.NewGuid() }); conflict.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.ContractInvalid);
    }

    [Theory]
    [InlineData("capture",MarketConditionFailureCategory.RequiredInputInvalid)]
    [InlineData("project",MarketConditionFailureCategory.ProjectionFailed)]
    [InlineData("persist",MarketConditionFailureCategory.PersistenceFailed)]
    public async Task Technical_failure_never_returns_completed_authority(string step,MarketConditionFailureCategory category)
    {
        var f=new FunctionFixture { FailAt=step }; var r=await f.Execute(); r.IsFailed.Should().BeTrue(); r.Failed!.FailureCategory.Should().Be(category);
        f.Saved.Should().BeFalse();
    }

    [Fact]
    public async Task Timed_out_capture_cannot_project_or_append_when_it_finishes_late()
    {
        var f=new FunctionFixture();
        var late=new TaskCompletionSource<MarketConditionAssessmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.CaptureAsync(Arg.Any<MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(_=>new ValueTask<MarketConditionAssessmentSnapshot>(late.Task));
        var result=await f.Execute(f.Command with { ExpiresAtUtc=f.Command.RequestedAtUtc.AddMilliseconds(30) });
        result.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout);
        late.SetResult(Snapshot(f.Command).Seal());
        await Task.Delay(30);
        f.Order.Should().BeEmpty(); f.Saved.Should().BeFalse();
    }

    sealed class Clock(DateTime at):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(at); }
    sealed class FunctionFixture
    {
        public ExecuteMarketConditionAssessmentCommand Command { get; }=AssessmentFixture.Command();
        public IMarketConditionAssessmentSnapshotProvider Provider { get; }=Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
        public List<string> Order { get; }=[];
        public string FailAt { get; init; }="";
        public bool Saved { get; private set; }
        readonly MarketConditionAssessmentHandler _handler;
        readonly IFunctionActorContext _context=Substitute.For<IFunctionActorContext>();
        public FunctionFixture()
        {
            var repo=Substitute.For<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>>();
            var projector=Substitute.For<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>();
            var state=new MarketConditionAssessmentState();
            repo.LoadStateAsync(Arg.Any<ExecuteMarketConditionAssessmentCommand>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult(state));
            Provider.CaptureAsync(Arg.Any<MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(_=>
            { Step("capture"); return ValueTask.FromResult(Snapshot(Command).Seal()); });
            projector.ProjectAsync(Arg.Any<MarketConditionAssessmentCompletedEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{ Step("project"); return ValueTask.CompletedTask; });
            repo.SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<MarketConditionAssessmentState>(),Arg.Any<ExecuteMarketConditionAssessmentCommand>(),Arg.Any<CancellationToken>()).Returns(_=>{ Step("persist"); Saved=true; return ValueTask.CompletedTask; });
            _handler=new(Provider,repo,projector,Substitute.For<ILogger<MarketConditionAssessmentHandler>>(),new Clock(Command.RequestedAtUtc));
        }
        void Step(string step) { Order.Add(step); if(FailAt==step) throw new InvalidOperationException("Injected "+step+" failure"); }
        public Task<FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>> Execute(ExecuteMarketConditionAssessmentCommand? c=null)
            => _handler.ExecuteAsync(_context,c??Command,Command.Subject.ThreadId);
    }
}
