using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>
/// Represents a registry of actor types that can be used in the application.
/// </summary>
/// <remarks>This class provides a collection of actor types that are registered for use.  It is typically used to
/// manage and access the types of actors available in the system.</remarks>
/// <param name="actorTypes"></param>
public class ActorRegistry(Type[] actorTypes)
    : IActorRegistry
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    public Type[] ActorTypes { get; } = actorTypes;
}
