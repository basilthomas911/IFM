using MessagePack;
using MessagePack.Resolvers;
using System.Buffers;

namespace TomasAI.IFM.Framework.Serialization;

/// <summary>
/// Binary serializer implementation using MessagePack.
/// </summary>
/// <remarks>
/// Uses Contractless resolver to support POCOs without attributes and LZ4 compression for compact payloads.
/// For non-generic object serialization, the typeless APIs are used to embed type information.
/// </remarks>
public class MessagePackBinarySerializer : IBinarySerializer
{
    /// <summary>
    /// Shared MessagePack options used for typed serialization.
    /// </summary>
    static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard
            .WithResolver(StandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    /// <summary>
    /// Serializes a value of type <typeparamref name="TData"/> to a MessagePack byte array.
    /// </summary>
    /// <typeparam name="TData">The value type to serialize.</typeparam>
    /// <param name="data">The value to serialize.</param>
    /// <returns>The serialized byte array, or null if <paramref name="data"/> is null.</returns>
    public byte[]? Serialize<TData>(TData data)
        => data is null ? null : MessagePackSerializer.Serialize(data, Options);

    /// <summary>
    /// Deserializes a MessagePack byte array to a value of type <typeparamref name="TData"/>.
    /// </summary>
    /// <typeparam name="TData">The destination type.</typeparam>
    /// <param name="data">The MessagePack payload.</param>
    /// <returns>The deserialized value, or default if <paramref name="data"/> is null or empty.</returns>
    public TData? Deserialize<TData>(byte[] data)
    {
        TData? result = default!;
        if (data is not null && data.Length > 0)
        {
            var buffer = new ReadOnlySequence<byte>(data);
            result = buffer.IsEmpty ? default : MessagePackSerializer.Deserialize<TData>(buffer, Options);
        }
        return result;
    }

}
