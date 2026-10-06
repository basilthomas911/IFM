using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a factory for creating instances of actors based on their type.
/// </summary>
/// <remarks>This interface provides a mechanism for resolving and creating actor instances dynamically at
/// runtime. Implementations of this interface are responsible for managing the lifecycle and dependencies of the
/// created actors, if applicable.</remarks>
public interface IActorFactory
{
    IActor GetActor(Type actorType);
    /// <summary>Constructs a fresh disposable realtime actor and its actor-owned dependencies.</summary>
    IActor CreateRealtimeReplacement(Type actorType)
        => throw new NotSupportedException("Fresh realtime actor construction is not configured.");
}
