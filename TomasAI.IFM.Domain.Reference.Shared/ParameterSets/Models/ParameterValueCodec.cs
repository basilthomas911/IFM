using MessagePack;
using System.Security.Cryptography;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Deterministic binary encoding for typed parameter messages, event facts and content fingerprints.</summary>
public static class ParameterValueCodec
{
    public const string Codec = "typed-messagepack-v1";
    static readonly MessagePackSerializerOptions Options=MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
    public static byte[] Encode(ParameterSetValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return MessagePackSerializer.Serialize(value,Options);
    }
    public static ParameterSetValue Decode(ReadOnlyMemory<byte> bytes)=>MessagePackSerializer.Deserialize<ParameterSetValue>(bytes,Options);
    public static ParameterSetValue Snapshot(ParameterSetValue value)=>Decode(Encode(value));
    public static string Hash(ParameterSetValue value)=>Convert.ToHexStringLower(SHA256.HashData(Encode(value)));
}
