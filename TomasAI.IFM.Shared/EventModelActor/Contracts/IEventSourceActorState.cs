using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the state of an event-sourced actor, including its current state and the collection of domain events.
/// </summary>
/// <typeparam name="TState">The type of the actor's state, which must implement <see cref="IActorState"/>.</typeparam>
public interface IEventSourceActorState<TState> : IActorState<TState>
    where TState :  IActorState
{
    DomainEventCollection Events { get; }
    bool Apply<TEvent>(TEvent domainEvent, bool addEvent = true) where TEvent : IEvent;
    void ReplayEvents(DomainEventCollection domainEvents);
    void ReplayEvents(ICollection<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<IEvent> eventStream);
}



    
