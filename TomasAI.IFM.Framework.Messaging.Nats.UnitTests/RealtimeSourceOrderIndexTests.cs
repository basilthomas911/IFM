using System.Reflection;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;
public sealed class RealtimeSourceOrderIndexTests
{
    [Fact]
    public async Task Ownership_group_prevalidates_all_original_proofs_and_cold_duplicates_without_partial_acceptance()
    {
        var root=Root();var options=Options(root);var target=Mailbox("GROUP");var first=Source(target);var second=Source(target);
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))
            {
                await store.CaptureBatchAsync([first,second],default);
                await store.RecordDestinationCompletedAsync(first,Key(target),default);
                await Assert.ThrowsAsync<InvalidOperationException>(()=>store.ConfirmBrokerOwnershipBatchAsync([first,second],default).AsTask());
                Assert.Equal(2,store.Pending().Length);
                await store.RecordDestinationCompletedAsync(second,Key(target),default);
                await Assert.ThrowsAsync<InvalidDataException>(()=>store.ConfirmBrokerOwnershipBatchAsync([first,second with {ManifestHash=new string('0',64)}],default).AsTask());
                await Assert.ThrowsAsync<InvalidDataException>(()=>store.ConfirmBrokerOwnershipBatchAsync([first,Source(target)],default).AsTask());
                Assert.Equal(2,store.Pending().Length);
                await Task.WhenAll(store.ConfirmBrokerOwnershipBatchAsync([first,second,first],default).AsTask(),store.ConfirmBrokerOwnershipBatchAsync([second,first],default).AsTask());
                Assert.Empty(store.Pending());
            }
            await using var cold=new RealtimeSourceOutbox(options);
            await cold.ConfirmBrokerOwnershipBatchAsync([second,first],default);
            await Assert.ThrowsAsync<InvalidDataException>(()=>cold.ConfirmBrokerOwnershipBatchAsync([first with {SourceHash=new string('0',64)}],default).AsTask());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>cold.ConfirmBrokerOwnershipBatchAsync(Enumerable.Repeat(first,65).ToArray(),default).AsTask());
            Assert.Empty(cold.Pending());
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Sixty_four_source_capture_destination_and_ownership_groups_use_one_flush_per_durable_file()
    {
        var root=Root();var options=Options(root);options.CaptureBatchSize=64;var target=Mailbox("GROUP64");var sources=Enumerable.Range(0,64).Select(_=>Source(target)).ToArray();
        try
        {
            await using var store=new RealtimeSourceOutbox(options);
            var before=store.WriterFlushCount;await store.CaptureBatchAsync(sources,default);Assert.Equal(1,store.WriterFlushCount-before);
            Task[] proofs;
            var gate=(Lock)typeof(RealtimeSourceOutbox).GetField("gate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;
            // Queue a complete producer group before allowing the writer to choose
            // its durable group, including when it dequeued the first item early.
            gate.Enter();
            try{proofs=sources.Select(source=>store.RecordDestinationCompletedAsync(source,Key(target),default).AsTask()).ToArray();}
            finally{gate.Exit();}
            await Task.WhenAll(proofs);Assert.Equal(2,store.WriterFlushCount-before);
            await store.ConfirmBrokerOwnershipBatchAsync(sources,default);
            // Completion is signalled inside the commit gate; wait for its final
            // proven-empty segment reclamation before inspecting flush metrics.
            gate.Enter();gate.Exit();
            Assert.Equal(4,store.WriterFlushCount-before); // receipt file plus proven-empty active segment retirement
            Assert.Empty(store.Pending());Assert.Equal(0,store.LiveDestinationProofSlots);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Ownership_group_writer_refusal_keeps_original_cold_work_recoverable()
    {
        var root=Root();var options=Options(root);var first=Source();var second=Source();
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))
            {
                await store.CaptureBatchAsync([first,second],default);
                var channel=typeof(RealtimeSourceOutbox).GetField("writes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;var writer=channel.GetType().GetProperty("Writer")!.GetValue(channel)!;
                Assert.True((bool)writer.GetType().GetMethod("TryComplete")!.Invoke(writer,[null])!);
                await Assert.ThrowsAsync<IOException>(()=>store.ConfirmBrokerOwnershipBatchAsync([first,second],default).AsTask());Assert.Equal(2,store.Pending().Length);
            }
            await using var cold=new RealtimeSourceOutbox(options);Assert.Equal(2,cold.Pending().Length);
            await cold.ConfirmBrokerOwnershipBatchAsync([first,second],default);Assert.Empty(cold.Pending());
        }finally{Directory.Delete(root,true);}
    }
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-source-prefix-index",Guid.NewGuid().ToString("N"));
    static ActorSubject Mailbox(string name)=>new(ActorType.Realtime,name,"Updated","ES");
    static string Key(ActorSubject target)=>RealtimeMailboxCodec.MailboxKey(target.ThreadId);
    static CapturedRealtimeSource Source(params ActorSubject[] targets)=>RealtimeMailboxCodec.Capture(new(new(ActorType.Realtime,"Source","Trade","ES"),Guid.NewGuid(),new byte[]{1,2,3}),targets,1024);
    static RealtimeMailboxOptions Options(string root)=>new(){Directory=root,MaximumPayloadBytes=1024,CaptureMaximumDelay=TimeSpan.Zero};
    [Fact]
    public async Task Interleaved_mailbox_prefixes_retire_only_flushed_destinations_and_rebuild_from_unique_cold_proofs()
    {
        var root=Root();var options=Options(root);var a=Mailbox("A");var b=Mailbox("B");var c=Mailbox("C");
        var first=Source(a,b);var unrelated=Source(c);var next=Source(a);var shared=Source(b,c);var tail=Source(a,b);
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))
            {
                await store.CaptureAsync(first,default);await store.CaptureBatchAsync([unrelated,next,shared,tail],default);
                Assert.Equal(3,store.PendingMailboxOrderCount);Assert.Equal(8,store.PendingMailboxOrderReferences);Assert.Equal(0,store.LiveDestinationProofSlots);
                Assert.False(store.CanDeliverOrdered([next],Key(a)));Assert.True(store.CanDeliverOrdered([unrelated,shared],Key(c)));Assert.False(store.CanDeliverOrdered([shared],Key(b)));
                await store.RecordDestinationCompletedAsync(first,Key(a),default);Assert.Equal(7,store.PendingMailboxOrderReferences);Assert.Equal(1,store.LiveDestinationProofSlots);Assert.True(store.CanDeliverOrdered([next,tail],Key(a)));
                await Assert.ThrowsAsync<InvalidOperationException>(()=>store.ConfirmBrokerOwnershipAsync(first,default).AsTask());Assert.False(store.CanDeliverOrdered([shared],Key(b)));
                await store.RecordDestinationCompletedAsync(unrelated,Key(c),default);await store.RecordDestinationCompletedAsync(next,Key(a),default);
                Assert.Equal(5,store.PendingMailboxOrderReferences);Assert.Equal(3,store.LiveDestinationProofSlots);
            }
            // Duplicate valid proof rows can arise from an uncertain prior ACK;
            // cold derivation counts original destination identity exactly once.
            var proofPath=Path.Combine(root,options.Epoch,"sources","delivery-receipts.bin");var proof=File.ReadAllBytes(proofPath)[..128];
            using(var file=new FileStream(proofPath,FileMode.Append,FileAccess.Write)){file.Write(proof);file.Flush(true);}
            await using(var cold=new RealtimeSourceOutbox(options))
            {
                Assert.Equal(3,cold.LiveDestinationProofSlots);Assert.Equal(5,cold.PendingMailboxOrderReferences);Assert.Equal(3,cold.PendingMailboxOrderCount);
                Assert.True(cold.CanDeliverOrdered([tail],Key(a)));Assert.False(cold.CanDeliverOrdered([tail],Key(b)));Assert.True(cold.CanDeliverOrdered([shared],Key(c)));Assert.False(cold.CanDeliverOrdered([tail,tail],Key(a)));
                await cold.RecordDestinationCompletedAsync(first,Key(a),default);Assert.Equal(3,cold.LiveDestinationProofSlots);
                await cold.RecordDestinationCompletedAsync(first,Key(b),default);await cold.ConfirmBrokerOwnershipAsync(first,default);Assert.Equal(2,cold.LiveDestinationProofSlots);
                await cold.ConfirmBrokerOwnershipAsync(unrelated,default);await cold.ConfirmBrokerOwnershipAsync(next,default);Assert.Equal(0,cold.LiveDestinationProofSlots);
                await Task.WhenAll(cold.RecordDestinationCompletedAsync(shared,Key(b),default).AsTask(),cold.RecordDestinationCompletedAsync(shared,Key(c),default).AsTask());await cold.ConfirmBrokerOwnershipAsync(shared,default);
                Assert.Equal(2,cold.PendingMailboxOrderReferences);Assert.Equal(0,cold.LiveDestinationProofSlots);
                await Task.WhenAll(cold.RecordDestinationCompletedAsync(tail,Key(a),default).AsTask(),cold.RecordDestinationCompletedAsync(tail,Key(b),default).AsTask());Assert.Equal(0,cold.PendingMailboxOrderReferences);Assert.Equal(0,cold.PendingMailboxOrderCount);Assert.Equal(2,cold.LiveDestinationProofSlots);
                await cold.ConfirmBrokerOwnershipAsync(tail,default);Assert.Equal(0,cold.LiveDestinationProofSlots);Assert.Empty(cold.Pending());
            }
            await using var finished=new RealtimeSourceOutbox(options);Assert.Equal(0,finished.PendingMailboxOrderCount);Assert.Equal(0,finished.PendingMailboxOrderReferences);Assert.Equal(0,finished.LiveDestinationProofSlots);Assert.True((await finished.CaptureAsync(first,default)).AlreadyCaptured);Assert.Equal(0,finished.PendingMailboxOrderCount);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Exact_live_proof_slot_capacity_frees_only_after_full_receipt_then_compaction_and_retry_succeed()
    {
        var root=Root();var options=Options(root);options.MaximumReceiptBytes=1024;var targets=Enumerable.Range(0,9).Select(index=>Mailbox("Reader"+index)).ToArray();var first=Source(targets[..7]);var second=Source(targets[7..]);
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))
            {
                await store.CaptureBatchAsync([first,second],default);await Task.WhenAll(targets[..7].Select(target=>store.RecordDestinationCompletedAsync(first,Key(target),default).AsTask()));
                Assert.Equal(7,store.LiveDestinationProofSlots);await store.RecordDestinationCompletedAsync(second,Key(targets[7]),default);Assert.Equal(8,store.LiveDestinationProofSlots);
                await store.RecordDestinationCompletedAsync(second,Key(targets[7]),default);Assert.Equal(8,store.LiveDestinationProofSlots);
                await Assert.ThrowsAsync<IOException>(()=>store.RecordDestinationCompletedAsync(second,Key(targets[8]),default).AsTask());Assert.Equal(8,store.LiveDestinationProofSlots);Assert.Equal(1,store.PendingMailboxOrderReferences);
                await store.ConfirmBrokerOwnershipAsync(first,default);Assert.Equal(1,store.LiveDestinationProofSlots);
                await store.RecordDestinationCompletedAsync(second,Key(targets[8]),default);Assert.Equal(2,store.LiveDestinationProofSlots);Assert.Equal(0,store.PendingMailboxOrderCount);
                Assert.Equal(256,new FileInfo(Path.Combine(root,options.Epoch,"sources","delivery-receipts.bin")).Length);
                await store.ConfirmBrokerOwnershipAsync(second,default);Assert.Equal(0,store.LiveDestinationProofSlots);
            }
            await using var cold=new RealtimeSourceOutbox(options);Assert.Empty(cold.Pending());Assert.Equal(0,cold.LiveDestinationProofSlots);Assert.Equal(0,cold.PendingMailboxOrderCount);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Closed_writer_rolls_back_unaccepted_capture_index_and_proof_reservation_then_cold_originals_recover()
    {
        var root=Root();var options=Options(root);var a=Mailbox("A");var b=Mailbox("B");var first=Source(a,b);var refused=Source(Mailbox("REFUSED"));
        try
        {
            await using(var store=new RealtimeSourceOutbox(options))
            {
                await store.CaptureAsync(first,default);
                var channel=typeof(RealtimeSourceOutbox).GetField("writes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;var writer=channel.GetType().GetProperty("Writer")!.GetValue(channel)!;
                Assert.True((bool)writer.GetType().GetMethod("TryComplete")!.Invoke(writer,[null])!);
                await Assert.ThrowsAsync<IOException>(()=>store.RecordDestinationCompletedAsync(first,Key(a),default).AsTask());Assert.Equal(0,store.LiveDestinationProofSlots);Assert.Equal(2,store.PendingMailboxOrderReferences);
                await Assert.ThrowsAsync<IOException>(()=>store.CaptureAsync(refused,default).AsTask());Assert.Single(store.Pending());Assert.Equal(2,store.PendingMailboxOrderCount);Assert.Equal(2,store.PendingMailboxOrderReferences);Assert.True(store.CanDeliverOrdered([first],Key(a)));
            }
            await using var cold=new RealtimeSourceOutbox(options);Assert.Single(cold.Pending());Assert.Equal(0,cold.LiveDestinationProofSlots);Assert.Equal(2,cold.PendingMailboxOrderReferences);await cold.RecordDestinationCompletedAsync(first,Key(a),default);Assert.Equal(1,cold.LiveDestinationProofSlots);Assert.Equal(1,cold.PendingMailboxOrderReferences);Assert.Equal(first.ManifestHash,Assert.Single(cold.Pending()).ManifestHash);
        }finally{Directory.Delete(root,true);}
    }
}
