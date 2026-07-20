using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Provides methods to create instances of actor state objects from various sources of domain events or event streams.
/// </summary>
/// <remarks>This factory interface is designed to construct actor state objects that implement the <see
/// cref="IActorState{TState}"/> interface. The created state objects can be initialized using domain events, event
/// stream data, or with default values.</remarks>
public interface IEventSourceActorStateFactory 
{
    IEventSourceActorState<TState> CreateState<TState>(DomainEventCollection domainEvents)
        where TState : IEventSourceActorState<TState>;

    IEventSourceActorState<TState> CreateState<TState>(ICollection<EventStreamReadModel> eventStream)
        where TState : IEventSourceActorState<TState>;

    IEventSourceActorState<TState> CreateState<TState>()
        where TState : IEventSourceActorState<TState>;

}
