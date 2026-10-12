using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextState
{
    DomainEventCollection Events { get; }
    bool Apply<TEvent>(TEvent domainEvent, bool addEvent = true) where TEvent : IEvent;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IBoundedContextState<TBoundedContextState> : IBoundedContextState where TBoundedContextState : IBoundedContextState
{
    void ReplayEvents(DomainEventCollection domainEvents);
    void ReplayEvents(ICollection<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<IEvent> eventStream);
}

