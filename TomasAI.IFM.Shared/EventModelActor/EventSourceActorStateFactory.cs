using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Factory for creating actor state instances and initializing them by replaying domain events or event streams.
/// </summary>
/// <remarks>
/// This factory resolves actor state instances via the dependency injection container using
/// <see cref="IActorStateFactoryResolver"/> and optionally replays the provided events to bring the state to its
/// current version. Ensure the concrete implementation of the requested state type is registered in the container.
/// </remarks>
/// <param name="stateFactoryResolver">Resolver used to obtain actor state instances from the dependency injection container.</param>
public class EventSourceActorStateFactory(IActorStateFactoryResolver stateFactoryResolver) 
    : IEventSourceActorStateFactory
{
    readonly IActorStateFactoryResolver _actorStateFactoryResolver = IsArgumentNull.Set( stateFactoryResolver);

    /// <summary>
    /// Creates and initializes an actor state by replaying the provided domain events.
    /// </summary>
    /// <typeparam name="TState">
    /// The actor state type to create. Must implement <see cref="IEventSourceActorState{TState}"/>.
    /// </typeparam>
    /// <param name="domainEvents">The domain events to replay to build the current state.</param>
    /// <returns>
    /// The initialized state instance after replaying the supplied domain events.
    /// </returns>
    public IEventSourceActorState<TState> CreateState<TState>(DomainEventCollection domainEvents) 
        where TState : IEventSourceActorState<TState>
    {
        // load actor state from DI container...
        var actorStateType = typeof(IEventSourceActorState<>);
        var actorStateGenericType = actorStateType.MakeGenericType(typeof(TState));
        var actorState = _actorStateFactoryResolver.Resolve(actorStateGenericType) as IEventSourceActorState<TState>;

        // replay events in actor state to set current state...
        actorState
            ?.ReplayEvents(domainEvents);

        // return current actor state...
        return actorState!;
    }

    /// <summary>
    /// Creates and initializes an actor state by replaying the provided event stream entries.
    /// </summary>
    /// <remarks>
    /// The actor state is resolved from the container and then each <see cref="EventStreamReadModel"/> is converted
    /// to a domain event and applied to reconstruct the current state.
    /// </remarks>
    /// <typeparam name="TState">
    /// The actor state type to create. Must implement <see cref="IEventSourceActorState{TState}"/>.
    /// </typeparam>
    /// <param name="eventStream">The event stream entries used to rebuild the state.</param>
    /// <returns>
    /// The initialized state instance after replaying the supplied event stream.
    /// </returns>
    public IEventSourceActorState<TState> CreateState<TState>(ICollection<EventStreamReadModel> eventStream) 
        where TState : IEventSourceActorState<TState>
    {
        // load actor state from DI container...
        var actorStateType = typeof(IEventSourceActorState<>);
        var actorStateGenericType = actorStateType.MakeGenericType(typeof(TState));
        var actorState = _actorStateFactoryResolver.Resolve(actorStateGenericType) as IEventSourceActorState<TState>;

        // replay events in actor state to set current state...
        actorState
              ?.ReplayEvents(eventStream);

        // return current actor state...
        return actorState!;
    }

    /// <summary>
    /// Creates an actor state instance for the specified type without replaying any events.
    /// </summary>
    /// <remarks>
    /// The actor state is resolved using the dependency injection container. Use the overloads that accept
    /// <see cref="DomainEventCollection"/> or <see cref="ICollection{T}"/> of <see cref="EventStreamReadModel"/>
    /// to reconstruct state by replaying events.
    /// </remarks>
    /// <typeparam name="TState">The actor state type to create. Must implement <see cref="IEventSourceActorState{TState}"/>.</typeparam>
    /// <returns>A new instance of the requested actor state type.</returns>
    public IEventSourceActorState<TState> CreateState<TState>()
       where TState : IEventSourceActorState<TState>
    {
        // load actor state from DI container...
        var actorStateType = typeof(IEventSourceActorState<>);
        var actorStateGenericType = actorStateType.MakeGenericType(typeof(TState));
        var actorState = _actorStateFactoryResolver.Resolve(actorStateGenericType) as IEventSourceActorState<TState>;
        return actorState!;
    }
    
}
