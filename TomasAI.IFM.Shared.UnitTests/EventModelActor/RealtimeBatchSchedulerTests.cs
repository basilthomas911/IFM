using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class RealtimeBatchSchedulerTests
{
    [Fact]
    public async Task Scheduler_batches_existing_backlog_without_early_ACK_and_preserves_FIFO()
    {
        var runtime = Create(); await using var pool = runtime.Pool;
        var first = new Message(0);
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(first));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var remaining = Enumerable.Range(1, 64).Select(n => new Message(n)).ToArray();
        foreach (var message in remaining) Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(message));
        Assert.Empty(first.Outcomes);
        Assert.All(remaining, message => Assert.Empty(message.Outcomes));
        runtime.Actor.Release.TrySetResult();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(10)));
        Assert.Equal(Enumerable.Range(0, 65), runtime.Actor.Committed);
        Assert.Equal(new[] { 1, 64 }, runtime.Actor.Sizes);
        Assert.All(remaining.Append(first), message => { Assert.Equal(new[] { true }, message.Outcomes); Assert.Equal(1, message.Disposals); });
        Assert.Equal(0, runtime.Actor.SingleCalls);
    }

    [Fact]
    public async Task Failed_batch_settles_false_and_retries_in_the_same_order()
    {
        var runtime = Create(); await using var pool = runtime.Pool;
        runtime.Actor.Fail = true;
        var messages = Enumerable.Range(0, 8).Select(n => new Message(n)).ToArray();
        foreach (var message in messages) Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(message));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runtime.Actor.Release.TrySetResult();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(10)));
        Assert.Empty(runtime.Actor.Committed);
        Assert.All(messages, message => Assert.Equal(new[] { false }, message.Outcomes));
        runtime.Actor.Fail = false;
        var retried = Enumerable.Range(0, 8).Select(n => new Message(n)).ToArray();
        foreach (var message in retried) Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(message));
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(10)));
        Assert.Equal(Enumerable.Range(0, 8), runtime.Actor.Committed);
        Assert.All(retried, message => Assert.Equal(new[] { true }, message.Outcomes));
    }

    [Fact]
    public async Task Retirement_does_not_ACK_or_dispose_an_unfinished_batch()
    {
        var runtime = Create(); await using var pool = runtime.Pool;
        RealtimeActorResetPolicy.Register(typeof(BatchActor));
        var message = new Message(1);
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(message));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runtime.Actor.RealtimeGeneration.Retire();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(10)));
        Assert.Empty(message.Outcomes); Assert.Equal(0, message.Disposals);
        runtime.Actor.Release.TrySetResult();
        await message.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { false }, message.Outcomes);
        Assert.Equal(1, message.Disposals);
    }

    [Fact]
    public async Task Source_generation_fencing_and_completion_evidence_are_preserved_in_a_batch()
    {
        var generation = Guid.NewGuid();
        var guard = new Guard(generation);
        var evidence = new RealtimeActorCompletionEvidence();
        evidence.Arm(new("GLBX.MDP3", generation, ActorType.Realtime, "BatchSchedulerTest", "Run"));
        var runtime = Create(guard, evidence); await using var pool = runtime.Pool;
        var first = new Message(0) { SourceGenerationId = generation };
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(first));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stale = new Message(1) { SourceGenerationId = Guid.NewGuid() };
        var current = new Message(2) { SourceGenerationId = generation };
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(stale));
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(current));
        runtime.Actor.Release.TrySetResult();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0, 2 }, runtime.Actor.Committed);
        Assert.Equal(2, evidence.Capture().Completed);
        Assert.Equal(2, guard.Released);
        Assert.All(new[] { first, stale, current }, message => Assert.Equal(1, message.Disposals));
    }

    [Fact]
    public async Task Broker_group_is_one_local_slot_but_handlers_and_settlements_see_every_ordered_child()
    {
        var runtime=Create();await using var pool=runtime.Pool;
        var children=Enumerable.Range(0,64).Select(n=>new BrokerMessage(n)).ToArray();var batch=new BrokerBatch(children);
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(batch));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[]{64},runtime.Actor.Sizes);Assert.All(children,child=>{Assert.Empty(child.Outcomes);Assert.Equal(0,child.Disposals);});Assert.Equal(0,batch.Disposals);
        runtime.Actor.Release.TrySetResult();Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(Enumerable.Range(0,64),runtime.Actor.Committed);Assert.Equal(1,batch.Disposals);Assert.Equal(0,batch.FailedSettlements);
        Assert.All(children,child=>{Assert.Equal(new[]{true},child.Outcomes);Assert.Equal(1,child.Disposals);});
    }
    [Fact]
    public async Task Failed_broker_group_retries_every_child_before_later_ordered_work()
    {
        var runtime=Create();await using var pool=runtime.Pool;runtime.Actor.Fail=true;
        var first=new BrokerBatch(Enumerable.Range(0,8).Select(n=>new BrokerMessage(n)).ToArray());
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(first));await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));runtime.Actor.Release.TrySetResult();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));Assert.Empty(runtime.Actor.Committed);
        Assert.All(first.Children,child=>Assert.Equal(new[]{false},child.Outcomes));Assert.Equal(1,first.Disposals);
        runtime.Actor.Fail=false;var retry=new BrokerBatch(Enumerable.Range(0,8).Select(n=>new BrokerMessage(n)).ToArray());
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(retry));Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        var later=new BrokerBatch(new[]{new BrokerMessage(8)});Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(later));
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(Enumerable.Range(0,9),runtime.Actor.Committed);Assert.All(retry.Children,child=>Assert.Equal(new[]{true},child.Outcomes));
    }
    [Fact]
    public async Task Broker_wrapper_and_child_leases_survive_retirement_until_actual_handler_finishes()
    {
        var generation=Guid.NewGuid();var guard=new Guard(generation);var runtime=Create(guard);await using var pool=runtime.Pool;
        RealtimeActorResetPolicy.Register(typeof(BatchActor));
        var children=new[]{new BrokerMessage(0){SourceGenerationId=generation},new BrokerMessage(1){SourceGenerationId=generation}};var batch=new BrokerBatch(children);
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(batch));await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runtime.Actor.RealtimeGeneration.Retire();Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(0,batch.Disposals);Assert.Equal(0,guard.Released);Assert.All(children,child=>{Assert.Equal(0,child.Disposals);Assert.Empty(child.Outcomes);});
        runtime.Actor.Release.TrySetResult();await batch.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1,batch.Disposals);Assert.Equal(2,guard.Released);Assert.All(children,child=>{Assert.Equal(1,child.Disposals);Assert.Equal(new[]{false},child.Outcomes);});
    }
    [Fact]
    public async Task Legacy_backlog_never_swallows_broker_wrapper_and_child_order_is_preserved()
    {
        var runtime=Create();await using var pool=runtime.Pool;
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(new Message(0)));await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(new Message(1)));
        var batch=new BrokerBatch(new[]{new BrokerMessage(2),new BrokerMessage(3),new BrokerMessage(4)});
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(batch));Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(new Message(5)));
        runtime.Actor.Release.TrySetResult();Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(new[]{1,1,3,1},runtime.Actor.Sizes);Assert.Equal(Enumerable.Range(0,6),runtime.Actor.Committed);Assert.Equal(1,batch.Disposals);
    }
    [Fact]
    public async Task Nonopted_actor_rejects_broker_batch_without_calling_handler()
    {
        var runtime=Create(wrapper:actor=>new NonBatchActor(actor));await using var pool=runtime.Pool;
        var batch=new BrokerBatch(new[]{new BrokerMessage(0),new BrokerMessage(1)});
        Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(batch));Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(0,runtime.Actor.SingleCalls);Assert.Equal(1,batch.FailedSettlements);Assert.Equal(1,batch.Disposals);
        Assert.All(batch.Children,child=>{Assert.Equal(new[]{false},child.Outcomes);Assert.Equal(1,child.Disposals);});
    }
    [Fact]
    public async Task Broker_children_keep_individual_generation_fence_and_completion_evidence()
    {
        var generation=Guid.NewGuid();var guard=new Guard(generation);var evidence=new RealtimeActorCompletionEvidence();
        evidence.Arm(new("GLBX.MDP3",generation,ActorType.Realtime,"BatchSchedulerTest","Run"));
        var runtime=Create(guard,evidence);await using var pool=runtime.Pool;
        var children=new[]{new BrokerMessage(0){SourceGenerationId=generation},new BrokerMessage(1){SourceGenerationId=Guid.NewGuid()},new BrokerMessage(2){SourceGenerationId=generation}};
        var batch=new BrokerBatch(children);Assert.True(await runtime.Mailbox.ThreadQueues.WriteAsync(batch));
        await runtime.Actor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));runtime.Actor.Release.TrySetResult();
        Assert.True(await pool.WaitForIdleAsync(TimeSpan.FromMilliseconds(1),TimeSpan.FromSeconds(10)));
        Assert.Equal(new[]{0,2},runtime.Actor.Committed);Assert.Equal(1,children[1].Fenced);Assert.Empty(children[1].Outcomes);
        Assert.Equal(2,evidence.Capture().Completed);Assert.Equal(2,guard.Released);Assert.All(children,child=>Assert.Equal(1,child.Disposals));
    }

    static (ActorThreadPoolV2 Pool, ActorMailbox Mailbox, BatchActor Actor) Create(IRealtimeSourceAdmission? guard = null, RealtimeActorCompletionEvidence? evidence = null, Func<BatchActor,IActor>? wrapper=null)
    {
        var id = new ActorMailboxId(ActorType.Realtime, "BatchSchedulerTest");
        var container = new Mock<IContainerInstance>();
        container.Setup(value => value.Resolve<IActorThreadQueue>()).Returns(() => new ActorThreadQueueV2(128));
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(value => value.Container).Returns(container.Object);
        var pool = new ActorThreadPoolV2(supervisor.Object, NullLogger.Instance, realtimeSourceAdmission: guard, realtimeCompletionEvidence: evidence); pool.Initialize(4);
        supervisor.SetupGet(value => value.ThreadPool).Returns(pool);
        supervisor.Setup(value => value.GetThread(It.IsAny<ActorThreadId>())).Returns((ActorThreadId thread) => pool.GetThread(thread));
        supervisor.Setup(value => value.GetThreadAsync(It.IsAny<ActorThreadId>(), It.IsAny<CancellationToken>()))
            .Returns((ActorThreadId thread, CancellationToken token) => pool.GetThreadAsync(thread, token));
        var mailbox = new ActorMailbox(supervisor.Object, id);
        var actor = new BatchActor(id, mailbox);
        supervisor.SetupGet(value => value.Children).Returns(new Dictionary<ActorMailboxId, IActor> { [id] = wrapper?.Invoke(actor)??actor });
        return (pool, mailbox, actor);
    }

    sealed class BatchActor(ActorMailboxId id, IActorMailbox mailbox) : IActor, IBatchedRealtimeActor, IReplaceableRealtimeActor
    {
        public ActorMailboxId Id => id;
        public IActorMailbox Mailbox => mailbox;
        public bool IsRunning => true;
        public bool Fail;
        public int SingleCalls;
        public RealtimeActorGeneration RealtimeGeneration { get; } = new();
        public void RetireRealtimeGeneration() => RealtimeGeneration.Retire();
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ConcurrentQueue<int> Committed = new();
        public readonly ConcurrentQueue<int> Sizes = new();
        public ValueTask StartAsync(IActorSupervisor supervisor) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;
        public ValueTask HandleMessageAsync(IActorMessage message) { SingleCalls++; throw new InvalidOperationException("Batch path was bypassed."); }
        public async ValueTask HandleBatchAsync(IReadOnlyList<IActorMessage> messages, ActorThreadId thread, CancellationToken token)
        {
            Sizes.Enqueue(messages.Count); Entered.TrySetResult();
            await Release.Task;
            token.ThrowIfCancellationRequested();
            if (Fail) throw new InvalidOperationException("Injected batch commit failure.");
            foreach (var message in messages) Committed.Enqueue(((Message)message).Sequence);
        }
    }
    class Message(int sequence) : IActorMessage, IActorDeliveryCompletion
    {
        public Guid SourceGenerationId { get; init; }
        public string? SourceDataset => "GLBX.MDP3";
        public int Sequence => sequence;
        public ActorSubject Subject => new(ActorType.Realtime, "BatchSchedulerTest", "Run", "same");
        public ActorSubject ReplySubject { get; set; }
        public readonly ConcurrentQueue<bool> Outcomes = new();
        public readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Disposals;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => null;
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public void Dispose() { Interlocked.Increment(ref Disposals); Disposed.TrySetResult(); }
        public ValueTask CompleteDeliveryAsync(bool succeeded) { Outcomes.Enqueue(succeeded); return ValueTask.CompletedTask; }
    }
    sealed class BrokerMessage(int sequence) : Message(sequence),IRealtimeBrokerDelivery
    {
        public int Fenced;
        public ValueTask CompleteFencedAsync(){Fenced++;return ValueTask.CompletedTask;}
    }
    sealed class BrokerBatch(BrokerMessage[] children) : IRealtimeBrokerBatch
    {
        public BrokerMessage[] Children=>children;
        public IReadOnlyList<IActorMessage> Messages=>children;
        public ActorSubject Subject=>children[0].Subject;
        public ActorSubject ReplySubject{get;set;}
        public int Disposals,FailedSettlements;
        public TaskCompletionSource Disposed{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TEvent? AsEvent<TEvent>() where TEvent:class,IEvent=>null;
        public TCommand? AsCommand<TCommand>() where TCommand:class,ICommand=>null;
        public TQuery? AsQuery<TQuery,TResult>() where TQuery:class,IQuery<TResult> where TResult:class=>null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult:class=>ValueTask.CompletedTask;
        public void ReleasePayload(){}
        public void Dispose(){Disposals++;foreach(var child in children)if(child.Disposals==0)child.Dispose();Disposed.TrySetResult();}
        public async ValueTask CompleteDeliveryAsync(bool succeeded){if(!succeeded)FailedSettlements++;foreach(var child in children)if(child.Outcomes.IsEmpty&&child.Fenced==0)await child.CompleteDeliveryAsync(succeeded);}
        public async ValueTask CompleteFencedAsync(){foreach(var child in children)await child.CompleteFencedAsync();}
    }
    sealed class NonBatchActor(BatchActor inner):IActor
    {
        public ActorMailboxId Id=>inner.Id;public IActorMailbox Mailbox=>inner.Mailbox;public bool IsRunning=>true;
        public ValueTask StartAsync(IActorSupervisor supervisor)=>ValueTask.CompletedTask;
        public ValueTask StopAsync()=>ValueTask.CompletedTask;
        public ValueTask HandleMessageAsync(IActorMessage message)=>inner.HandleMessageAsync(message);
    }
    sealed class Guard(Guid generation) : IRealtimeSourceAdmission
    {
        public int Released;
        public bool TryEnter(ActorSubject subject, string? dataset, Guid source, out IDisposable? lease)
        { lease = source == generation ? new Lease(this) : null; return lease is not null; }
        sealed class Lease(Guard owner) : IDisposable { public void Dispose() => Interlocked.Increment(ref owner.Released); }
    }
}
