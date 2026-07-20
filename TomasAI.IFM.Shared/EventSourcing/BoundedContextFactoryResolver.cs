namespace TomasAI.IFM.Shared.EventSourcing;

public class BoundedContextFactoryResolver(Func<Type, object> resolverFunction = null!)
    : IBoundedContextFactoryResolver
{
    Func<Type, object>? _resolverFunction = resolverFunction;

    /// <summary>
    /// Resolves an instance of the specified bounded context type.
    /// </summary>
    /// <remarks>This method attempts to resolve an instance using the provided resolver function. If the
    /// resolution fails, it returns the default value for the specified type.</remarks>
    /// <param name="boundedContextGenericType">The type of the bounded context to resolve.</param>
    /// <returns>An instance of the specified type if resolution is successful; otherwise, the default value for the type.</returns>
    public object Resolve(Type boundedContextGenericType)
    {
        try
        {
            return _resolverFunction?.Invoke(boundedContextGenericType)!;
        }
        catch { }
        {
            return default!;
        }
    }

}
