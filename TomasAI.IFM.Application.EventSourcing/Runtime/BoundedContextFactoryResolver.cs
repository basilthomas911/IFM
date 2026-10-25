using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing;

public class BoundedContextFactoryResolver(Func<Type, object> resolverFunction = null!)
    : IBoundedContextFactoryResolver
{
    readonly Func<Type, object>? _resolverFunction = resolverFunction;

    /// <summary>
    /// Resolves an instance of the specified bounded context type.
    /// </summary>
    /// <remarks>This method attempts to resolve an instance using the provided resolver function. If the
    /// resolution fails, it returns the default value for the specified type.</remarks>
    /// <param name="genericType">The type of the bounded context to resolve.</param>
    /// <returns>An instance of the specified type if resolution is successful; otherwise, the default value for the type.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A resolver miss or activation failure is returned to the caller as no matching handler.")]
    public object Resolve(Type genericType)
    {
        try
        {
            return _resolverFunction?.Invoke(genericType)!;
        }
        catch { /* Resolution failure returns no matching instance to the caller. */ }
        return default!;
    }

}
