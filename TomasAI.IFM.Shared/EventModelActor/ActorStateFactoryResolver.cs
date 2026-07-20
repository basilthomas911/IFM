using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Resolves instances of actor state types using a specified resolver function.
/// </summary>
/// <remarks>This class provides a mechanism to resolve actor state types dynamically at runtime by invoking a
/// user-defined resolver function. If no resolver function is provided, the default behavior is to return <see
/// langword="null"/> or the default value for the type.</remarks>
/// <param name="resolverFunction">A function that takes a <see cref="Type"/> representing the actor state type and returns an instance of that type.
/// If <see langword="null"/>, the resolver will return the default value for the requested type.</param>
public class ActorStateFactoryResolver(Func<Type, object> resolverFunction = default!)
: IActorStateFactoryResolver
{
    readonly Func<Type, object>? _resolverFunction = resolverFunction;

    /// <summary>
    /// Resolves an instance of the specified type using the configured resolver function.
    /// </summary>
    /// <remarks>The resolution process relies on the delegate provided to the resolver function. If the
    /// resolver function is null or an exception occurs during resolution, the method returns the default value for the
    /// specified type.</remarks>
    /// <param name="genericActorStateType">The type of the object to resolve.</param>
    /// <returns>An instance of the specified type if the resolution is successful; otherwise, the default value for the type.</returns>
    public object Resolve(Type genericActorStateType)
    {
        try
        {
            return _resolverFunction?.Invoke(genericActorStateType)!;
        }
        catch { }
        {
            return default!;
        }
    }

}
