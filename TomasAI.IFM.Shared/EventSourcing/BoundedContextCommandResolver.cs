using System.Collections.Concurrent;
namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Provides a mechanism to resolve command handler instances for a given type,  either from an internal cache or by
/// invoking a user-defined resolver function.
/// </summary>
/// <remarks>This class is designed to manage the resolution of command handlers, which can be added  explicitly
/// or resolved dynamically using a provided resolver function. Resolved instances  are cached for future use to improve
/// performance.</remarks>
/// <param name="resolverFunction"></param>
public class BoundedContextCommandResolver(Func<Type, object>? resolverFunction) 
    : IBoundedContextCommandResolver
{
    static readonly ConcurrentDictionary<Type, object> _resolverMap = [];
    readonly Func<Type, object>? _resolverFunction = resolverFunction;

    /// <summary>
    /// Adds a command handler to the resolver map.
    /// </summary>
    /// <remarks>If a handler of the specified type already exists in the resolver map, this method does not
    /// overwrite it.</remarks>
    /// <param name="cmdHandlerType">The type of the command handler to add. This cannot be <see langword="null"/>.</param>
    /// <param name="cmdHandler">The instance of the command handler to associate with the specified type. This cannot be <see langword="null"/>.</param>
    public void Add(Type cmdHandlerType, object cmdHandler)
        =>  _resolverMap.TryAdd(cmdHandlerType, cmdHandler);

    /// <summary>
    /// Resolves an instance of the specified command handler type.
    /// </summary>
    /// <remarks>This method attempts to resolve the requested command handler type using an internal resolver
    /// map.  If the type is not found in the map and a resolver function is provided, the function is invoked to 
    /// create the instance, which is then cached for future resolutions.</remarks>
    /// <param name="commandHandlerType">The <see cref="Type"/> of the command handler to resolve.</param>
    /// <returns>An instance of the specified command handler type if it can be resolved; otherwise, <see langword="null"/>.</returns>
    public object? Resolve(Type commandHandlerType)
    {
        try
        {
            if (_resolverMap.TryGetValue(commandHandlerType, out object? value))
                return value;
            if (_resolverFunction is not null)
            {
                var commandHandler = _resolverFunction(commandHandlerType);
                if (commandHandler is not null)
                    _resolverMap.TryAdd(commandHandlerType, commandHandler);
                return commandHandler;
            }
        }
        catch { }
        {
        }
        return default;
    }
}
