namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Provides a mechanism to resolve actor state factory instances based on the specified generic actor state type.
/// </summary>
/// <remarks>This interface is typically used to resolve dependencies for actor state management in distributed
/// systems. Implementations of this interface should provide the appropriate factory instance for the given actor state
/// type.</remarks>
public interface IActorStateFactoryResolver
{
    object Resolve(Type genericActorStateType);
}
