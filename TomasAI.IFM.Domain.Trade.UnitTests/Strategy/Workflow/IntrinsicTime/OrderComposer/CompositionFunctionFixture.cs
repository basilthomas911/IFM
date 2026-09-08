using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventSourcing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
    internal sealed class CompositionTestClock(DateTime now):TimeProvider{public DateTime Now {get;set;}=now;public override DateTimeOffset GetUtcNow()=>new(Now);}
    internal sealed class CompositionFunctionFixture
    {
        public readonly ExecuteOrderCompositionPipelineCommand Command;
        public readonly IOrderCompositionFunctionContext Context=Substitute.For<IOrderCompositionFunctionContext>();
        public readonly List<string> Order=[];
        public readonly CompositionTestClock Clock;
        public string FailAt="";
        public Func<ValueTask>? Project { get; set; }
        public OrderCompositionFunctionCompletedEvent? Committed;
        internal readonly OrderCompositionFunctionActor actor;
        public CompositionFunctionFixture(ExecuteOrderCompositionPipelineCommand c)
        {
            Command=c;Clock=new(c.EvaluatedAtUtc);
            var repo=Substitute.For<IEventSourceFunctionStateRepository<OrderCompositionFunctionState,ExecuteOrderCompositionPipelineCommand>>();
            var projector=Substitute.For<IFunctionProjector<OrderCompositionFunctionCompletedEvent>>();
            repo.LoadStateAsync(Arg.Any<ExecuteOrderCompositionPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(_=>
            {Step("load");var state=new OrderCompositionFunctionState();if(Committed is not null)state.TryComplete(Committed,Command);return ValueTask.FromResult(state);});
            projector.ProjectAsync(Arg.Any<OrderCompositionFunctionCompletedEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{Step("project");return Project?.Invoke()??ValueTask.CompletedTask;});
            repo.SaveCompletedStateAsync(Arg.Any<IFunctionActorContext>(),Arg.Any<OrderCompositionFunctionState>(),Arg.Any<ExecuteOrderCompositionPipelineCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{Step("persist");Committed=call.Arg<OrderCompositionFunctionState>().CompletedEvent;return ValueTask.CompletedTask;});
            Context.ActorId.Returns(new ActorMailboxId(ActorType.Function,OrderCompositionFunctionActor.ActorName));Context.StateRepository.Returns(repo);Context.FunctionProjector.Returns(projector);Context.TimeProvider.Returns(Clock);Context.Logger.Returns(Substitute.For<ILogger<OrderCompositionFunctionActor>>());
            Context.CalculationModel.Returns(new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()));
            actor=new(Context);
        }
        void Step(string step){Order.Add(step);if(FailAt==step)throw new InvalidOperationException("Injected "+step);}
        public Task<FunctionResult<OrderCompositionFunctionCompletedEvent,OrderCompositionFunctionFailedEvent>> Execute(ExecuteOrderCompositionPipelineCommand? c=null,CancellationToken token=default)=>OrderCompositionFunctionTestDriver.ExecuteAsync(actor,c??Command,token);
    }
