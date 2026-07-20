using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventService;

/// <summary>
/// event service handler resolver constructor
/// </summary>
/// <param name="resolverFunction">function that will return event service handler using dependancy injection</param>
public class EventServiceHandlerResolver(Func<Type, object>? resolverFunction) 
    : IEventServiceHandlerResolver
{

    /// <summary>
    /// Resolves an event handler for the specified event and service types.
    /// </summary>
    /// <remarks>This method attempts to resolve an event handler dynamically using the provided types.  If
    /// the resolution fails, the method returns <see langword="null"/> without throwing an exception.</remarks>
    /// <param name="eventType">The type of the event to be handled.</param>
    /// <param name="eventServiceType">The type of the service associated with the event.</param>
    /// <returns>An instance of the event handler that implements the <see cref="IAsyncEventHandler{TEvent, TService}"/>
    /// interface for the specified event and service types, or <see langword="null"/> if no handler is found.</returns>
    public object? ResolveEventHandler(Type eventType, Type eventServiceType)
    {
        try
        {
            var eventHandlerType = typeof(IAsyncEventHandler<,>).MakeGenericType(eventType, eventServiceType);
            return resolverFunction?.Invoke(eventHandlerType);
        }
        catch { }
        {
            return default;
        }
    }

    /// <summary>
    /// Resolves an instance of an event service handler for the specified event and service types.
    /// </summary>
    /// <remarks>This method dynamically constructs the generic type <see
    /// cref="IAsyncEventServiceHandler{TEvent, TService}"/> using the provided <paramref name="eventType"/> and
    /// <paramref name="eventServiceType"/>, and attempts to resolve an instance of that type using the configured
    /// resolver function. If the resolution fails, the method returns <see langword="null"/>.</remarks>
    /// <param name="eventType">The type of the event for which the handler is being resolved.</param>
    /// <param name="eventServiceType">The type of the service associated with the event.</param>
    /// <returns>An instance of the event service handler that implements <see cref="IAsyncEventServiceHandler{TEvent,
    /// TService}"/> for the specified <paramref name="eventType"/> and <paramref name="eventServiceType"/>, or <see
    /// langword="null"/> if the handler could not be resolved.</returns>
    public object? ResolveEventServiceHandler(Type eventType, Type eventServiceType)
    {
        try
        {
            var eventHandlerType = typeof(IAsyncEventServiceHandler<,>).MakeGenericType(eventType, eventServiceType);
            return resolverFunction?.Invoke(eventHandlerType);
        }
        catch { }
        {
            return default;
        }
    }
}
