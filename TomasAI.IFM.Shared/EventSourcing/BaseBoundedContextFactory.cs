using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Provides functionality to create bounded contexts with a specified state type and initialize their state by
/// replaying domain events.
/// </summary>
/// <remarks>This factory utilizes a resolver to obtain the appropriate bounded context type from a dependency
/// injection container. It then replays the provided domain events to initialize the state of the bounded
/// context.</remarks>
/// <param name="boundedContextFactoryResolver"></param>
public class BoundedContextFactory(IBoundedContextFactoryResolver boundedContextFactoryResolver) 
    : IBoundedContextFactory
{
     readonly IBoundedContextFactoryResolver _boundedContextFactoryResolver = IsArgumentNull.Set( boundedContextFactoryResolver);

    /// <summary>
    /// Creates a bounded context with the specified state type and replays domain events to initialize its state.
    /// </summary>
    /// <typeparam name="TBoundedContextState">The type of the bounded context state, which must implement <see
    /// cref="IBoundedContextState{TBoundedContextState}"/>.</typeparam>
    /// <param name="domainEvents">The collection of domain events to replay in the bounded context state.</param>
    /// <returns>An instance of <see cref="IBoundedContext{TBoundedContextState}"/> with the current state initialized by the
    /// provided domain events.</returns>
    public IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>(DomainEventCollection domainEvents) 
        where TBoundedContextState : IBoundedContextState<TBoundedContextState>
    {
        // load bounded context root from DI container...
        var boundedContextRootType = typeof(IBoundedContext<>);
        var boundedContextRootGenericType = boundedContextRootType.MakeGenericType(typeof(TBoundedContextState));
        var boundedContext = _boundedContextFactoryResolver.Resolve(boundedContextRootGenericType) as IBoundedContext<TBoundedContextState>;

        // replay events in bounded context state to set current state...
        boundedContext
            ?.State
            ?.ReplayEvents(domainEvents);

        // return bounded context with current state...
        return boundedContext!;
    }

    /// <summary>
    /// Creates a bounded context with the specified state type and replays the given domain events to establish the
    /// current state.
    /// </summary>
    /// <typeparam name="TBoundedContextState">The type of the bounded context state, which must implement <see
    /// cref="IBoundedContextState{TBoundedContextState}"/>.</typeparam>
    /// <param name="eventStream">A collection of domain events to replay in the bounded context state. Cannot be null.</param>
    /// <returns>An instance of <see cref="IBoundedContext{TBoundedContextState}"/> with the current state set by the replayed
    /// events.</returns>
    public IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>(ICollection<EventStreamReadModel> eventStream) 
        where TBoundedContextState : IBoundedContextState<TBoundedContextState>
    {
        // load bounded context root from DI container...
        var boundedContextRootType = typeof(IBoundedContext<>);
        var boundedContextRootGenericType = boundedContextRootType.MakeGenericType(typeof(TBoundedContextState));
        var boundedContext = _boundedContextFactoryResolver.Resolve(boundedContextRootGenericType) as IBoundedContext<TBoundedContextState>;

        // replay events in bounded context state to set current state...
        boundedContext
            ?.State
            ?.ReplayEvents(eventStream);

        // return bounded context with current state...
        return boundedContext!;
    }

    public IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>()
       where TBoundedContextState : IBoundedContextState<TBoundedContextState>
    {
        // load bounded context root from DI container...
        var boundedContextRootType = typeof(IBoundedContext<>);
        var boundedContextRootGenericType = boundedContextRootType.MakeGenericType(typeof(TBoundedContextState));
        var boundedContext = _boundedContextFactoryResolver.Resolve(boundedContextRootGenericType) as IBoundedContext<TBoundedContextState>;
        return boundedContext!;
    }
}
