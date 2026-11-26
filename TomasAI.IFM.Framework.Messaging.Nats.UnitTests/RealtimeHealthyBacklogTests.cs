using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeHealthyBacklogTests
{
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Healthy_successors_run_promptly_but_failed_head_keeps_its_backoff_and_order(RealtimeMailboxStorage storage)
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-healthy-backlog-"+Guid.NewGuid().ToString("N"));
        var options=new RealtimeMailboxOptions{Enabled=true,CapacityQualified=true,Storage=storage,Directory=root,
            Epoch="healthy_"+Guid.NewGuid().ToString("N"),FailureRedeliveryDelay=TimeSpan.FromSeconds(5),DeliveryConcurrency=2};
        var subject=new ActorSubject(ActorType.Realtime,"HealthyBacklog","H1","ES");
        var seen=new ConcurrentQueue<(string Verb,long At)>();var failedAttempts=0;
        var firstFailure=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var supervisor=Substitute.For<IActorSupervisor>();var actor=Substitute.For<IActor>();
        var mailbox=Substitute.For<IActorMailbox>();var queues=Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox);mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{{subject.ActorId,actor}});
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(),Arg.Any<ActorSubject>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var message=call.Arg<IActorMessage>();var verb=message.Subject.Verb;
            seen.Enqueue((verb,Stopwatch.GetTimestamp()));
            var success=verb!="F1"||Interlocked.Increment(ref failedAttempts)>1;
            ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(success).GetAwaiter().GetResult();message.Dispose();
            if(!success)firstFailure.TrySetResult();
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        try
        {
            await using var manager=new NatsConnectionManager();
            await using var hub=new DurableRealtimeMailboxHub(options,new NatsProducerOptions{Url=Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!},manager,NullLogger<DurableRealtimeMailboxHub>.Instance);
            await hub.StartAsync(supervisor);
            var started=Stopwatch.GetTimestamp();
            await hub.CaptureBatchAsync(Enumerable.Range(1,8).Select(i=>new RealtimeSourcePublication(subject with{Verb="H"+i},Guid.NewGuid(),new byte[]{1})).ToArray());
            using(var fast=new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                while(seen.Count<8||hub.PendingSourceCount>0)await Task.Delay(5,fast.Token);
            Assert.True(Stopwatch.GetElapsedTime(started)<options.FailureRedeliveryDelay,"Healthy backlog inherited the configured failure delay.");
            Assert.Equal(Enumerable.Range(1,8).Select(i=>"H"+i),seen.Select(item=>item.Verb));
            await hub.CaptureBatchAsync(new[]{new RealtimeSourcePublication(subject with{Verb="F1"},Guid.NewGuid(),new byte[]{2}),new RealtimeSourcePublication(subject with{Verb="F2"},Guid.NewGuid(),new byte[]{3})});
            await firstFailure.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(300);
            Assert.Equal(1,Volatile.Read(ref failedAttempts));Assert.DoesNotContain(seen,item=>item.Verb=="F2");
            using(var recovered=new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                while(seen.Count<11||hub.PendingSourceCount>0)await Task.Delay(5,recovered.Token);
            var tail=seen.Where(item=>item.Verb.StartsWith("F",StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[]{"F1","F1","F2"},tail.Select(item=>item.Verb));
            Assert.True(Stopwatch.GetElapsedTime(tail[0].At,tail[1].At)>=TimeSpan.FromMilliseconds(4750),"Failed head did not retain its backoff.");
            Assert.True(Stopwatch.GetElapsedTime(tail[1].At,tail[2].At)<TimeSpan.FromSeconds(2),"Healthy successor inherited the failed head's backoff.");
        }
        finally{Directory.Delete(root,true);}
    }
}
