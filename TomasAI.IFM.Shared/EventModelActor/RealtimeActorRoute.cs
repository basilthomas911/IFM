namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Describes one realtime fan-out destination and, when supplied, projects the source subject to a
/// destination-only scheduling entity identity. The serialized source event payload is not changed.
/// </summary>
public sealed record RealtimeActorRoute
{
    readonly Func<ActorSubject, string>? entityIdProjection;

    /// <summary>Initializes a route that preserves the source entity identity.</summary>
    public RealtimeActorRoute(ActorMailboxId destination)
        : this(destination, null)
    {
    }

    /// <summary>Initializes a route with a destination scheduling-identity projection.</summary>
    public RealtimeActorRoute(
        ActorMailboxId destination,
        Func<ActorSubject, string>? entityIdProjection)
    {
        if (string.IsNullOrWhiteSpace(destination.Name))
            throw new ArgumentException("Realtime route destination name is required.", nameof(destination));
        Destination = destination;
        this.entityIdProjection = entityIdProjection;
    }

    /// <summary>Gets the destination actor mailbox.</summary>
    public ActorMailboxId Destination { get; }

    /// <summary>Builds the destination scheduling subject for one source observation.</summary>
    public ActorSubject Resolve(ActorSubject source)
    {
        var entityId = entityIdProjection?.Invoke(source) ?? source.EntityId;
        if (string.IsNullOrWhiteSpace(entityId))
            throw new InvalidOperationException(
                $"Realtime route to '{Destination}' produced an empty scheduling entity identity.");
        return new ActorSubject(
            Destination.ActorType,
            Destination.Name,
            source.Verb,
            entityId);
    }
}
