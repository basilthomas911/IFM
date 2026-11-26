using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Trade.Shared;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowDeliverySettlementAuditTests
{
    [Fact]
    public async Task Required_workflow_dispatch_failure_settles_failure_without_metrics()
    {
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        var id=new ActorMailboxId(ActorType.Realtime,IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName);
        context.ActorId.Returns(id);context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowRealtimeActor>.Instance);
        context.TimeProvider.Returns(TimeProvider.System);
        var calls=0;
        context.ReadActiveWorkflowAsync(Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>(),Arg.Any<CancellationToken>())
            .Returns(_=>{calls++;return Task.FromException<IntrinsicTimeStrategyWorkflowView?>(new IOException("Injected workflow projection read failure."));});
        var supervisor=Substitute.For<IActorSupervisor>();var container=Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_=>new ActorThreadQueueV2(16));supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool=new ActorThreadPoolV2(supervisor,NullLogger.Instance);pool.Initialize(1);supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call=>pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(),Arg.Any<CancellationToken>()).Returns(call=>pool.GetThreadAsync(call.Arg<ActorThreadId>(),call.Arg<CancellationToken>()));
        var queue=new ActorMailbox(supervisor,id);var mailbox=Substitute.For<IActorMailbox>();mailbox.ThreadQueues.Returns(queue.ThreadQueues);mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox);
        var actor=new IntrinsicTimeStrategyWorkflowRealtimeActor(context);supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{[id]=actor});
        await actor.StartAsync(supervisor);
        try
        {
            var initial=IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification();
            var source=initial with{Subject=new(ActorType.Realtime,IntrinsicTimeStrategyWorkflowRealtimeActor.ActorName,WorkflowStrategyStateUpdatedEvent.Verb,"same"),State=initial.State with{CurrentStage=StrategyWorkflowStage.TradeSelection}};
            var delivery=new Delivery(source);Assert.True(await queue.ThreadQueues.WriteAsync(delivery));
            await delivery.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1,calls);Assert.False(await delivery.Settled.Task);
        }
        finally{await actor.StopAsync();}
    }
    sealed class Delivery(IEvent source):IActorMessage,IActorDeliveryCompletion
    {
        public ActorSubject Subject=>source.Subject;public ActorSubject ReplySubject{get;set;}
        public readonly TaskCompletionSource<bool> Settled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Disposed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>()where TEvent:class,IEvent=>source as TEvent;
        public TCommand? AsCommand<TCommand>()where TCommand:class,ICommand=>null;
        public TQuery? AsQuery<TQuery,TResult>()where TQuery:class,IQuery<TResult>where TResult:class=>null;
        public ValueTask ReplyAsync<TResult>(TResult result)where TResult:class=>ValueTask.CompletedTask;
        public void ReleasePayload(){}public void Dispose()=>Disposed.TrySetResult();
        public ValueTask CompleteDeliveryAsync(bool succeeded){Settled.TrySetResult(succeeded);return ValueTask.CompletedTask;}
    }
}
