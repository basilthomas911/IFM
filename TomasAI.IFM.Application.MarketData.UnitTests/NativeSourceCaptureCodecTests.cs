using MessagePack;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class NativeSourceCaptureCodecTests
{
    static DatasetPublicationEnvelope Frame()=>new(){Dataset="GLBX.MDP3",ValueDate=new(2026,11,20),WorkerInstanceId=Guid.NewGuid(),GenerationId=Guid.NewGuid(),ManifestRevision=1,PublicationSequence=7,Kind=DatasetPublicationKind.Trade,Payload=new byte[512]};
    [Fact]
    public void Native_source_group_preserves_original_wire_and_legacy_absent_origin()
    {
        var source=Frame();var wire=MessagePackSerializer.Serialize(source,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        var group=MessagePackSerializer.Serialize(new NativeSourceCaptureBatch(2,[new(wire,null)]));var restored=NativeSourceCaptureCodec.DecodeBatch(group);
        Assert.Equal(wire,restored.Sources[0].Wire);Assert.Null(restored.Sources[0].OriginJson);
        var decoded=NativeSourceCaptureCodec.DecodeFrame(restored.Sources[0].Wire);Assert.Equal(source.WorkerInstanceId,decoded.WorkerInstanceId);Assert.Equal(source.GenerationId,decoded.GenerationId);Assert.Equal(source.Payload,decoded.Payload);Assert.Equal(source.PublicationSequence,decoded.PublicationSequence);
    }
    [Fact]
    public void Invalid_batch_count_version_trailing_bytes_and_wire_size_are_rejected_before_DTO_arrays()
    {
        Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeBatch(MessagePackSerializer.Serialize(new NativeSourceCaptureBatch(9,[]))));
        Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeBatch(MessagePackSerializer.Serialize(new NativeSourceCaptureBatch(2,new NativeSourceCaptureFrame[65]))));
        Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeBatch(new byte[512*1024+1]));
        var valid=MessagePackSerializer.Serialize(new NativeSourceCaptureBatch(2,[new([0x90],null)]));Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeBatch(valid.Concat(new byte[]{0}).ToArray()));
    }
    [Fact]
    public void Sixty_four_high_ratio_frames_cannot_bypass_the_aggregate_decoded_byte_limit()
    {
        var frame=Frame() with{Payload=new byte[16*1024]};var wire=MessagePackSerializer.Serialize(frame,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        var request=new NativeSourceCaptureBatch(2,Enumerable.Range(0,64).Select(_=>new NativeSourceCaptureFrame(wire,null)).ToArray());
        var encoded=MessagePackSerializer.Serialize(request);Assert.True(encoded.Length<512*1024);
        Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeBatch(encoded));
    }
    [Fact]
    public void Oversized_decompression_header_is_rejected_without_allocating_the_claimed_buffer()
    {
        var output=new System.Buffers.ArrayBufferWriter<byte>();var writer=new MessagePackWriter(output);writer.WriteArrayHeader(2);
        var size=new System.Buffers.ArrayBufferWriter<byte>();var sizes=new MessagePackWriter(size);sizes.Write(int.MaxValue);sizes.Flush();
        writer.WriteExtensionFormat(new ExtensionResult(98,size.WrittenSpan.ToArray().AsMemory()));writer.Write(new byte[]{0});writer.Flush();
        Assert.Throws<InvalidDataException>(()=>NativeSourceCaptureCodec.DecodeFrame(output.WrittenSpan.ToArray()));
    }
}
