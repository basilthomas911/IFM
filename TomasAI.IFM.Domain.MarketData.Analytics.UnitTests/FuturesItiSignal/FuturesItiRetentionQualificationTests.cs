using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiRetentionQualificationTests
{
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-iti-retention",Guid.NewGuid().ToString("N"));
    static Guid Id(int value)=>new(value,0,0,new byte[8]);
    [Fact]
    public void Actual_sixteen_MiB_receipt_horizon_refuses_atomically_then_authoritative_active_set_reclamation_restores_capacity()
    {
        const int capacity=16*1024*1024/80;
        var root=Root();var owner=Guid.NewGuid();var payload=new byte[]{1,2,3,4};
        try
        {
            using(var receipts=new FuturesItiRecoveryCoverage(root,owner))
            {
                receipts.RecordBatch(Enumerable.Range(1,capacity).Select(index=>(Id(index),payload)).ToArray());
                Assert.Equal(capacity,receipts.Count);
                var path=Assert.Single(Directory.GetFiles(root,"*.replay-receipts"));Assert.Equal(capacity*80L,new FileInfo(path).Length);
                Assert.Throws<IOException>(()=>receipts.RecordBatch([(Id(capacity+1),payload),(Id(capacity+2),payload)]));
                Assert.Equal(capacity,receipts.Count);Assert.False(receipts.Contains(Id(capacity+1),payload));
                Assert.True(receipts.Contains(Id(1),payload));Assert.True(receipts.Contains(Id(capacity),payload));
                Assert.Throws<InvalidDataException>(()=>receipts.Record(Id(1),[9]));
                // This is the exact active-set compaction primitive; production calls
                // it only after acknowledged WAL segments are durably retired.
                receipts.Retain(new HashSet<Guid>{Id(1),Id(capacity)});
                Assert.Equal(2,receipts.Count);Assert.Equal(160,new FileInfo(path).Length);
                receipts.RecordBatch([(Id(capacity+1),payload),(Id(capacity+2),payload)]);
            }
            using(var cold=new FuturesItiRecoveryCoverage(root,owner))
            {
                Assert.Equal(4,cold.Count);foreach(var id in new[]{1,capacity,capacity+1,capacity+2})Assert.True(cold.Contains(Id(id),payload));
                Assert.False(cold.Contains(Id(2),payload));Assert.Throws<InvalidDataException>(()=>cold.Record(Id(1),[0]));
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Corrupt_or_torn_release_proofs_fail_closed_without_rewriting_authentic_files(bool torn)
    {
        var root=Root();var owner=Guid.NewGuid();
        try
        {
            using(var receipts=new FuturesItiRecoveryCoverage(root,owner))receipts.Record(Id(1),[1,2,3]);
            var path=Assert.Single(Directory.GetFiles(root,"*.replay-receipts"));var bytes=File.ReadAllBytes(path);
            if(torn)bytes=bytes[..^1];else bytes[^1]^=1;File.WriteAllBytes(path,bytes);
            Assert.Throws<InvalidDataException>(()=>new FuturesItiRecoveryCoverage(root,owner));Assert.Equal(bytes,File.ReadAllBytes(path));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task WAL_exact_aggregate_byte_bound_reserves_whole_groups_and_committed_prefix_drain_resumes_without_restart()
    {
        var root=Root();var payload=new byte[988]; // 988 +36 framing = one exact1024-byte segment.
        try
        {
            await using(var wal=new FuturesItiInputWal(root,1024,4096))
            {
                var positions=await wal.AppendBatchAsync([payload,payload,payload,payload],default);
                Assert.Equal(4096,wal.RetainedAndReservedBytes);Assert.Equal(4096,Directory.GetFiles(root,"*.wal").Sum(path=>new FileInfo(path).Length));
                Assert.Throws<IOException>(()=>wal.AppendBatchAsync([payload,payload],default).GetAwaiter().GetResult());
                var rows=new List<FuturesItiInputWal.Record>();await foreach(var row in wal.ReplayAsync())rows.Add(row);Assert.Equal(4,rows.Count);
                await wal.AcknowledgeAsync(positions[1]);Assert.Equal(2048,wal.RetainedAndReservedBytes);
                var next=await wal.AppendBatchAsync([payload,payload],default);Assert.Equal(4096,wal.RetainedAndReservedBytes);
                await wal.AcknowledgeAsync(next[^1]);Assert.Equal(0,wal.RetainedAndReservedBytes); // Includes active fully committed segment.
                await wal.AppendAsync(payload,default);Assert.Equal(1024,wal.RetainedAndReservedBytes);
            }
            await using(var cold=new FuturesItiInputWal(root,1024,4096))
            {
                Assert.Equal(1024,cold.RetainedAndReservedBytes);var rows=new List<byte[]>();await foreach(var row in cold.ReplayAsync())rows.Add(row.Payload);Assert.Single(rows);Assert.Equal(payload,rows[0]);
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Recovered_overcap_WAL_refuses_without_erasing_history_and_larger_authorized_bound_recovers()
    {
        var root=Root();var payload=new byte[988];
        try
        {
            await using(var wal=new FuturesItiInputWal(root,1024,4096))await wal.AppendBatchAsync([payload,payload,payload],default);
            var originals=Directory.GetFiles(root,"*.wal").ToDictionary(path=>path,File.ReadAllBytes);
            Assert.Throws<IOException>(()=>new FuturesItiInputWal(root,1024,2048));
            foreach(var(path,bytes)in originals)Assert.Equal(bytes,File.ReadAllBytes(path));
            await using var recovered=new FuturesItiInputWal(root,1024,4096);Assert.Equal(3072,recovered.RetainedAndReservedBytes);
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Concurrent_WAL_reservations_cannot_overcommit_and_definite_refusals_do_not_poison_writer()
    {
        var root=Root();var payload=new byte[988];
        try
        {
            await using var wal=new FuturesItiInputWal(root,1024,4096);
            var writes=Enumerable.Range(0,32).Select(async _=>{try{return(await wal.AppendAsync(payload,default),(Exception?)null);}catch(Exception error){return(default(FuturesItiInputWal.Position),error);}}).ToArray();
            var outcomes=await Task.WhenAll(writes);Assert.Equal(4,outcomes.Count(row=>row.Item2 is null));Assert.All(outcomes.Where(row=>row.Item2 is not null),row=>Assert.IsType<IOException>(row.Item2));Assert.Equal(4096,wal.RetainedAndReservedBytes);
            var last=outcomes.Where(row=>row.Item2 is null).Select(row=>row.Item1).OrderBy(row=>row.Segment).ThenBy(row=>row.EndOffset).Last();await wal.AcknowledgeAsync(last);
            Assert.Equal(0,wal.RetainedAndReservedBytes);await wal.AppendAsync(payload,default);Assert.Equal(1024,wal.RetainedAndReservedBytes);
        }
        finally{Directory.Delete(root,true);}
    }

}
