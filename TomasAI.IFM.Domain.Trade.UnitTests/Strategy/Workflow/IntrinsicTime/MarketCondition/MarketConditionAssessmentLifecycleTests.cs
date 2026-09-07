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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
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
    public async Task Accepted_assessment_controls_one_workflow_transition(string scenario,WorkflowStrategyMachineStatus expected)
    {
        var c = AssessmentFixture.Command();
        var selection=await TradeSelection.TradeSelectionFixture.Command();
        var authority=selection.SelectionBinding.PortfolioSnapshot with {WorkflowId=c.WorkflowId.Value,CorrelationId=c.CorrelationId};
        authority=authority with {PayloadSha256=TomasAI.IFM.Domain.Portfolio.Workflow.PortfolioCanonicalHash.Compute(authority with {PayloadSha256=""})};
        var selectionBinding=Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts.Seal(selection.SelectionBinding with {PortfolioSnapshot=authority});
        c=c with {WorkflowView=c.WorkflowView with {SelectionBinding=selectionBinding}};
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
        state.CurrentView!.Status.Should().Be(expected,state.CurrentView.TradeSelection.Failure?.ErrorMessage);
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

    [Fact]
    public async Task Completed_replay_after_expiry_does_not_recapture_or_project()
    {
        var f = new FunctionFixture();
        var original = await f.Execute();
        f.Clock.UtcNow = f.Command.ExpiresAtUtc.AddDays(1);
        var replay = await f.Execute();
        replay.Completed.Should().BeSameAs(original.Completed);
        f.Order.Should().Equal("capture", "project", "persist");
    }

    [Fact]
    public async Task Expiry_at_projection_completion_prevents_persistence()
    {
        var f = new FunctionFixture();
        f.Projector.ProjectAsync(Arg.Any<MarketConditionAssessmentCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { f.Clock.UtcNow=f.Command.ExpiresAtUtc; return ValueTask.CompletedTask; });
        var result=await f.Execute();
        result.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout);
        f.Saved.Should().BeFalse();
    }

    [Fact]
    public async Task Timed_out_projection_does_not_append_after_dependency_returns()
    {
        var f=new FunctionFixture();
        var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Projector.ProjectAsync(Arg.Any<MarketConditionAssessmentCompletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_=>new ValueTask(late.Task));
        var result=await f.Execute(f.Command with { ExpiresAtUtc=f.Command.RequestedAtUtc.AddMilliseconds(30) });
        result.Failed!.FailureCategory.Should().Be(MarketConditionFailureCategory.Timeout);
        late.SetResult();
        await Task.Delay(30);
        f.Saved.Should().BeFalse();
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_late_capture_cannot_project()
    {
        var f=new FunctionFixture();
        using var cancellation=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late=new TaskCompletionSource<MarketConditionAssessmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.CaptureAsync(Arg.Any<MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(_=>{ entered.SetResult(); return new ValueTask<MarketConditionAssessmentSnapshot>(late.Task); });
        var running=f.Execute(token:cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await FluentActions.Awaiting(()=>running).Should().ThrowAsync<OperationCanceledException>();
        late.SetResult(Snapshot(f.Command).Seal());
        await Task.Delay(30);
        f.Order.Should().BeEmpty(); f.Saved.Should().BeFalse();
    }

    sealed class Clock(DateTime at):TimeProvider
    {
        public DateTime UtcNow { get; set; } = at;
        public override DateTimeOffset GetUtcNow()=>new(UtcNow);
    }
    sealed class FunctionFixture
    {
        public ExecuteMarketConditionAssessmentCommand Command { get; }=AssessmentFixture.Command();
        public IMarketConditionAssessmentSnapshotProvider Provider { get; }=Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
        public List<string> Order { get; }=[];
        public string FailAt { get; init; }="";
        public bool Saved { get; private set; }
        public Clock Clock { get; }
        public IFunctionProjector<MarketConditionAssessmentCompletedEvent> Projector { get; private set; } = null!;
        readonly MarketConditionFunctionActor _actor;
        readonly IMarketConditionFunctionContext _context=Substitute.For<IMarketConditionFunctionContext>();
        public FunctionFixture()
        {
            var repo=Substitute.For<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,ExecuteMarketConditionAssessmentCommand>>();
            var projector=Substitute.For<IFunctionProjector<MarketConditionAssessmentCompletedEvent>>(); Projector=projector;
            var state=new MarketConditionAssessmentState();
            repo.LoadStateAsync(Arg.Any<ExecuteMarketConditionAssessmentCommand>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult(state));
            Provider.CaptureAsync(Arg.Any<MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(_=>
            { Step("capture"); return ValueTask.FromResult(Snapshot(Command).Seal()); });
            projector.ProjectAsync(Arg.Any<MarketConditionAssessmentCompletedEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{ Step("project"); return ValueTask.CompletedTask; });
            repo.SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<MarketConditionAssessmentState>(),Arg.Any<ExecuteMarketConditionAssessmentCommand>(),Arg.Any<CancellationToken>()).Returns(_=>{ Step("persist"); Saved=true; return ValueTask.CompletedTask; });
            _context.ActorId.Returns(new ActorMailboxId(ActorType.Function, MarketConditionFunctionActor.ActorName));
            _context.StateRepository.Returns(repo); _context.FunctionProjector.Returns(projector);
            _context.SnapshotProvider.Returns(Provider); Clock=new(Command.RequestedAtUtc); _context.TimeProvider.Returns(Clock);
            _context.Logger.Returns(Substitute.For<ILogger<MarketConditionFunctionActor>>());
            _actor=new(_context);
        }
        void Step(string step) { Order.Add(step); if(FailAt==step) throw new InvalidOperationException("Injected "+step+" failure"); }
        public Task<FunctionResult<MarketConditionAssessmentCompletedEvent,MarketConditionAssessmentFailedEvent>> Execute(ExecuteMarketConditionAssessmentCommand? c=null, CancellationToken token=default)
            => MarketConditionFunctionTestDriver.ExecuteAsync(_actor,c??Command,token);
    }
}
