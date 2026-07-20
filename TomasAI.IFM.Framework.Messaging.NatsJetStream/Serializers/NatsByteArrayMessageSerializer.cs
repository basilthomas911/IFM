using System.Buffers;
using NATS.Client.Core;

namespace TomasAI.IFM.Framework.Messaging.Nats.Serializers;

/// <summary>
/// Provides serialization and deserialization functionality for messages represented as byte arrays.
/// </summary>
/// <remarks>This class implements the <see cref="INatsSerializer{T}"/> interface, enabling the serialization of
/// byte array messages for use with NATS messaging systems. It supports combining serializers, deserializing messages
/// from a binary format, and serializing messages into a binary format.</remarks>
public class NatsByteArrayMessageSerializer : INatsSerializer<byte[]>
{
    /// <summary>
    /// Combines the current serializer with another serializer to create a composite serializer.
    /// </summary>
    /// <param name="next">The next serializer to combine with the current serializer. Cannot be <see langword="null"/>.</param>
    /// <returns>A composite serializer that applies the current serializer followed by the specified <paramref name="next"/>
    /// serializer.</returns>
    /// <exception cref="NotImplementedException"></exception>
    public INatsSerializer<byte[]> CombineWith(INatsSerializer<byte[]> next)
        => throw new NotImplementedException();

    /// <summary>
    /// Converts the specified <see cref="ReadOnlySequence{T}"/> of bytes into an array of bytes.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlySequence{T}"/> containing the bytes to deserialize.</param>
    /// <returns>An array of bytes representing the contents of the <paramref name="buffer"/>,  or <see langword="null"/> if the
    /// sequence is empty.</returns>
    public byte[]? Deserialize(in ReadOnlySequence<byte> buffer)
        => buffer.ToArray();

    /// <summary>
    /// Writes the specified byte array to the provided buffer writer.
    /// </summary>
    /// <param name="bufferWriter">The buffer writer to which the byte array will be written.</param>
    /// <param name="value">The byte array to write to the buffer writer. Cannot be null.</param>
    public void Serialize(IBufferWriter<byte> bufferWriter, byte[] value)
        => bufferWriter.Write(value);

}
