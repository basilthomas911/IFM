using MessagePack;
using System.Buffers;
namespace TomasAI.IFM.Application.MarketData.Remote;
/// <summary>Exact native wire receipt. Success means durable destination capture and required ordered input ownership, not business execution.</summary>
[MessagePackObject]
public sealed record NativeSourceCaptureReceipt([property:Key(0)]int Version,[property:Key(1)]Guid Worker,[property:Key(2)]Guid Generation,
    [property:Key(3)]long Sequence,[property:Key(4)]string WireSha256,[property:Key(5)]bool Captured,[property:Key(6)]string Error);

[MessagePackObject]
public sealed record NativeSourceCaptureHello([property:Key(0)]int Version,[property:Key(1)]string Scope);
[MessagePackObject]
public sealed record NativeSourceCaptureHelloReceipt([property:Key(0)]int Version,[property:Key(1)]string Scope,[property:Key(2)]bool Accepted);
[MessagePackObject]
public sealed record NativeSourceCaptureFrame([property:Key(0)]byte[] Wire,[property:Key(1)]string? OriginJson);
[MessagePackObject]
public sealed record NativeSourceCaptureBatch([property:Key(0)]int Version,[property:Key(1)]NativeSourceCaptureFrame[] Sources);
[MessagePackObject]
public sealed record NativeSourceCaptureBatchReceipt([property:Key(0)]int Version,[property:Key(1)]NativeSourceCaptureReceipt[] Sources);

/// <summary>Bounds the native transport framing before generated DTO formatters allocate arrays or decompress bodies.</summary>
public static class NativeSourceCaptureCodec
{
    public static NativeSourceCaptureBatch DecodeBatch(byte[] wire)
    {
        if(wire.Length is <1 or >512*1024)throw new InvalidDataException("Native source group wire exceeds bounds.");
        var reader=new MessagePackReader(wire);
        if(reader.ReadArrayHeader()!=2||reader.ReadInt32()!=2)throw new InvalidDataException("Unsupported native source group version.");
        var count=reader.ReadArrayHeader();if(count is <1 or >64)throw new InvalidDataException("Native source group count exceeds bounds.");
        var sources=new NativeSourceCaptureFrame[count];long decoded=0;
        for(var index=0;index<count;index++)
        {
            if(reader.ReadArrayHeader()!=2)throw new InvalidDataException("Invalid native source group member.");
            var bytes=reader.ReadBytes()??throw new InvalidDataException("Missing native source wire.");
            if(bytes.Length is <1 or >512*1024)throw new InvalidDataException("Native source member exceeds bounds.");
            var origin=reader.TryReadNil()?null:reader.ReadString();if(origin?.Length>2048)throw new InvalidDataException("Native source origin exceeds bounds.");
            var original=bytes.ToArray();decoded+=ExpandedFrameLength(original);if(decoded>576*1024)throw new InvalidDataException("Native source group decoded bytes exceed bounds.");
            sources[index]=new(original,origin);
        }
        if(!reader.End)throw new InvalidDataException("Native source group has trailing bytes.");return new(2,sources);
    }
    public static long ExpandedFrameLength(byte[] wire)
    {
        if(wire.Length is <1 or >2*1024*1024)throw new InvalidDataException("Native source frame exceeds bounds.");
        var reader=new MessagePackReader(wire);var parts=reader.ReadArrayHeader();
        if(parts==0)throw new InvalidDataException("Empty native source envelope.");
        if(reader.NextMessagePackType==MessagePackType.Extension)
        {
            if(parts is <2 or >65)throw new InvalidDataException("Native compression group exceeds bounds.");
            var extension=reader.ReadExtensionFormat();if(extension.TypeCode!=98)throw new InvalidDataException("Unsupported native compression framing.");
            var sizes=new MessagePackReader(extension.Data);long total=0;var blocks=0;
            while(!sizes.End){var length=sizes.ReadInt32();if(length<0||(total+=length)>576*1024)throw new InvalidDataException("Native source decompression exceeds bounds.");blocks++;}
            if(blocks!=parts-1)throw new InvalidDataException("Native compressed blocks differ from sizes.");
            for(var index=0;index<blocks;index++)if(reader.ReadBytes() is null)throw new InvalidDataException("Missing native compressed block.");
            if(!reader.End)throw new InvalidDataException("Native compressed source has trailing bytes.");return total;
        }
        if(parts!=8||wire.Length>576*1024)throw new InvalidDataException("Invalid or oversized native source envelope.");
        for(var index=0;index<8;index++)reader.Skip();if(!reader.End)throw new InvalidDataException("Native source has trailing bytes.");return wire.Length;
    }
    public static TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope DecodeFrame(byte[] wire)
    {
        _=ExpandedFrameLength(wire);
        var frame=MessagePackSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope>(wire,MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        if(frame.Payload.Length>512*1024)throw new InvalidDataException("Native source payload exceeds supported body bound.");return frame;
    }
}
