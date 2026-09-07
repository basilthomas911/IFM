using TomasAI.IFM.Shared.EventSourcing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
public sealed class TradeSelectionFunctionTests
{
    [Fact,Trait("Fixture","TS-C29")]
    public async Task Success_projects_then_appends_and_replay_never_reprojects_even_after_expiry()
    {
        var c=await TradeSelectionFixture.Command();var f=new FunctionFixture(c);
        var first=await f.Execute();first.IsCompleted.Should().BeTrue();f.Order.Should().Equal("load","project","persist");
        f.Clock.Now=c.ExpiresAtUtc.AddDays(1);var replay=await f.Execute();replay.Completed.Should().Be(first.Completed);f.Order.Should().Equal("load","project","persist","load");
        var conflict=await f.Execute(c with{CausationId=Guid.NewGuid()});conflict.IsFailed.Should().BeTrue();conflict.Failed!.ReasonCode.Should().Be("TS.CONTRACT.CONFLICTING_DUPLICATE");
    }
    [Theory,InlineData("load"),InlineData("project"),InlineData("persist"),Trait("Fixture","TS-C30")]
    public async Task Failed_attempt_is_not_committed_and_identical_retry_can_complete(string stage)
    {
        var f=new FunctionFixture(await TradeSelectionFixture.Command()){FailAt=stage};
        var result=await f.Execute();result.IsFailed.Should().BeTrue();f.Committed.Should().BeNull();
        if(stage=="project")f.Order.Should().NotContain("persist");
        f.FailAt="";(await f.Execute()).IsCompleted.Should().BeTrue();f.Committed.Should().NotBeNull();
    }
    [Fact,Trait("Fixture","TS-C31")]
    public async Task Caller_cancellation_releases_payload_without_projection_or_state()
    {
        var f=new FunctionFixture(await TradeSelectionFixture.Command());using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Func<Task> act=()=>f.Execute(token:cancellation.Token);await act.Should().ThrowAsync<OperationCanceledException>();f.Committed.Should().BeNull();f.Order.Should().NotContain("project");
    }
    [Fact,Trait("Fixture","TS-C31")]
    public async Task Late_projection_is_observed_and_cannot_append_a_success()
    {
        var c=await TradeSelectionFixture.Command();c=c with{ExpiresAtUtc=c.EvaluatedAtUtc.AddMilliseconds(15)};
        var f=new FunctionFixture(c);var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Project=()=>new ValueTask(late.Task);
        var result=await f.Execute();result.IsFailed.Should().BeTrue();result.Failed!.ReasonCode.Should().Be("TS.TIME.EXPIRED");
        late.SetResult();await Task.Yield();f.Committed.Should().BeNull();f.Order.Should().NotContain("persist");
    }
    [Fact,Trait("Fixture","TS-C21")]
    public async Task Production_capabilities_reject_unimplemented_builder_before_state_loading()
    {
        var c=await TradeSelectionFixture.Command();var f=new FunctionFixture(c);
        f.Context.Capabilities.Returns(new StrategyCatalogCapabilityRegistry(TradeSelectionCatalogCapabilities.Create()));
        (await f.Execute()).IsFailed.Should().BeTrue();f.Order.Should().BeEmpty();
    }
    [Theory,InlineData("Execute",true),InlineData("Start",false),InlineData("execute",false),InlineData("Unknown",false)]
    public async Task Exact_mailbox_verb_controls_dispatch_and_payload_is_always_released(string verb,bool success)
    {
        var c=await TradeSelectionFixture.Command();var f=new FunctionFixture(c);
        var result=await f.Execute(c with{Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,verb,c.EntityId.Format())});
        result.IsCompleted.Should().Be(success);
    }
    internal sealed class Clock(DateTime now):TimeProvider{public DateTime Now {get;set;}=now;public override DateTimeOffset GetUtcNow()=>new(Now);}
    internal sealed class FixtureOnlyDownstreamCapability(CatalogCapability capability):IStrategyCatalogCapabilityValidator
    {
        public CatalogCapability Capability=>capability;
        public void Validate(StrategyCatalogDefinition owner,IReadOnlyDictionary<CatalogKey,StoredStrategyCatalogDefinition> graph)
        {
            if(owner.Key.Kind!=StrategyCatalogKind.Structure || capability.Role is not ("builder" or "risk") || owner.Legs.Length is <1 or >4)
                throw new ArgumentException("Invalid isolated downstream fixture capability.");
        }
    }
    internal sealed class FunctionFixture
    {
        public readonly ExecuteTradeSelectionPipelineCommand Command;
        public readonly ITradeSelectionFunctionContext Context=Substitute.For<ITradeSelectionFunctionContext>();
        public readonly List<string> Order=[];
        public readonly Clock Clock;
        public string FailAt="";
        public Func<ValueTask>? Project;
        public TradeSelectionFunctionCompletedEvent? Committed;
        readonly TradeSelectionFunctionActor actor;
        public FunctionFixture(ExecuteTradeSelectionPipelineCommand c)
        {
            Command=c;Clock=new(c.EvaluatedAtUtc);
            var repo=Substitute.For<IEventSourceFunctionStateRepository<TradeSelectionFunctionState,ExecuteTradeSelectionPipelineCommand>>();
            var projector=Substitute.For<IFunctionProjector<TradeSelectionFunctionCompletedEvent>>();
            repo.LoadStateAsync(Arg.Any<ExecuteTradeSelectionPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(_=>
            {Step("load");var state=new TradeSelectionFunctionState();if(Committed is not null)state.TryComplete(Committed,Command);return ValueTask.FromResult(state);});
            projector.ProjectAsync(Arg.Any<TradeSelectionFunctionCompletedEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{Step("project");return Project?.Invoke()??ValueTask.CompletedTask;});
            repo.SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<TradeSelectionFunctionState>(),Arg.Any<ExecuteTradeSelectionPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{Step("persist");Committed=call.Arg<TradeSelectionFunctionState>().CompletedEvent;return ValueTask.CompletedTask;});
            Context.ActorId.Returns(new ActorMailboxId(ActorType.Function,TradeSelectionFunctionActor.ActorName));Context.StateRepository.Returns(repo);Context.FunctionProjector.Returns(projector);Context.TimeProvider.Returns(Clock);Context.Logger.Returns(Substitute.For<ILogger<TradeSelectionFunctionActor>>());
            var fixtures=c.SelectionBinding.CatalogDefinitions.SelectMany(x=>x.Capabilities).Where(x=>x.Role is "builder" or "risk").Select(x=>new CatalogCapability(x.Role,x.Code,x.Version)).Distinct().Select(x=>(IStrategyCatalogCapabilityValidator)new FixtureOnlyDownstreamCapability(x));
            Context.Capabilities.Returns(new StrategyCatalogCapabilityRegistry(TradeSelectionCatalogCapabilities.Create().Concat(fixtures)));
            actor=new(Context);
        }
        void Step(string step){Order.Add(step);if(FailAt==step)throw new InvalidOperationException("Injected "+step);}
        public Task<FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>> Execute(ExecuteTradeSelectionPipelineCommand? c=null,CancellationToken token=default)=>TradeSelectionFunctionTestDriver.ExecuteAsync(actor,c??Command,token);
    }
}
