using System.Buffers;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Binary-only event-log codec. The event registry supplies the concrete type; no typeless payloads.</summary>
public sealed class EventLogMessagePackCodec(bool compressed = false)
{
    static readonly MessagePackSerializerOptions BaseOptions = MessagePackSerializerOptions.Standard.WithResolver(
        CompositeResolver.Create([new ActorEntityIdFormatter()], [ContractlessStandardResolver.Instance]));
    public static EventLogMessagePackCodec Shared { get; } = new();
    readonly MessagePackSerializerOptions _writeOptions = BaseOptions.WithCompression(
        compressed ? MessagePackCompression.Lz4BlockArray : MessagePackCompression.None);
    // The LZ4-aware reader accepts both ordinary MessagePack and LZ4 extension blocks. This keeps replay independent
    // of the writer selected for the current process generation and avoids exception-driven codec probing.
    readonly MessagePackSerializerOptions _readOptions = BaseOptions.WithCompression(MessagePackCompression.Lz4BlockArray);

    // Version 1 envelope: [version, original AggregateId was null, concrete MessagePack event].
    // Some existing transport constructors normalize null metadata; event-log replay must preserve it.
    public byte[] Serialize(IEvent value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(1);
        writer.Write(value.AggregateId is null);
        MessagePackSerializer.Serialize(value.GetType(), ref writer, value, _writeOptions);
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
        var value = (IEvent)MessagePackSerializer.Deserialize(type, ref reader, _readOptions);
        if (!reader.End) throw new InvalidDataException("Trailing bytes in binary event-log payload.");
        if (nullAggregateId) EventInitHelper.SetProperty<string?>(value, nameof(IEvent.AggregateId), null);
        // Match the JSON event-log reader's restoration of authoritative storage metadata.
        EventInitHelper.SetProperty(value, nameof(IEvent.EventId), eventVersion);
        return value;
    }

    /// <summary>
    /// Reads Portfolio events written before their contracts moved to the shared assembly. Those event objects were
    /// contractless string-keyed maps; current event contracts use stable integer-keyed arrays.
    /// </summary>
    public IEvent DeserializeLegacyContractless(string typeName, long eventVersion, byte[] payload)
    {
        var type = Type.GetType(typeName, throwOnError: true)!;
        if (!typeof(IEvent).IsAssignableFrom(type)) throw new InvalidDataException("Registered type is not an event.");

        var reader = new MessagePackReader(payload);
        if (reader.ReadArrayHeader() != 3 || reader.ReadInt32() != 1)
            throw new InvalidDataException("Unknown binary event-log envelope.");
        var nullAggregateId = reader.ReadBoolean();
        var value = (IEvent)ReadStringKeyedObject(type, ref reader);
        if (!reader.End) throw new InvalidDataException("Trailing bytes in binary event-log payload.");
        if (nullAggregateId) EventInitHelper.SetProperty<string?>(value, nameof(IEvent.AggregateId), null);
        EventInitHelper.SetProperty(value, nameof(IEvent.EventId), eventVersion);
        return value;
    }

    object ReadStringKeyedObject(Type type, ref MessagePackReader reader)
    {
        var value = Activator.CreateInstance(type)
            ?? throw new InvalidDataException($"Cannot create legacy event target {type.FullName}.");
        var count = reader.ReadMapHeader();
        for (var index = 0; index < count; index++)
        {
            var name = reader.ReadString();
            var raw = reader.ReadRaw().ToArray();
            var property = name is null ? null : type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.SetMethod is null) continue;
            property.SetValue(value, ReadLegacyValue(property.PropertyType, raw));
        }
        return value;
    }

    object? ReadLegacyValue(Type type, byte[] raw)
    {
        // The original mandate placed strategy-family references at key 19. A short-lived replacement contract used
        // keys 19-20 for retired fields and moved that value to 21, so accept both persisted array layouts.
        if (type.FullName == "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel")
            return ReadLegacyFundMandate(type, raw);
        return MessagePackSerializer.Deserialize(type, raw, _readOptions);
    }

    object ReadLegacyFundMandate(Type type, byte[] raw)
    {
        var reader = new MessagePackReader(raw);
        var count = reader.ReadArrayHeader();
        if (count >= 22) return MessagePackSerializer.Deserialize(type, raw, _readOptions)!;

        var value = Activator.CreateInstance(type)
            ?? throw new InvalidDataException($"Cannot create legacy mandate target {type.FullName}.");
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .ToDictionary(item => item.Key!.Value, item => item.Property);
        var strategyFamilies = type.GetProperty("PermittedTradeStrategyFamilies");

        for (var key = 0; key < count; key++)
        {
            var bytes = reader.ReadRaw().ToArray();
            var property = key == 19 ? strategyFamilies : properties.GetValueOrDefault(key);
            if (property?.SetMethod is null) continue;
            property.SetValue(value, MessagePackSerializer.Deserialize(property.PropertyType, bytes, _readOptions));
        }
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
