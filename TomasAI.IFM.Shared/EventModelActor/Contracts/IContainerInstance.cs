namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a container instance that provides functionality to resolve dependencies.
/// </summary>
/// <remarks>This interface is typically used in dependency injection scenarios to retrieve instances of services
/// or objects registered within a container. The generic method <see cref="Resolve{TInstance}"/> allows resolving
/// instances by their type.</remarks>
public interface IContainerInstance
{
    TInstance Resolve<TInstance>() where TInstance : class;
}
