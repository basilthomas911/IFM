using Xunit;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;
public sealed class DurableJournalCapacityTests
{
    static string Root(){var root=Path.Combine(Path.GetTempPath(),"ifm-capacity-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"pending"));Directory.CreateDirectory(Path.Combine(root,"done"));return root;}
    [Fact]
    public void Saturation_reserves_settlement_and_history_never_expires_to_make_room()
    {
        var root=Root();try
        {
            var capacity=new DurableJournalCapacity(root,4096,1024);var first=Path.Combine(root,"pending","first.bin");var second=Path.Combine(root,"pending","second.bin");
            capacity.Write(first,32,()=>File.WriteAllBytes(first,new byte[32]));capacity.Write(second,32,()=>File.WriteAllBytes(second,new byte[32]));
            Assert.Equal(4096,capacity.RetainedBytes+capacity.SettlementReservedBytes);var called=false;
            Assert.Throws<IOException>(()=>capacity.Write(Path.Combine(root,"pending","third.bin"),32,()=>called=true));Assert.False(called);
            capacity.Write(first,1024,()=>File.WriteAllBytes(first,new byte[1024]));var done=Path.Combine(root,"done","first.bin");capacity.Move(first,done,()=>File.Move(first,done));
            capacity.Write(second,1024,()=>File.WriteAllBytes(second,new byte[1024]));var secondDone=Path.Combine(root,"done","second.bin");capacity.Move(second,secondDone,()=>File.Move(second,secondDone));
            Assert.Equal(2048,capacity.RetainedBytes);Assert.Equal(0,capacity.SettlementReservedBytes);Assert.Equal(1024,new FileInfo(done).Length);
            var cold=new DurableJournalCapacity(root,4096,1024);Assert.Equal(2048,cold.RetainedBytes);Assert.Throws<IOException>(()=>new DurableJournalCapacity(root,2047,1024));Assert.True(File.Exists(done));
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Cursor_reserve_survives_cold_pending_and_covers_new_metadata_at_exact_bound()
    {
        var root=Root();try
        {
            Directory.CreateDirectory(Path.Combine(root,"cursors"));var pending=Path.Combine(root,"pending","one.bin");var cursor=Path.Combine(root,"cursors","one.cur");
            var capacity=new DurableJournalCapacity(root,2088,1024);capacity.Write(pending,100,()=>File.WriteAllBytes(pending,new byte[100]),cursor,40);
            Assert.Equal(2088,capacity.RetainedBytes+capacity.SettlementReservedBytes);
            var cold=new DurableJournalCapacity(root,2088,1024);cold.ReserveSettlement(pending,cursor,40);
            cold.Write(cursor,40,()=>File.WriteAllBytes(cursor,new byte[40]),settlementOwner:pending);
            Assert.Equal(2088,cold.RetainedBytes+cold.SettlementReservedBytes);
            var final=new DurableJournalCapacity(root,2088,1024);final.ReserveSettlement(pending,cursor,40);Assert.Equal(2088,final.RetainedBytes+final.SettlementReservedBytes);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Failed_write_accounts_orphan_temp_and_does_not_delete_unproven_evidence()
    {
        var root=Root();try
        {
            var pending=Path.Combine(root,"pending","one.bin");var capacity=new DurableJournalCapacity(root,4096,1024);
            Assert.Throws<IOException>(()=>capacity.Write(pending,100,()=>{File.WriteAllBytes(pending+".tmp",new byte[100]);throw new IOException("uncertain replace");}));
            Assert.Equal(100,capacity.RetainedBytes);Assert.Equal(100,new DurableJournalCapacity(root,4096,1024).RetainedBytes);Assert.True(File.Exists(pending+".tmp"));
            capacity.Write(pending,100,()=>File.Move(pending+".tmp",pending));Assert.Equal(100,capacity.RetainedBytes);Assert.Equal(1948,capacity.SettlementReservedBytes);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Thousands_of_completed_receipts_count_against_disk_without_remaining_in_hot_path_index()
    {
        var root=Root();try
        {
            var done=Path.Combine(root,"done");for(var index=0;index<4096;index++)File.WriteAllBytes(Path.Combine(done,index+".bin"),new byte[32]);
            var capacity=new DurableJournalCapacity(root,256*1024,1024);Assert.Equal(4096*32,capacity.RetainedBytes);Assert.Equal(0,capacity.TrackedPathCount);
            var pending=Path.Combine(root,"pending","last.bin");capacity.Write(pending,32,()=>File.WriteAllBytes(pending,new byte[32]));Assert.Equal(1,capacity.TrackedPathCount);
            var final=Path.Combine(done,"last.bin");capacity.Move(pending,final,()=>File.Move(pending,final));Assert.Equal(0,capacity.TrackedPathCount);Assert.Equal(4097*32,capacity.RetainedBytes);
        }finally{Directory.Delete(root,true);}
    }

}
