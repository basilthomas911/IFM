using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeSessionArchiveTests
{
    [Fact]
    public async Task Disabled_archive_creates_no_directory_and_cannot_claim_ownership()
    {
        var root=NewRoot();
        await using var archive=new DurableRealtimeSessionArchive(new(){Enabled=false,Directory=root});
        Assert.False(archive.Enabled);Assert.False(Directory.Exists(root));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>archive.RecordAsync(Record()).AsTask());
        Assert.Throws<InvalidOperationException>(()=>archive.ReadCommitted().ToArray());
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_wire_origin_and_first_event_manifest_survive_duplicates_and_cold_reopen(bool knownOrigin)
    {
        var root=NewRoot();var options=Options(root);var record=Record(knownOrigin);
        var expected=record.OriginalWire.ToArray();var first=record.Destinations.ToArray();
        string sourceHash;string manifestHash;string directory;DateTimeOffset? firstAdmission = null;
        try
        {
            await using(var archive=new DurableRealtimeSessionArchive(options))
            {
                directory=archive.SessionDirectory;
                var receipt=await archive.RecordAsync(record);Assert.True(receipt.ArchiveOwnershipOnly);Assert.False(receipt.AlreadyRecorded);
                sourceHash=receipt.ArchiveReceiptSha256;manifestHash=receipt.ManifestSha256;
                var duplicate=await archive.RecordAsync(record with{Destinations=[new(ActorType.Realtime,"OtherConsumer","Update","different")]});
                Assert.True(duplicate.AlreadyRecorded);Assert.Equal(manifestHash,duplicate.ManifestSha256);
                var committed=Assert.Single(archive.ReadCommitted());
                firstAdmission=committed.RecordedAtUtc;Assert.NotNull(firstAdmission);
                AssertRecord(record,committed,expected,first);Assert.Equal(0,archive.PendingRecords);
                Assert.True(archive.RetainedBytes>expected.Length);Assert.True(archive.ReceiptBytes>=148);
            }
            Assert.Contains(Directory.EnumerateFiles(Path.Combine(directory,"sources"),"*.rto"),path=>new FileInfo(path).Length>expected.Length);
            await using(var restored=new DurableRealtimeSessionArchive(options))
            {
                var committed=Assert.Single(restored.ReadCommitted());AssertRecord(record,committed,expected,first);
                Assert.Equal(sourceHash,committed.ArchiveSourceSha256);
                Assert.Equal(firstAdmission,committed.RecordedAtUtc);
                var replay=await restored.RecordAsync(record with{Destinations=[]});
                Assert.True(replay.AlreadyRecorded);Assert.Equal(manifestHash,replay.ManifestSha256);Assert.Single(restored.ReadCommitted());
            }
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public void Archive_admission_metadata_does_not_change_original_hashes_and_legacy_v3_remains_unpaced()
    {
        var value=RealtimeMailboxCodec.CaptureArchive(new(new(ActorType.Realtime,"SessionArchive","Recorded","key"),Guid.NewGuid(),new byte[]{1,2,3}),[],65536);
        var old=RealtimeMailboxCodec.Decode(RealtimeMailboxCodec.Encode(value),65536,true);
        Assert.Null(old.RecordedAtUtc);
        var recorded=new DateTimeOffset(2026,11,25,14,30,0,TimeSpan.Zero);
        var paced=RealtimeMailboxCodec.Decode(RealtimeMailboxCodec.Encode(value with{RecordedAtUtc=recorded}),65536,true);
        Assert.Equal(recorded,paced.RecordedAtUtc);Assert.Equal(old.SourceHash,paced.SourceHash);
        Assert.Equal(old.Key,paced.Key);Assert.Equal(old.ManifestHash,paced.ManifestHash);
        Assert.Throws<InvalidDataException>(()=>RealtimeMailboxCodec.Decode(RealtimeMailboxCodec.Encode(paced),65536));
    }

    [Fact]
    public async Task Changed_wire_under_same_logical_key_is_rejected_without_changing_original_body()
    {
        var root=NewRoot();var record=Record();
        try
        {
            await using var archive=new DurableRealtimeSessionArchive(Options(root));
            await archive.RecordAsync(record);
            await Assert.ThrowsAsync<InvalidDataException>(()=>archive.RecordAsync(record with{OriginalWire=new byte[]{9,8,7}}).AsTask());
            Assert.Equal(record.OriginalWire.ToArray(),Assert.Single(archive.ReadCommitted()).Record.OriginalWire.ToArray());
            Assert.Null(archive.CurrentFailure);Assert.True((await archive.RecordAsync(record)).AlreadyRecorded);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Concurrent_duplicates_have_one_retained_body_and_original_receipt_after_cold_restart()
    {
        var root=NewRoot();var options=Options(root);var record=Record(true);
        try
        {
            await using(var archive=new DurableRealtimeSessionArchive(options))
            {
                var results=await Task.WhenAll(Enumerable.Range(0,32).Select(_=>archive.RecordAsync(record).AsTask()));
                Assert.Single(results,value=>!value.AlreadyRecorded);
                Assert.Single(results.Select(value=>value.ArchiveReceiptSha256).Distinct());
                Assert.Single(archive.ReadCommitted());Assert.Equal(0,archive.PendingRecords);
            }
            await using(var restored=new DurableRealtimeSessionArchive(options))
            {
                Assert.Single(restored.ReadCommitted());Assert.True((await restored.RecordAsync(record)).AlreadyRecorded);
                Assert.Single(restored.ReadCommitted());Assert.Equal(0,restored.PendingRecords);
            }
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Definite_receipt_capacity_refusal_preserves_committed_body_and_duplicate_recovery()
    {
        var root=NewRoot();var options=Options(root);options.MaximumReceiptBytes=148;var record=Record();
        try
        {
            await using(var archive=new DurableRealtimeSessionArchive(options))
            {
                await archive.RecordAsync(record);
                await Assert.ThrowsAsync<IOException>(()=>archive.RecordAsync(record with{LogicalSourceKey="second",EventId=Guid.NewGuid(),Sequence=2}).AsTask());
                Assert.Equal(0,archive.PendingRecords);Assert.True((await archive.RecordAsync(record)).AlreadyRecorded);
                Assert.Single(archive.ReadCommitted());
            }
            await using(var cold=new DurableRealtimeSessionArchive(options))
            {Assert.Single(cold.ReadCommitted());Assert.True((await cold.RecordAsync(record)).AlreadyRecorded);}
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Archive_v3_cannot_be_loaded_as_normal_outbox_or_claim_any_handler_or_broker_success()
    {
        var root=NewRoot();var options=Options(root);var record=Record();string directory;
        try
        {
            await using(var archive=new DurableRealtimeSessionArchive(options))
            {await archive.RecordAsync(record);directory=archive.SessionDirectory;}
            var mailbox=new RealtimeMailboxOptions{Directory=root,Epoch=Path.GetFileName(directory),MaximumPayloadBytes=65536,MaximumOutboxBytes=64L*1024*1024};
            Assert.Throws<InvalidDataException>(()=>new RealtimeSourceOutbox(mailbox));
            await using var archiveJournal=new RealtimeSourceOutbox(mailbox,archiveOnly:true);
            var archived=Assert.Single(archiveJournal.ReadCommittedArchive());
            var wire=RealtimeMailboxCodec.Encode(archived);
            Assert.Equal(4,BitConverter.ToInt32(wire));
            Assert.NotNull(archived.RecordedAtUtc);
            Assert.Throws<InvalidDataException>(()=>RealtimeMailboxCodec.Decode(wire,65536));
            Assert.True(RealtimeMailboxCodec.Decode(wire,65536,true).ArchiveOwnershipOnly);
            var target=RealtimeMailboxCodec.MailboxKey(archived.Destinations[0].ThreadId);
            Assert.Throws<InvalidOperationException>(()=>archiveJournal.IsDestinationCompleted(archived,target));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>archiveJournal.RecordDestinationCompletedAsync(archived,target,default).AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>archiveJournal.ConfirmBrokerOwnershipAsync(archived,default).AsTask());
            var normalRoot=Path.Combine(root,"normal");
            await using var normal=new RealtimeSourceOutbox(new(){Directory=normalRoot,Epoch="test",MaximumPayloadBytes=65536,MaximumOutboxBytes=64L*1024*1024});
            await Assert.ThrowsAsync<InvalidDataException>(()=>normal.CaptureAsync(archived,default).AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>normal.ConfirmBrokerOwnershipAsync(archived,default).AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>normal.ConfirmArchiveOwnershipAsync(archived,default).AsTask());
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Cold_archive_recovers_flushed_v3_body_before_the_archival_receipt_was_written()
    {
        var root=NewRoot();var options=Options(root);var record=Record(true);string directory;string sourceHash;
        try
        {
            // Create only the public protocol ownership marker, then seed the
            // exact durable prefix where Capture completed but Confirm did not.
            await using(var empty=new DurableRealtimeSessionArchive(options))directory=empty.SessionDirectory;
            var mailbox=new RealtimeMailboxOptions{Directory=root,Epoch=Path.GetFileName(directory),MaximumPayloadBytes=options.MaximumWireBytes+65536,
                MaximumOutboxBytes=options.MaximumArchiveBytes,MaximumReceiptBytes=options.MaximumReceiptBytes,CaptureBatchSize=16,CaptureMaximumDelay=TimeSpan.Zero};
            var identity=SHA256.HashData(Encoding.UTF8.GetBytes(record.Kind+"|"+record.LogicalSourceKey));
            var subject=new ActorSubject(ActorType.Realtime,"SessionArchive","Recorded",Convert.ToHexStringLower(identity));
            var payload=MessagePackSerializer.Serialize(record with{Destinations=[]});
            var source=RealtimeMailboxCodec.CaptureArchive(new(subject,new Guid(identity.AsSpan(0,16)),payload,record.Origin,record.Dataset,record.GenerationId,record.TraceParent,record.TraceState),
                record.Destinations,mailbox.MaximumPayloadBytes);
            sourceHash=source.SourceHash;
            await using(var prefix=new RealtimeSourceOutbox(mailbox,archiveOnly:true))
            {
                await prefix.CaptureAsync(source,default);
                Assert.Equal(1,prefix.PendingCount);Assert.Equal(0,prefix.ReceiptBytes);Assert.Empty(prefix.ReadCommittedArchive());
            }
            await using(var recovered=new DurableRealtimeSessionArchive(options))
            {
                Assert.Equal(0,recovered.PendingRecords);
                var committed=Assert.Single(recovered.ReadCommitted());
                AssertRecord(record,committed,record.OriginalWire.ToArray(),record.Destinations);
                Assert.Equal(sourceHash,committed.ArchiveSourceSha256);
                Assert.True((await recovered.RecordAsync(record with{Destinations=[]})).AlreadyRecorded);
                Assert.Single(recovered.ReadCommitted());
            }
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Committed_prefix_racing_thirty_two_records_restores_a_consistent_owned_prefix()
    {
        var root=NewRoot();var options=Options(root);
        try
        {
            await using var archive=new DurableRealtimeSessionArchive(options);
            var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var records=Enumerable.Range(0,32).Select(index=>Record(true) with{LogicalSourceKey="concurrent/"+index,Sequence=index}).ToArray();
            var writers=records.Select(record=>Task.Run(async()=>{await start.Task;return await archive.RecordAsync(record);})).ToArray();
            var capturing=Task.Run(async()=>{await start.Task;return await archive.GetCommittedPrefixAsync();});
            start.SetResult();
            var prefix=await capturing;await Task.WhenAll(writers);
            Assert.InRange(prefix.Records,0,32);Assert.Equal(0,archive.PendingRecords);
            Assert.True(prefix.ArchiveOwnershipOnly);Assert.False(prefix.OwnerSnapshot);Assert.False(prefix.MapperCheckpoint);
            Assert.Equal(prefix.Records*148,prefix.Files.Single(file=>Path.GetFileName(file.RelativePath)=="receipts.bin").Length);
            var expected=archive.ReadCommitted().Take(checked((int)prefix.Records)).Select(entry=>entry.Record.LogicalSourceKey).ToArray();
            var restoredRoot=Path.Combine(root,"restored-prefix");CopyPrefix(prefix,restoredRoot);
            await using var restored=new DurableRealtimeSessionArchive(Options(restoredRoot));
            var actual=restored.ReadCommitted().ToArray();
            Assert.Equal(prefix.Records,actual.LongLength);Assert.Equal(expected,actual.Select(entry=>entry.Record.LogicalSourceKey));
            Assert.Equal(0,restored.PendingRecords);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Fixed_prefix_bytes_and_hashes_remain_exact_after_future_appends_and_restore_no_future_records()
    {
        var root=NewRoot();var options=Options(root);
        try
        {
            await using var archive=new DurableRealtimeSessionArchive(options);
            for(var index=0;index<3;index++)await archive.RecordAsync(Record(true) with{LogicalSourceKey="before/"+index,Sequence=index});
            var prefix=await archive.GetCommittedPrefixAsync();Assert.Equal(3,prefix.Records);
            var original=prefix.Files.ToDictionary(file=>file.RelativePath,file=>ReadPrefix(prefix,file));
            for(var index=0;index<32;index++)await archive.RecordAsync(Record(true) with{LogicalSourceKey="future/"+index,Sequence=100+index});
            foreach(var file in prefix.Files)
            {
                var current=ReadPrefix(prefix,file);Assert.Equal(original[file.RelativePath],current);
                Assert.Equal(SHA256.HashData(original[file.RelativePath]),SHA256.HashData(current));
            }
            var restoredRoot=Path.Combine(root,"restored-fixed-prefix");CopyPrefix(prefix,restoredRoot);
            await using var restored=new DurableRealtimeSessionArchive(Options(restoredRoot));
            var records=restored.ReadCommitted().ToArray();Assert.Equal(3,records.Length);
            Assert.Equal(new[]{"before/0","before/1","before/2"},records.Select(entry=>entry.Record.LogicalSourceKey));
            Assert.DoesNotContain(records,entry=>entry.Record.LogicalSourceKey.StartsWith("future/",StringComparison.Ordinal));
            Assert.True(prefix.ArchiveOwnershipOnly);Assert.False(prefix.OwnerSnapshot);Assert.False(prefix.MapperCheckpoint);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Concurrent_disposal_returns_receipts_only_for_durably_restorable_records()
    {
        var root=NewRoot();var options=Options(root);var archive=new DurableRealtimeSessionArchive(options);
        try
        {
            var records=Enumerable.Range(0,32).Select(index=>Record() with{LogicalSourceKey="dispose/"+index,Sequence=index}).ToArray();
            var attempts=records.Select(async record=>
            {
                try{await archive.RecordAsync(record);return record.LogicalSourceKey;}
                catch(ObjectDisposedException){return null;}
            }).ToArray();
            await archive.DisposeAsync();var acknowledged=(await Task.WhenAll(attempts)).Where(key=>key is not null).ToArray();
            await Assert.ThrowsAsync<ObjectDisposedException>(()=>archive.RecordAsync(Record()).AsTask());
            await using var restored=new DurableRealtimeSessionArchive(options);
            var committed=restored.ReadCommitted().Select(entry=>entry.Record.LogicalSourceKey).ToArray();
            Assert.All(acknowledged,key=>Assert.Contains(key,committed));
            Assert.Equal(acknowledged.Length,committed.Length);Assert.Equal(0,restored.PendingRecords);
        }
        finally{await archive.DisposeAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    static byte[] ReadPrefix(RealtimeSessionArchivePrefix prefix,RealtimeSessionArchivePrefixFile file)
    {
        using var source=new FileStream(Path.Combine(prefix.SessionDirectory,file.RelativePath),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var bytes=new byte[checked((int)file.Length)];source.ReadExactly(bytes);return bytes;
    }
    static void CopyPrefix(RealtimeSessionArchivePrefix prefix,string targetRoot)
    {
        var directory=Path.Combine(targetRoot,Path.GetFileName(prefix.SessionDirectory));
        foreach(var file in prefix.Files)
        {
            var destination=Path.Combine(directory,file.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination,ReadPrefix(prefix,file));
        }
    }

    static string NewRoot()=>Path.Combine(Path.GetTempPath(),"ifm-session-archive-"+Guid.NewGuid().ToString("N"));
    static RealtimeSessionArchiveOptions Options(string root)=>new(){Enabled=true,Directory=root,SessionId="20261125-fixture",MaximumWireBytes=4096,MaximumArchiveBytes=64L*1024*1024,MaximumReceiptBytes=1024*1024};
    static RealtimeSessionArchiveRecord Record(bool known=false)=>new()
    {
        Kind="NativeFrame",LogicalSourceKey="worker/generation/17",Subject="Event.Futures.SessionStatistics.ESZ6",
        OriginalWire=new byte[]{0,255,1,128,2,0,3},Dataset="GLBX.MDP3",WorkerInstanceId=Guid.NewGuid(),GenerationId=Guid.NewGuid(),Sequence=17,EventId=Guid.NewGuid(),
        Origin=known?new(new DateTimeOffset(2026,11,25,15,0,0,TimeSpan.Zero),"replay-exact",Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()):null,
        TraceParent=known?"00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01":null,TraceState=known?"ifm=original":null,
        Destinations=[new(ActorType.Event,"FuturesEodDataEvent","SessionStatistics","ESZ6"),new(ActorType.Realtime,"TickAggregationRealtime","Update","ESZ6")]
    };
    static void AssertRecord(RealtimeSessionArchiveRecord expected,RealtimeSessionArchiveEntry actual,byte[] wire,ActorSubject[] destinations)
    {
        Assert.Equal(1,actual.Ordinal);Assert.Equal(wire,actual.Record.OriginalWire.ToArray());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(wire)),actual.OriginalWireSha256);
        Assert.Equal(expected.Origin,actual.Record.Origin);Assert.Equal(expected.Subject,actual.Record.Subject);Assert.Equal(expected.Kind,actual.Record.Kind);
        if(expected.TraceParent is null)Assert.True(string.IsNullOrEmpty(actual.Record.TraceParent));else Assert.Equal(expected.TraceParent,actual.Record.TraceParent);
        if(expected.TraceState is null)Assert.True(string.IsNullOrEmpty(actual.Record.TraceState));else Assert.Equal(expected.TraceState,actual.Record.TraceState);
        Assert.Equal(expected.Dataset,actual.Record.Dataset);Assert.Equal(expected.WorkerInstanceId,actual.Record.WorkerInstanceId);Assert.Equal(expected.GenerationId,actual.Record.GenerationId);
        Assert.Equal(expected.Sequence,actual.Record.Sequence);Assert.Equal(expected.EventId,actual.Record.EventId);Assert.Equal(expected.LogicalSourceKey,actual.Record.LogicalSourceKey);
        Assert.Equal(destinations,actual.Record.Destinations);
    }
}
