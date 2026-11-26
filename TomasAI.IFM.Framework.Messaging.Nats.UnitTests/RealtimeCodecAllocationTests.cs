using System.Text;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeCodecAllocationTests
{
    static readonly ActorSubject Subject=new(ActorType.Realtime,"CodecPrice","Updated","ES20261218");
    [Theory]
    [InlineData(256*1024)]
    [InlineData(512*1024)]
    public void Large_wire_matches_original_writer_and_decode_owns_its_body_snapshot(int size)
    {
        var input=new byte[size];new Random(1759).NextBytes(input);var expected=input.ToArray();
        var origin=MessageOrigins.ForPublication(new object(),TimeProvider.System,"codec-test",Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
        var source=RealtimeMailboxCodec.Capture(new(Subject,Guid.NewGuid(),input,origin,"GLBX.MDP3",Guid.NewGuid(),
            "00-"+new string('a',32)+"-"+new string('b',16)+"-01","vendor=value"),[Subject,Subject with{Name="CodecIndicator"}],size);
        var wire=RealtimeMailboxCodec.Encode(source);
        Assert.Equal(OriginalEncode(source),wire);
        Assert.Equal(wire,RealtimeMailboxCodec.Delivery(source,Subject));
        var decoded=RealtimeMailboxCodec.Decode(wire,size);
        Array.Fill(wire,(byte)0);Array.Fill(input,(byte)7);
        Assert.Equal(expected,decoded.Source.Payload.ToArray());
        Assert.Equal(expected,source.Source.Payload.ToArray());
        Assert.Equal(source.Key,decoded.Key);Assert.Equal(source.SourceHash,decoded.SourceHash);Assert.Equal(source.ManifestHash,decoded.ManifestHash);
        Assert.Equal(source.Source.SourceEventId,decoded.Source.SourceEventId);Assert.Equal(origin,decoded.Source.Origin);
        Assert.Equal(source.Destinations,decoded.Destinations);
    }
    [Fact]
    public void Decoder_rejects_truncated_or_trailing_body_before_copying()
    {
        var source=RealtimeMailboxCodec.Capture(new(Subject,Guid.NewGuid(),new byte[]{1,2,3}),[Subject],1024);
        var wire=RealtimeMailboxCodec.Encode(source);
        Assert.Throws<EndOfStreamException>(()=>RealtimeMailboxCodec.Decode(wire[..^1],1024));
        Assert.Throws<InvalidDataException>(()=>RealtimeMailboxCodec.Decode([..wire,0],1024));
    }
    [Fact]
    public void Header_bound_applies_to_encode_and_decode_independently_of_body_size()
    {
        var source=RealtimeMailboxCodec.Capture(new(Subject with{EntityId=new string('\u754c',16000)},Guid.NewGuid(),new byte[]{1}),
            [Subject with{EntityId=new string('X',16000)},Subject with{Name="CodecIndicator",EntityId=new string('Y',16000)}],1024);
        Assert.Throws<InvalidDataException>(()=>RealtimeMailboxCodec.Encode(source));
        Assert.Throws<InvalidDataException>(()=>RealtimeMailboxCodec.Decode(OriginalEncode(source,false),1024));
    }
    // Frozen original allocation-heavy v2 encoder retained as the exact wire oracle.
    static byte[] OriginalEncode(CapturedRealtimeSource value,bool enforceBound=true)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);
        writer.Write(2);writer.Write(value.Key);writer.Write(value.SourceHash);writer.Write(value.ManifestHash);
        RealtimeMailboxCodec.WriteSubject(writer,value.Source.Subject);writer.Write(value.Source.SourceEventId.ToByteArray());
        writer.Write(value.Source.Dataset??"");writer.Write(value.Source.SourceGenerationId.ToByteArray());
        writer.Write(value.Source.TraceParent??"");writer.Write(value.Source.TraceState??"");
        writer.Write(value.Source.Origin is not null);if(value.Source.Origin is{} origin)writer.Write(MessageOriginBinary.Serialize(origin));
        writer.Write(value.Destinations.Length);foreach(var target in value.Destinations)RealtimeMailboxCodec.WriteSubject(writer,target);
        writer.Write(value.Source.Payload.Length);writer.Write(value.Source.Payload.Span);writer.Flush();
        if(enforceBound&&memory.Length>value.Source.Payload.Length+65536L)throw new InvalidDataException("Realtime envelope metadata exceeds reserved byte bound.");
        return memory.ToArray();
    }
}
