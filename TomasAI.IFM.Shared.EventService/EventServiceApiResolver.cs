namespace TomasAI.IFM.Shared.EventService;

/// <summary>
/// Provides a mechanism to resolve instances of event service APIs based on their type.
/// </summary>
/// <remarks>This class allows for the resolution of event service API instances using a custom resolver function.
/// The resolver function is invoked with the specified event service type and is expected to return an instance of the
/// requested type or <see langword="null"/> if the type cannot be resolved.</remarks>
/// <param name="resolverFunction"></param>
public class EventServiceApiResolver(Func<Type, object>? resolverFunction)
    : IEventServiceApiResolver
{
    /// <summary>
    /// Resolves an instance of the specified event service API type.
    /// </summary>
    /// <remarks>This method uses a resolver function to attempt to resolve the requested event service API
    /// type.  If the resolver function is not set or an exception occurs during resolution, the method returns <see
    /// langword="null"/>.</remarks>
    /// <param name="eventServiceApiType">The <see cref="Type"/> of the event service API to resolve. This parameter cannot be <see langword="null"/>.</param>
    /// <returns>An instance of the specified event service API type if the resolution is successful; otherwise, <see
    /// langword="null"/>.</returns>
    public TApi? ResolveApi<TApi>() where TApi : class
    {
        try
        {
            var eventServiceApiType = typeof(TApi); 
            return resolverFunction?.Invoke(eventServiceApiType) as TApi;
        }
        catch 
        {
            return default;
        }
    }
}
