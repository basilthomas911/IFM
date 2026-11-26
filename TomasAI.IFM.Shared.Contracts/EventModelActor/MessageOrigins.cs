using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Associates immutable metadata with payloads without changing their serialized numeric keys.</summary>
/// <remarks>The weak table owns metadata only, never clocks. Durable stores and transports must copy metadata explicitly.</remarks>
public static class MessageOrigins
{
    sealed record Association(MessageOrigin? Origin);
    // Legacy DTOs retain weak-key lifetime semantics. Sharding bounds individual table
    // growth; high-volume DTOs use an owned slot and never enter a weak table.
    static readonly ConditionalWeakTable<object, Association>[] Origins =
        Enumerable.Range(0, 1024).Select(_ => new ConditionalWeakTable<object, Association>()).ToArray();
    static ConditionalWeakTable<object, Association> Table(object message)
        => Origins[unchecked((uint)RuntimeHelpers.GetHashCode(message) * 2654435769u) >> 22];
    static bool TryGet(object message, out MessageOrigin? origin)
    {
        if (message is IMessageOriginOwner owner) return owner.MessageOriginSlot.TryGet(message, out origin);
        var found = Table(message).TryGetValue(message, out var association);
        origin = association?.Origin;
        return found;
    }
    static MessageOrigin? GetOrAdd(object message, MessageOrigin? origin)
        => message is IMessageOriginOwner owner ? owner.MessageOriginSlot.GetOrAdd(message, origin)
            : Table(message).GetValue(message, _ => new Association(origin)).Origin;

    /// <summary>Returns recorded provenance, or null for legacy payloads with unknown origin.</summary>
    public static MessageOrigin? Get(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return TryGet(message, out var value) ? value : null;
    }

    /// <summary>Captures origin at an explicit creation boundary, preserving it on repeated publication.</summary>
    public static MessageOrigin Capture(object message, TimeProvider clock, string clockDomainId = "system",
        Guid? runId = null, Guid? generation = null, Guid? causationId = null)
        => ForPublication(message, clock, clockDomainId, runId, generation, causationId)
            ?? throw new InvalidOperationException("A legacy unknown origin cannot be invented at publication.");

    /// <summary>Captures a newly created message, but preserves explicitly unknown legacy provenance when forwarding.</summary>
    public static MessageOrigin? ForPublication(object message, TimeProvider clock, string clockDomainId = "system",
        Guid? runId = null, Guid? generation = null, Guid? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(clockDomainId);
        if (TryGet(message, out var existing))
        {
            if (existing is { } captured) FillMissingEventTimes(message, captured);
            return existing;
        }
        if (clock is TomasAI.IFM.Shared.Time.IMessageClockIdentity identity)
        {
            if (clockDomainId == "system") clockDomainId = identity.ClockDomainId;
            runId ??= identity.RunId;
            generation ??= identity.Generation;
        }
        return CaptureNew(message, clock, clockDomainId, runId, generation, causationId);
    }

    static MessageOrigin? CaptureNew(object message, TimeProvider clock, string clockDomainId,
        Guid? runId, Guid? generation, Guid? causationId)
    {
        var origin = GetOrAdd(message, new MessageOrigin(clock.GetUtcNow(), clockDomainId, runId, generation, causationId));
        if (origin is not null) FillMissingEventTimes(message, origin);
        return origin;
    }

    /// <summary>Attaches authoritative metadata read from durable storage or transport without restamping.</summary>
    public static T Restore<T>(T message, MessageOrigin? origin) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        var existing = GetOrAdd(message, origin);
        if (origin is not null && existing != origin)
            throw new InvalidOperationException("Message origin cannot change after creation.");
        return message;
    }

    /// <summary>Preserves provenance when a transport forwards a copied DTO; a new derived message uses Create instead.</summary>
    public static T Copy<T>(object source, T copy) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);
        return TryGet(source, out var value) ? Restore(copy, value) : copy;
    }

    /// <summary>Preserves a forwarded command's origin only when producer preparation keeps its existing identity.</summary>
    public static T CopyCommandIfSameIdentity<T>(TomasAI.IFM.Shared.EventSourcing.ICommand source, T copy)
        where T : class, TomasAI.IFM.Shared.EventSourcing.ICommand
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);
        return source.CommandId != Guid.Empty && source.CommandId == copy.CommandId
            && source.CommandName == copy.CommandName ? Copy(source, copy) : copy;
    }

    static void FillMissingEventTimes(object message, MessageOrigin origin)
    {
        if (message is TomasAI.IFM.Shared.EventSourcing.IEvent domainEvent && domainEvent.ReceivedOn == default)
            EventInitHelper.SetProperty(domainEvent, nameof(domainEvent.ReceivedOn), origin.OriginatedAtUtc.UtcDateTime);
        if (message is TomasAI.IFM.Shared.EventSourcing.IErrorEvent error && error.ErrorDate == default
            && message.GetType().GetProperty(nameof(error.ErrorDate))?.CanWrite == true)
            EventInitHelper.SetProperty(error, nameof(error.ErrorDate), origin.OriginatedAtUtc.UtcDateTime);
    }

    /// <summary>Creates and stamps a message using an explicitly supplied clock before staging or publication.</summary>
    public static T Create<T>(TimeProvider clock, Func<T> factory, string clockDomainId = "system",
        Guid? runId = null, Guid? generation = null, Guid? causationId = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        var message = factory() ?? throw new InvalidOperationException("Message factory returned null.");
        Capture(message, clock, clockDomainId, runId, generation, causationId);
        return message;
    }
}
