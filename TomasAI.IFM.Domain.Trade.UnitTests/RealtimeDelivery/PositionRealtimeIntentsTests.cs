using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class PositionRealtimeIntentsTests
{
    static PositionRealtimeIntent Input(Guid key,int sequence=1)=>new()
    {
        Key=key,Kind=PositionIntentKind.FuturesPlan,PositionId=IronCondorPositionDeliveryFixture.Position(1).Id,
        RouteGeneration=1,PositionSequence=sequence,Request=[1,2,3]
    };
    [Fact]
    public async Task Restart_preserves_original_request_output_and_completed_receipt()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ifm-position-intents-"+Guid.NewGuid().ToString("N"));
        var key=Guid.NewGuid();var hash=PositionRealtimeIntents.Hash("original");
        try
        {
            await using(var store=new PositionRealtimeIntents(directory))
            {
                var original=await store.CaptureAsync(key,hash,()=>Input(key));
                var retry=await store.CaptureAsync(key,hash,()=>throw new InvalidOperationException("Must not recalculate."));
                Assert.Equal(original.Request,retry.Request);
                await store.AdvanceAsync(key,PositionIntentFlags.OutputCaptured,[4,5],outputOrigin:"origin");
            }
            await using(var recovered=new PositionRealtimeIntents(directory))
            {
                var pending=Assert.Single(recovered.Pending(PositionIntentKind.FuturesPlan));
                Assert.Equal(new byte[]{1,2,3},pending.Request);Assert.Equal(new byte[]{4,5},pending.Output);Assert.Equal("origin",pending.OutputOrigin);
                await Assert.ThrowsAsync<InvalidDataException>(async()=>await recovered.CaptureAsync(key,PositionRealtimeIntents.Hash("changed"),()=>Input(key)));
                await recovered.AdvanceAsync(key,PositionIntentFlags.Published|PositionIntentFlags.Completed);
            }
            await using(var complete=new PositionRealtimeIntents(directory))
            {
                Assert.Empty(complete.Pending(PositionIntentKind.FuturesPlan));
                Assert.True((await complete.FindAsync(key,hash))!.IsCompleted);
            }
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Incomplete_tail_is_recovered_but_checksum_corruption_fails_closed()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ifm-position-intents-"+Guid.NewGuid().ToString("N"));
        var key=Guid.NewGuid();var hash=PositionRealtimeIntents.Hash("original");
        try
        {
            await using(var store=new PositionRealtimeIntents(directory))await store.CaptureAsync(key,hash,()=>Input(key));
            var file=Path.Combine(directory,"intents.pil");var length=new FileInfo(file).Length;
            await using(var append=new FileStream(file,FileMode.Append))await append.WriteAsync(new byte[]{5,6,7});
            await using(var recovered=new PositionRealtimeIntents(directory))Assert.Single(recovered.Pending(PositionIntentKind.FuturesPlan));
            Assert.Equal(length,new FileInfo(file).Length);
            var bytes=await File.ReadAllBytesAsync(file);bytes[^1]^=1;await File.WriteAllBytesAsync(file,bytes);
            Assert.Throws<InvalidDataException>(()=>new PositionRealtimeIntents(directory));
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Group_commits_keep_order_and_capacity_never_discards_required_evidence()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ifm-position-intents-"+Guid.NewGuid().ToString("N"));
        try
        {
            await using var store=new PositionRealtimeIntents(directory,4096,8192);
            var keys=Enumerable.Range(0,8).Select(_=>Guid.NewGuid()).ToArray();
            await Task.WhenAll(keys.Select(key=>store.CaptureAsync(key,PositionRealtimeIntents.Hash(key),()=>Input(key)).AsTask()));
            Assert.Equal(8,store.Pending(PositionIntentKind.FuturesPlan).Length);Assert.InRange(store.GroupFlushes,1,8);
            Assert.True(store.Pending(PositionIntentKind.FuturesPlan).Select(x=>x.Ordinal).SequenceEqual(store.Pending(PositionIntentKind.FuturesPlan).Select(x=>x.Ordinal).Order()));
            var extra=Guid.NewGuid();
            await Assert.ThrowsAsync<IOException>(async()=>await store.CaptureAsync(extra,PositionRealtimeIntents.Hash(extra),()=>Input(extra) with{Request=new byte[4096]}));
            Assert.False(store.IsHealthy);Assert.Equal(8,store.Pending(PositionIntentKind.FuturesPlan).Length);
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
