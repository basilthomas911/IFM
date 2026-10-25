using MessagePack;
using Provider = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer;
namespace TomasAI.IFM.Application.Serialization;
/// <summary>Application encoding boundary. Delegation preserves persisted bytes, hashes and compression limits.</summary>
public sealed class MessagePackBinarySerializer
{
    public static MessagePackBinarySerializer Shared { get; } = new();
    public static MessagePackSerializerOptions Options => Provider.Options;
    public static MessagePackSerializerOptions ContentOptions => Provider.ContentOptions;
    public byte[]? Serialize<T>(T value) => Provider.Shared.Serialize(value);
    public T? Deserialize<T>(byte[] value) => Provider.Shared.Deserialize<T>(value);
    public T? Deserialize<T>(ReadOnlyMemory<byte> value) => Provider.Shared.Deserialize<T>(value);
    public static int MeasureContent<T>(T value) => Provider.MeasureContent(value);
    public static int MeasureContent(Type type, object value) => Provider.MeasureContent(type, value);
    public static int MeasureEncoded<T>(T value) => Provider.MeasureEncoded(value);
    public static byte[] SerializeHistoricalContent<T>(T value) => Provider.SerializeHistoricalContent(value);
}
