using System.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Binary-only event-log codec. The event registry supplies the concrete type; no typeless payloads.</summary>
public sealed class EventLogMessagePackCodec(bool compressed = false)
{
    public static EventLogMessagePackCodec Shared { get; } = new();

    readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard.WithResolver(
        CompositeResolver.Create([new ActorEntityIdFormatter()], [ContractlessStandardResolver.Instance])).WithCompression(
        compressed ? MessagePackCompression.Lz4BlockArray : MessagePackCompression.None);

    // Version 1 envelope: [version, original AggregateId was null, concrete MessagePack event].
    // Some existing transport constructors normalize null metadata; event-log replay must preserve it.
    public byte[] Serialize(IEvent value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(1);
        writer.Write(value.AggregateId is null);
        MessagePackSerializer.Serialize(value.GetType(), ref writer, value, _options);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    public IEvent Deserialize(string typeName, long eventVersion, byte[] payload)
    {
        var type = Type.GetType(typeName, throwOnError: true)!;
        if (!typeof(IEvent).IsAssignableFrom(type)) throw new InvalidDataException("Registered type is not an event.");
        var reader = new MessagePackReader(payload);
        if (reader.ReadArrayHeader() != 3 || reader.ReadInt32() != 1) throw new InvalidDataException("Unknown binary event-log envelope.");
        var nullAggregateId = reader.ReadBoolean();
        var value = (IEvent)MessagePackSerializer.Deserialize(type, ref reader, _options);
        if (!reader.End) throw new InvalidDataException("Trailing bytes in binary event-log payload.");
        if (nullAggregateId) EventInitHelper.SetProperty<string?>(value, nameof(IEvent.AggregateId), null);
        // Match the JSON event-log reader's restoration of authoritative storage metadata.
        EventInitHelper.SetProperty(value, nameof(IEvent.EventId), eventVersion);
        return value;
    }

    // Preserve default(struct) separately from ActorEntityId.Default (whose value is "none").
    // Keep the existing one-field MessagePack array representation.
    internal sealed class ActorEntityIdFormatter : IMessagePackFormatter<ActorEntityId>
    {
        public void Serialize(ref MessagePackWriter writer, ActorEntityId value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(1);
            writer.Write(value.Value);
        }

        public ActorEntityId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.ReadArrayHeader() != 1) throw new InvalidDataException("Invalid actor entity ID.");
            var value = reader.ReadString();
            return value is null ? default : new ActorEntityId(value);
        }
    }
}
