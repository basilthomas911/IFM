using MessagePack;
using MessagePack.Resolvers;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

static class EventProjectorOutboxSerializer
{
    static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.Lz4BlockArray);

    public static EventProjectorOutboxMessage Serialize(
        IEvent domainEvent,
        EventProjectorEffectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(identity);
        EventInitHelper.SetProperty(domainEvent, nameof(IEvent.Id), CreateDeterministicEventId(identity.MessageId));
        var eventType = domainEvent.GetType();
        return new EventProjectorOutboxMessage(
            identity,
            eventType.AssemblyQualifiedName
                ?? throw new InvalidOperationException($"Unable to resolve the event type name for '{eventType.FullName}'."),
            MessagePackSerializer.Serialize(eventType, domainEvent, Options));
    }

    public static IEvent Deserialize(string eventTypeName, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);
        var eventType = Type.GetType(eventTypeName, throwOnError: true)!;
        if (!typeof(IEvent).IsAssignableFrom(eventType))
            throw new InvalidOperationException($"Outbox event type '{eventType.FullName}' does not implement {nameof(IEvent)}.");
        return (IEvent)(MessagePackSerializer.Deserialize(eventType, payload, Options)
            ?? throw new InvalidOperationException($"Outbox payload for '{eventType.FullName}' deserialized to null."));
    }

    static Guid CreateDeterministicEventId(string messageId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(messageId), hash);
        return new Guid(hash[..16]);
    }
}
