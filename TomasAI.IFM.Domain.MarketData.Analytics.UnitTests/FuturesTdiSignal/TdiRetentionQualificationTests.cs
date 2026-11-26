using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.TestSupport;
namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;
public sealed class TdiRetentionQualificationTests
{
    [Fact]
    public async Task Aggregate_history_cap_preserves_original_results_and_pre_reserved_completion_survives_refusal_and_cold_replay()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-tdi-retention",Guid.NewGuid().ToString("N"));const long cap=2*64*1024+8192;FuturesTdiResultReceipt original;
        try
        {
            using(var store=new FuturesTdiResultReceipts(root,cap))
            {
                var fixture=new TdiReceiptFixture.Scenario(store){FailSource=true};await fixture.Start();await Assert.ThrowsAsync<IOException>(()=>fixture.Process(TdiReceiptFixture.Source()).AsTask());original=Assert.Single(store.Pending());
                var second=original with{Generated=original.Generated with{Id=Guid.NewGuid()},Completed=original.Completed with{Id=Guid.NewGuid()}};
                Assert.Throws<IOException>(()=>store.Prepare(second));Assert.Single(store.Pending());store.Advance(original.Generated.Id,TdiReceiptStage.Completed);Assert.Empty(store.Pending());
                var count=1;
                for(;count<64;count++)
                {
                    var next=original with{Generated=original.Generated with{Id=Guid.NewGuid()},Completed=original.Completed with{Id=Guid.NewGuid()}};
                    try{store.Prepare(next);}catch(IOException){break;}store.Advance(next.Generated.Id,TdiReceiptStage.Completed);
                }
                Assert.InRange(count,2,63);Assert.True(store.RetainedBytes+store.SettlementReservedBytes<=cap);Assert.Equal(0,store.SettlementReservedBytes);
                Assert.Equal(MessagePackSerializer.Serialize(original.Generated),MessagePackSerializer.Serialize(store.Find(original.Generated.Id)!.Generated));
            }
            using(var cold=new FuturesTdiResultReceipts(root,cap)){Assert.Empty(cold.Pending());Assert.Equal(TdiReceiptStage.Completed,cold.Find(original.Generated.Id)!.Stage);Assert.Equal(MessagePackSerializer.Serialize(original.Completed),MessagePackSerializer.Serialize(cold.Find(original.Generated.Id)!.Completed));}
            var bytes=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length);Assert.Throws<IOException>(()=>new FuturesTdiResultReceipts(root,bytes-1));
        }
        finally{Directory.Delete(root,true);}
    }
}
