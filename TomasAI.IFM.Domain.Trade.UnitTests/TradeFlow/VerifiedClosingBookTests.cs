using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class VerifiedClosingBookTests
{
    static readonly DateTime Close=new(2026,11,19,22,0,0,DateTimeKind.Utc);
    const string Header="ts_recv,ts_event,rtype,publisher_id,instrument_id,action,sequence,flags,bid_px_00,ask_px_00,bid_sz_00,ask_sz_00";
    static long Ns(DateTime t)=>(t.Ticks-DateTime.UnixEpoch.Ticks)*100;
    static string Row(int seconds,int flags=128,string action="M",long ask=50000000,uint publisher=1,int receiveSeconds=0)
        =>$"{Ns(Close.AddSeconds(seconds+receiveSeconds))},{Ns(Close.AddSeconds(seconds))},1,{publisher},42,{action},123,{flags},9223372036854775807,{ask},0,18";
    static VerifiedClosingBook Proof(int count)=>new(){Dataset="GLBX.MDP3",PublisherId=1,RangeStartUtc=Close.AddHours(-2),RangeEndUtc=Close.AddMinutes(10),VerifiedThroughUtc=Close,CapturedAtUtc=Close.AddHours(1),ExpectedRecords=count};
    static byte[] Csv(params string[] rows)=>Encoding.UTF8.GetBytes(Header+"\n"+string.Join('\n',rows)+"\n");
    static StoredPositionClosingQuote? Replay(byte[] raw,int count=1,VerifiedClosingBook? proof=null,bool option=true)=>ClosingBookArchive.Reconstruct("EXACT",42,new(2026,11,19),Close,proof??Proof(count),raw,option);

    [Fact]
    public void Standing_book_retains_event_receive_and_sequence_and_excludes_postclose_clear()
    {
        var raw=Csv(Row(-3600),Row(1,action:"R",ask:long.MaxValue));var quote=Replay(raw,2)!;
        Assert.Equal(Close.AddHours(-1),quote.ObservedAtUtc);Assert.Equal(Ns(Close.AddHours(-1)),quote.ReceiveTimestampNanoseconds);
        Assert.Equal(.05m,quote.MarkPrice);Assert.False(quote.HasBid);Assert.Equal(123u,quote.SourceSequence);
        Assert.True(quote.Book!.Covers(Close));Assert.Equal(2,quote.Book.Records);ClosingBookArchive.Verify(quote,raw,true);
        Assert.Null(Replay(raw,2,option:false));
        Assert.Throws<InvalidDataException>(()=>ClosingBookArchive.Verify(quote with{Ask=.10m},raw,true));
    }
    [Theory]
    [InlineData(132,1,1)] // bad book
    [InlineData(0,1,1)] // incomplete event
    [InlineData(128,2,1)] // wrong publisher
    [InlineData(128,1,2)] // truncated archive
    public void Gaps_wrong_publisher_and_truncated_archives_block(int flags,uint publisher,int count)
        =>Assert.Throws<InvalidDataException>(()=>Replay(Csv(Row(-3600,flags,publisher:publisher)),count));
    [Theory]
    [InlineData("R",50000000)]
    [InlineData("C",9223372036854775807)]
    public void Final_clear_or_empty_book_cannot_reuse_an_earlier_quote(string action,long ask)
        =>Assert.Null(Replay(Csv(Row(-3600),Row(-1800,action:action,ask:ask)),2));
    [Fact]
    public void Late_received_preclose_change_is_applied_with_its_original_event_time()
    {
        var quote=Replay(Csv(Row(-3600),Row(-1,ask:100000000,receiveSeconds:2)),2)!;
        Assert.Equal(.10m,quote.Ask);Assert.Equal(Close.AddSeconds(-1),quote.ObservedAtUtc);Assert.Equal(Ns(Close.AddSeconds(1)),quote.ReceiveTimestampNanoseconds);
    }
    [Fact]
    public void Snapshot_can_anchor_an_old_quote_but_old_unflagged_records_cannot()
    {
        var raw=Csv(Row(-10800,flags:160,receiveSeconds:3600));Assert.NotNull(Replay(raw));
        Assert.Throws<InvalidDataException>(()=>Replay(Csv(Row(-10800,receiveSeconds:3600))));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    public void Receive_filtered_boundary_accepts_bounded_late_full_book_updates_without_retiming(int latency)
    {
        var raw=Csv(Row(-7201,receiveSeconds:latency));
        var quote=Replay(raw)!;
        Assert.Equal(Close.AddSeconds(-7201),quote.ObservedAtUtc);
        Assert.Equal(Ns(Close.AddSeconds(-7201+latency)),quote.ReceiveTimestampNanoseconds);
        ClosingBookArchive.Verify(quote,raw,true);
        Assert.Null(Replay(Csv(Row(-7201,receiveSeconds:latency),Row(-1,action:"C",ask:long.MaxValue)),2));
    }
    [Fact]
    public void Existing_thirteen_field_quote_wire_bytes_and_evidence_hash_are_unchanged()
    {
        var q=new StoredPositionClosingQuote{ContractId="TEST",ValueDate=new(2026,11,19),InstrumentId=42,
            EventTimestampNanoseconds=Ns(Close),ReceiveTimestampNanoseconds=Ns(Close)+1,SourceSequence=123,
            Bid=1,Ask=2,BidSize=3,AskSize=4,SourceArchiveSha256=new string('A',64)};
        var legacy=new LegacyQuote(q.EvidenceId,q.ContractId,q.ValueDate,q.InstrumentId,q.EventTimestampNanoseconds,q.ReceiveTimestampNanoseconds,q.SourceSequence,q.Bid,q.Ask,q.BidSize,q.AskSize,q.SourceArchiveSha256,q.HasBid);
        var oldBytes=MessagePackBinarySerializer.Shared.Serialize(legacy);var newBytes=MessagePackBinarySerializer.Shared.Serialize(q);
        Assert.Equal(oldBytes,newBytes);Assert.Equal(SHA256.HashData(oldBytes),SHA256.HashData(newBytes));
        Assert.Equal(q,MessagePackBinarySerializer.Shared.Deserialize<StoredPositionClosingQuote>(oldBytes));
        var book=Replay(Csv(Row(-3600)))!;
        Assert.Equal(book,MessagePackBinarySerializer.Shared.Deserialize<StoredPositionClosingQuote>(MessagePackBinarySerializer.Shared.Serialize(book)));
    }
    [MessagePackObject]
    public sealed record LegacyQuote([property:Key(0)]string EvidenceId,[property:Key(1)]string ContractId,[property:Key(2)]DateOnly ValueDate,
        [property:Key(3)]uint InstrumentId,[property:Key(4)]long Event,[property:Key(5)]long Receive,[property:Key(6)]uint Sequence,
        [property:Key(7)]decimal Bid,[property:Key(8)]decimal Ask,[property:Key(9)]uint BidSize,[property:Key(10)]uint AskSize,
        [property:Key(11)]string Archive,[property:Key(12)]bool HasBid);
}
