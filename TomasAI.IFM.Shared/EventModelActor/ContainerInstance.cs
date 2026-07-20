using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents an instance of a dependency injection container that resolves service instances.
/// </summary>
/// <remarks>This class provides a mechanism to resolve instances of services or objects based on their type. It
/// relies on a delegate to perform the resolution, which must be provided during construction.</remarks>
/// <param name="resolver"></param>
public class ContainerInstance(Func<Type, object> resolver) 
    : IContainerInstance
{
    public TInstance Resolve<TInstance>() where TInstance : class
    {
        var result = resolver.Invoke(typeof(TInstance));
        return (result as TInstance)!;
    }
}
