namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Optional nonserialized provenance storage on high-volume messages.</summary>
public interface IMessageOriginOwner
{
    ref MessageOriginSlot MessageOriginSlot { get; }
}

/// <summary>
/// Metadata is excluded from record value equality. A record copy starts unassociated:
/// the owner check prevents a copied slot from inheriting creation time accidentally.
/// Explicit forwarding still uses MessageOrigins.Copy.
/// </summary>
public struct MessageOriginSlot : IEquatable<MessageOriginSlot>
{
    sealed record Association(object Owner, MessageOrigin? Origin);
    Association? state;
    public bool TryGet(object owner, out MessageOrigin? origin)
    {
        var value = Volatile.Read(ref state);
        origin = value?.Origin;
        return value is not null && ReferenceEquals(value.Owner, owner);
    }
    public MessageOrigin? GetOrAdd(object owner, MessageOrigin? origin)
    {
        while (true)
        {
            var current = Volatile.Read(ref state);
            if (current is not null && ReferenceEquals(current.Owner, owner)) return current.Origin;
            var next = new Association(owner, origin);
            if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, current), current)) return origin;
        }
    }
    public readonly bool Equals(MessageOriginSlot other) => true;
    public override readonly bool Equals(object? obj) => obj is MessageOriginSlot;
    public override readonly int GetHashCode() => 0;
    public static bool operator ==(MessageOriginSlot left, MessageOriginSlot right) => true;
    public static bool operator !=(MessageOriginSlot left, MessageOriginSlot right) => false;
}
