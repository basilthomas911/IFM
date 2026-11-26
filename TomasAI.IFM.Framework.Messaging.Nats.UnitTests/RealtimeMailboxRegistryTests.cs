using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;
public sealed class RealtimeMailboxRegistryTests
{
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-registry-test",Guid.NewGuid().ToString("N"));
    static ActorSubject Target(string name="Retained",string entity="ES")=>new(ActorType.Realtime,name,"Updated",entity);
    static CapturedRealtimeSource Source(params ActorSubject[] targets)=>RealtimeMailboxCodec.Capture(new(new(ActorType.Realtime,"Producer","Price","ES"),Guid.NewGuid(),new byte[]{1,2,3},Dataset:"GLBX.MDP3",SourceGenerationId:Guid.NewGuid()),targets,1024);
    static RealtimeMailboxOptions Options(string root)=>new(){Enabled=true,CapacityQualified=true,Directory=root,MaximumPayloadBytes=1024,CaptureMaximumDelay=TimeSpan.Zero};
    static void Legacy(string directory,ActorThreadId identity,string? filename=null,int version=2,bool trailing=false)
    {
        Directory.CreateDirectory(directory);using var stream=File.Create(Path.Combine(directory,(filename??RealtimeMailboxCodec.MailboxKey(identity))+".rmb"));using var writer=new BinaryWriter(stream,Encoding.UTF8);writer.Write(version);writer.Write((int)identity.ActorType);writer.Write(identity.Name);writer.Write(identity.EntityId);if(trailing)writer.Write(1);
    }
    [Fact]
    public async Task Three_thousand_required_partitions_recover_in_original_capture_order_without_per_mailbox_files()
    {
        var root=Root();var options=Options(root);options.MaximumPendingSources=2048;var registry=Path.Combine(root,options.Epoch,"mailboxes");var captures=Enumerable.Range(0,1000).Select(index=>Source(Enumerable.Range(0,3).Select(actor=>Target("Reader"+actor,"Contract"+index)).ToArray())).ToArray();
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))foreach(var group in captures.Chunk(64))await store.CaptureBatchAsync(group,default);
            Assert.False(Directory.Exists(registry));
            await using var cold=new RealtimeSourceOutbox(options);var identities=RealtimeMailboxRegistry.Recover(registry,cold.Pending(),3000);
            Assert.Equal(3000,identities.Length);Assert.Equal(captures.SelectMany(source=>source.Destinations).Select(target=>target.ThreadId),identities);
            Assert.Throws<IOException>(()=>RealtimeMailboxRegistry.Recover(registry,cold.Pending(),2999));Assert.False(Directory.Exists(registry));
            Assert.Equal(captures.Select(source=>source.ManifestHash),cold.Pending().Select(source=>source.ManifestHash));
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Original_legacy_registry_remains_byte_compatible_and_deduplicates_exact_frozen_recipient()
    {
        var root=Root();var target=Target();Legacy(root,target.ThreadId);var path=Assert.Single(Directory.GetFiles(root));var bytes=File.ReadAllBytes(path);
        try{var recovered=RealtimeMailboxRegistry.Recover(root,[Source(target),Source(Target("Other"))],3);Assert.Equal(2,recovered.Length);Assert.Equal(target.ThreadId,recovered[0]);Assert.Equal(bytes,File.ReadAllBytes(path));}
        finally{Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("version")][InlineData("identity")][InlineData("trailing")][InlineData("torn")]
    public void Invalid_legacy_registry_fails_closed_without_rewriting_original(string fault)
    {
        var root=Root();Legacy(root,Target().ThreadId,fault=="identity"?new string('0',64):null,fault=="version"?9:2,fault=="trailing");var path=Assert.Single(Directory.GetFiles(root));if(fault=="torn"){var original=File.ReadAllBytes(path);File.WriteAllBytes(path,original[..^1]);}var bytes=File.ReadAllBytes(path);
        try{Assert.True(Record.Exception(()=>RealtimeMailboxRegistry.Recover(root,[],3000)) is InvalidDataException or EndOfStreamException);Assert.Equal(bytes,File.ReadAllBytes(path));}finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Archive_only_manifest_cannot_authorize_live_partition_recovery()
    {
        var root=Root();try{Assert.Throws<InvalidDataException>(()=>RealtimeMailboxRegistry.Recover(root,[Source(Target()) with{ArchiveOwnershipOnly=true}],10));}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public void Similar_names_and_entities_keep_distinct_digest_keys_and_exact_duplicate_destinations_deduplicate()
    {
        var root=Root();var first=Target("A|B","C");var second=Target("A","B|C");
        Assert.NotEqual(RealtimeMailboxCodec.MailboxKey(first.ThreadId),RealtimeMailboxCodec.MailboxKey(second.ThreadId));
        var restored=RealtimeMailboxRegistry.Recover(root,[Source(first,second,first)],2);Assert.Equal(new[]{first.ThreadId,second.ThreadId},restored);
    }
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory,false,false)][InlineData(RealtimeMailboxStorage.File,false,false)]
    [InlineData(RealtimeMailboxStorage.Memory,true,false)][InlineData(RealtimeMailboxStorage.File,true,false)]
    [InlineData(RealtimeMailboxStorage.Memory,false,true)][InlineData(RealtimeMailboxStorage.File,false,true)]
    public async Task Broker_accepted_source_with_no_recorded_PubACK_recovers_original_manifest_or_refuses_wrong_stream_owner_or_bounds(RealtimeMailboxStorage storage,bool wrongOwner,bool wrongSize)
    {
        var root=Root();var options=Options(root);options.Storage=storage;options.Epoch="cold_"+Guid.NewGuid().ToString("N");var target=Target();var captured=Source(target);var key=RealtimeMailboxCodec.MailboxKey(target.ThreadId);
        var connection=new NatsProducerOptions{Url=Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")!};
        var supervisor=Substitute.For<IActorSupervisor>();var actor=Substitute.For<IActor>();var mailbox=Substitute.For<IActorMailbox>();var queues=Substitute.For<IActorThreadQueues>();actor.Mailbox.Returns(mailbox);mailbox.ThreadQueues.Returns(queues);supervisor.Children.Returns(new Dictionary<ActorMailboxId,IActor>{{target.ActorId,actor}});
        // Current routing has no route from Producer to Retained. Only the
        // original persisted manifest may authorize this destination.
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);supervisor.ActorExists(Arg.Any<ActorMailboxId>()).Returns(false);
        var seen=new ConcurrentQueue<RealtimeBrokerActorMessage>();queues.TryAdmitAsync(Arg.Any<IActorMessage>(),Arg.Any<ActorSubject>(),Arg.Any<CancellationToken>()).Returns(call=>{var message=(RealtimeBrokerActorMessage)call.Arg<IActorMessage>();seen.Enqueue(message);message.CompleteDeliveryAsync(true).GetAwaiter().GetResult();message.Dispose();return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);});
        try
        {
            await using(var owner=new RealtimeSourceOutbox(options))await owner.CaptureAsync(captured,default);
            // Process died after source ownership and broker acceptance, before
            // any local publication-ACK hint or recipient registry was saved.
            await using var manager=new NatsConnectionManager();var js=await manager.GetJetStreamContextAsync(connection.Url);var actorKey=RealtimeMailboxCodec.ActorKey(target.Name);var streamName=options.StreamPrefix+"_"+options.Epoch+"_"+actorKey;var subject=options.StreamPrefix+"."+options.Epoch+"."+actorKey+"."+key;
            var ownerHash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName+"|"+Path.GetFullPath(root).ToUpperInvariant())));
            await js.CreateStreamAsync(new StreamConfig(streamName,[options.StreamPrefix+"."+options.Epoch+"."+actorKey+".>"]){Storage=storage==RealtimeMailboxStorage.Memory?StreamConfigStorage.Memory:StreamConfigStorage.File,Retention=StreamConfigRetention.Workqueue,Discard=StreamConfigDiscard.New,DiscardNewPerSubject=true,MaxMsgsPerSubject=options.MaximumMessagesPerMailbox,MaxBytes=options.MaximumActorBytes,MaxMsgSize=options.MaximumPayloadBytes+65536+(wrongSize?1024:0),NumReplicas=1,Description="IFM realtime source owner "+(wrongOwner?"wrong-owner":ownerHash)});
            (await js.PublishAsync(subject,RealtimeMailboxCodec.Delivery(captured,target),serializer:new TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsByteArrayMessageSerializer(),opts:new NatsJSPubOpts{MsgId=captured.Key+"."+key})).EnsureSuccess();
            Assert.False(Directory.Exists(Path.Combine(root,options.Epoch,"mailboxes")));
            await using(var hub=new DurableRealtimeMailboxHub(options,connection,manager,NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                if(wrongOwner||wrongSize){await Assert.ThrowsAsync<InvalidOperationException>(()=>hub.StartAsync(supervisor));Assert.Empty(seen);Assert.Equal(1,hub.PendingSourceCount);}
                else
                {
                    await hub.StartAsync(supervisor);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));while(hub.PendingSourceCount!=0)await Task.Delay(10,timeout.Token);
                    Assert.Equal(1,hub.MailboxCount);Assert.Equal(1,hub.Metrics.AcknowledgedDeliveries);Assert.Single(seen);Assert.Equal(target,Assert.Single(seen).Subject);Assert.Equal(captured.Source.Subject,Assert.Single(seen).SourceSubject);Assert.Equal(captured.Source.SourceGenerationId,Assert.Single(seen).SourceGenerationId);
                }
            }
            await using var retained=new RealtimeSourceOutbox(options);
            var receipt=await retained.CaptureAsync(captured,default);Assert.True(receipt.AlreadyCaptured);Assert.Equal(captured.Source.SourceEventId,receipt.SourceEventId);Assert.Equal(captured.SourceHash,receipt.SourceSha256);Assert.Equal(captured.ManifestHash,receipt.ManifestSha256);
            if(wrongOwner||wrongSize){Assert.Equal(captured.SourceHash,Assert.Single(retained.Pending()).SourceHash);Assert.Equal(captured.ManifestHash,Assert.Single(retained.Pending()).ManifestHash);}else Assert.Empty(retained.Pending());
        }finally{Directory.Delete(root,true);}
    }
}
