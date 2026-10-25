using System.Collections.Immutable;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>The two persisted metadata fields, independent of the current CLR location.</summary>
public sealed record EventWriteIdentity(string EventName, string EventTypeName);

/// <summary>
/// Freezes legacy write identities before moving event contracts. Registration is
/// additive: a process cannot change an event's write identity after registration.
/// Payload serialization always uses the actual current type.
/// </summary>
public sealed class EventWriteIdentityRegistry(EventTypeRegistry reader, bool useFrozenBaseline)
{
    public EventWriteIdentityRegistry(EventTypeRegistry reader) : this(reader, true) { }
    public static EventWriteIdentityRegistry Default { get; } = new(EventTypeRegistry.Default);
    static readonly Lazy<ImmutableDictionary<string, EventWriteIdentity>> Frozen = new(PersistedIdentityMetadata.ReadWrites);
    readonly object gate = new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<Type, EventWriteIdentity> defaults = new();
    ImmutableDictionary<Type, EventWriteIdentity> identities = ImmutableDictionary<Type, EventWriteIdentity>.Empty;

    public EventWriteIdentity GetIdentity(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        RequireEvent(eventType);
        return Volatile.Read(ref identities).GetValueOrDefault(eventType)
            ?? defaults.GetOrAdd(eventType, type =>
            {
                if (useFrozenBaseline && Frozen.Value.TryGetValue(type.FullName!, out var frozen))
                {
                    RegisterLegacyIdentity(type, frozen.EventName, frozen.EventTypeName);
                    return Volatile.Read(ref identities)[type];
                }
                return new(type.Name, type.AssemblyQualifiedName
                    ?? throw new InvalidOperationException($"No persisted CLR identity for '{type}'."));
            });
    }

    public void RegisterLegacyIdentity(Type eventType, string eventName, string eventTypeName)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        RequireEvent(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        // This phase deliberately cannot enable stable-ID writes.
        if (eventTypeName.Contains('/') || !eventTypeName.Contains(','))
            throw new ArgumentException("A legacy assembly-qualified CLR identity is required.", nameof(eventTypeName));
        var identity = new EventWriteIdentity(eventName, eventTypeName);
        lock (gate)
        {
            var current = identities;
            if (current.TryGetValue(eventType, out var existing) && existing != identity)
                throw new InvalidOperationException($"Write identity for '{eventType}' is already frozen.");
            if (current.Any(pair => pair.Key != eventType && pair.Value.EventTypeName == eventTypeName))
                throw new InvalidOperationException($"Write identity '{eventTypeName}' belongs to another event.");
            // Publish read support first, so no write can emit an unreadable alias.
            reader.RegisterHistoricalIdentity(eventType, eventTypeName);
            Volatile.Write(ref identities, current.SetItem(eventType, identity));
        }
    }

    static void RequireEvent(Type type)
    {
        if (!typeof(IEvent).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            throw new InvalidDataException($"Persisted type '{type}' is not a concrete event.");
    }
}
