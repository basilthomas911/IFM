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

    static MessagePackSerializerOptions Options => TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Options;

    NatsMessagePackSerializer()
    {
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
            return default;
        try
        {
            var value = MessagePackSerializer.Deserialize<T>(buffer, Options);
            NatsMessagingMetrics.RecordTypedDeserialization();
            return value;
        }
        catch
        {
            NatsMessagingMetrics.RecordSerializationFailure();
            throw;
        }
    }

    public void Serialize(IBufferWriter<byte> bufferWriter, T value)
    {
        try
        {
            MessagePackSerializer.Serialize(bufferWriter, value, Options);
            NatsMessagingMetrics.RecordTypedSerialization();
        }
        catch
        {
            NatsMessagingMetrics.RecordSerializationFailure();
            throw;
        }
    }

    public INatsSerializer<T> CombineWith(INatsSerializer<T> next)
        => throw new NotSupportedException("MessagePack serializer composition is not supported.");
}
