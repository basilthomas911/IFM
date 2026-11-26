using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeSettlementIncarnationTests
{
    [Fact]
    public async Task Disposal_releases_capture_waiters_before_start_without_accepting_sources()
    {
        await using var manager = new NatsConnectionManager();
        var options = new RealtimeMailboxOptions { Enabled = true, CapacityQualified = true };
        var hub = new DurableRealtimeMailboxHub(options,new NatsProducerOptions { Url = "nats://127.0.0.1:1" },manager,NullLogger<DurableRealtimeMailboxHub>.Instance);
        var source = new RealtimeSourcePublication(new(ActorType.Realtime,"NotStarted","Trade","ES"),Guid.NewGuid(),new byte[]{1});
        var single = hub.CaptureAsync(source).AsTask();
        var batch = hub.CaptureBatchAsync(new[]{source}).AsTask();
        Assert.False(single.IsCompleted); Assert.False(batch.IsCompleted);
        await hub.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>single.WaitAsync(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>batch.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0,hub.PendingSourceCount);
    }
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory, false)]
    [InlineData(RealtimeMailboxStorage.File, false)]
    [InlineData(RealtimeMailboxStorage.Memory, true)]
    [InlineData(RealtimeMailboxStorage.File, true)]
    public async Task Old_ack_cannot_settle_different_source_at_reused_sequence_after_reconstruction(RealtimeMailboxStorage storage, bool batched)
    {
        var root = Path.Combine(Path.GetTempPath(),"ifm-incarnation-fence-"+Guid.NewGuid().ToString("N"));
        var options = new RealtimeMailboxOptions { Enabled=true, CapacityQualified=true, Storage=storage, Directory=root,
            Epoch="fence_"+Guid.NewGuid().ToString("N"), DeliveryConcurrency=2, PublishTimeout=TimeSpan.FromSeconds(3), FailureRedeliveryDelay=TimeSpan.FromMilliseconds(40) };
        var subject = new ActorSubject(ActorType.Realtime,"IncarnationPrice","1","ES");
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = 0; var oldVersion = -1;
        var seen = new ConcurrentDictionary<string,int>(StringComparer.Ordinal);
        var physicalVersions = new ConcurrentDictionary<Guid,int>();
        var supervisor=Substitute.For<IActorSupervisor>();
        var actor=batched?Substitute.For<IActor,IBatchedRealtimeActor>():Substitute.For<IActor>();
        var mailbox=Substitute.For<IActorMailbox>(); var queues=Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{{subject.ActorId,actor}});
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(),Arg.Any<ActorSubject>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var message=call.Arg<IActorMessage>();
            var children=message is IRealtimeBrokerBatch batch?batch.Messages:new[]{message};
            foreach(var child in children)
            {
                seen.AddOrUpdate(child.Subject.Verb,1,(_,count)=>count+1);
                ((IActorDeliveryCompletion)child).CompleteDeliveryAsync(true).GetAwaiter().GetResult();
            }
            message.Dispose(); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        try
        {
            await using var manager=new NatsConnectionManager();
            var connection=new NatsProducerOptions{Url=Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!};
            await using var hub=new DurableRealtimeMailboxHub(options,connection,manager,NullLogger<DurableRealtimeMailboxHub>.Instance);
            hub.BeforePhysicalSettlementAsync=async (_,source,ack,version,token)=>
            {
                if(ack)physicalVersions[source]=version;
                if(source==first&&ack&&Interlocked.CompareExchange(ref hold,1,0)==0)
                { oldVersion=version; entered.TrySetResult(); await release.Task.WaitAsync(token); }
            };
            await hub.StartAsync(supervisor);
            await hub.CaptureAsync(new(subject,first,new byte[]{1}));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8)); // Handler proof is durable; old physical ACK is held.
            await hub.CaptureAsync(new(subject with{Verb="2"},second,new byte[]{2}));
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while(hub.PendingSourceCount!=1||hub.Metrics.PublishedDestinations<2)await Task.Delay(10,timeout.Token);
            Assert.Equal(1,seen.GetValueOrDefault("1"));
            Assert.Equal(0,seen.GetValueOrDefault("2"));
            var js=await manager.GetJetStreamContextAsync(connection.Url);
            var name=options.StreamPrefix+"_"+options.Epoch+"_"+RealtimeMailboxCodec.ActorKey(subject.Name);
            await js.DeleteStreamAsync(name);
            // Rebuilt stream starts with source2 at physical sequence1. The old
            // source1 ACK has the same consumer/sequence spelling but no authority.
            while(true)
            {
                try
                {
                    var stream=await js.GetStreamAsync(name,cancellationToken:timeout.Token);
                    if(hub.Metrics.PublishedDestinations>=3&&stream.Info.State.LastSeq==1&&stream.Info.State.Messages==1)break;
                }
                catch(NATS.Client.JetStream.NatsJSApiException error)when(error.Error.Code==404) { }
                await Task.Delay(10,timeout.Token);
            }
            // Register a new thread mailbox on the same actor after rebuilding.
            // It must share the incarnation, rather than create a private fence.
            await hub.CaptureAsync(new(subject with{Verb="3",EntityId="VX"},third,new byte[]{3}));
            release.TrySetResult();
            while(hub.PendingSourceCount>0||seen.GetValueOrDefault("2")<1||seen.GetValueOrDefault("3")<1||!physicalVersions.ContainsKey(second)||!physicalVersions.ContainsKey(third))await Task.Delay(10,timeout.Token);
            Assert.Equal(0,oldVersion);
            Assert.True(hub.SuppressedPhysicalSettlements>=1);
            Assert.True(physicalVersions[second]>oldVersion); Assert.True(physicalVersions[third]>oldVersion);
            Assert.Equal(1,seen["1"]); Assert.Equal(1,seen["2"]); Assert.Equal(1,seen["3"]);
            Assert.Equal(2,hub.MailboxCount);
        }
        finally { release.TrySetResult(); Directory.Delete(root,true); }
    }
}
