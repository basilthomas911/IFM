using MessagePack;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Futures.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class RoutingRetentionQualificationTests
{
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-routing-retention",Guid.NewGuid().ToString("N"));
    [Fact]
    public void Futures_cap_keeps_completed_history_and_owned_cursor_settlement_survives_refusal_and_restart()
    {
        var root=Root();var first=FuturesRoutingDeliveryFixture.Input(1);var second=FuturesRoutingDeliveryFixture.Input(2);const long cap=16*1024*1024+4096;
        try
        {
            using(var store=new FuturesRoutingReceipts(root,cap))
            {
                store.Prepare(first,[]);var pending=Assert.Single(Directory.GetFiles(Path.Combine(root,"pending"),"*.frt"));var original=File.ReadAllBytes(pending);
                Assert.Throws<IOException>(()=>store.Prepare(second,[]));Assert.Single(store.Pending());
                store.Complete(first.Id);Assert.Empty(store.Pending());var history=Assert.Single(Directory.GetFiles(Path.Combine(root,"history"),"*.frt",SearchOption.AllDirectories));Assert.Equal(original,File.ReadAllBytes(history));
                store.Prepare(second,[]);store.Complete(second.Id);Assert.True(store.IsCompletedSequence(second));Assert.Equal(0,store.SettlementReservedBytes);Assert.True(store.RetainedBytes<=cap);
            }
            using(var cold=new FuturesRoutingReceipts(root,cap)){Assert.True(cold.IsCompletedSequence(second));Assert.Equal(MessagePackSerializer.Serialize(first),MessagePackSerializer.Serialize(cold.Find(first)!.Source));}
            var used=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length);Assert.Throws<IOException>(()=>new FuturesRoutingReceipts(root,used-1));
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Option_cap_preserves_captured_publications_and_history_across_refusal_and_cold_replay()
    {
        var root=Root();var first=FuturesOptionRoutingDeliveryFixture.Input(1);var second=FuturesOptionRoutingDeliveryFixture.Input(2);const long cap=16*1024*1024+4096;
        try
        {
            using(var store=new FuturesOptionRoutingReceipts(root,cap))
            {
                var original=store.Prepare(first,[]);Assert.Throws<IOException>(()=>store.Prepare(second,[]));Assert.Single(store.Pending());
                var id=FuturesOptionRoutingReceipts.Identity(first);store.CapturePublications(id,[]);store.Complete(id);Assert.Empty(store.Pending());
                var next=store.Prepare(second,[]);var nextId=FuturesOptionRoutingReceipts.Identity(second);store.CapturePublications(nextId,[]);store.Complete(nextId);Assert.Equal(0,store.SettlementReservedBytes);Assert.True(store.RetainedBytes<=cap);
            }
            using(var cold=new FuturesOptionRoutingReceipts(root,cap)){Assert.True(cold.Find(first)!.Completed);Assert.True(cold.Find(first)!.PublicationsCaptured);Assert.Equal(MessagePackSerializer.Serialize(first),MessagePackSerializer.Serialize(cold.Find(first)!.Source));}
            var used=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length);Assert.Throws<IOException>(()=>new FuturesOptionRoutingReceipts(root,used-1));
        }finally{Directory.Delete(root,true);}
    }
}
