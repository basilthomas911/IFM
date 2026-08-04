using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using NATS.Client.Core;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

/// <summary>
/// Serializes a typed payload directly into NATS's pooled output writer.
/// </summary>
/// <typeparam name="T">The payload type carried on the wire.</typeparam>
public sealed class NatsMessagePackSerializer<T> : INatsSerializer<T>
{
    public static NatsMessagePackSerializer<T> Default { get; } = new();

    static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard
            .WithResolver(ContractlessStandardResolver.Instance)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

    NatsMessagePackSerializer()
    {
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
        => buffer.IsEmpty ? default : MessagePackSerializer.Deserialize<T>(buffer, Options);

    public void Serialize(IBufferWriter<byte> bufferWriter, T value)
        => MessagePackSerializer.Serialize(bufferWriter, value, Options);

    public INatsSerializer<T> CombineWith(INatsSerializer<T> next)
        => throw new NotSupportedException("MessagePack serializer composition is not supported.");
}
