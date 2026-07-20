using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a registry of actor types that can be used in the application.
/// </summary>
/// <remarks>This class provides a collection of actor types that are registered for use.  It is typically used to
/// manage and access the types of actors available in the system.</remarks>
/// <param name="actorTypes"></param>
public class ActorRegistry(Type[] actorTypes) 
    : IActorRegistry
{
    public Type[] ActorTypes { get; } = actorTypes;
}
