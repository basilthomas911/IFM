using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextState
{
    DomainEventCollection Events { get; }
    bool Apply<TEvent>(TEvent domainEvent, bool addEvent = true) where TEvent : IEvent;
}

public interface IBoundedContextState<TboundedContextState> : IBoundedContextState where TboundedContextState : IBoundedContextState
{
    void ReplayEvents(DomainEventCollection domainEvents);
    void ReplayEvents(ICollection<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<EventStreamReadModel> domainEvents);
    void ReplayEvents(IEnumerable<IEvent> eventStream);
}

public interface IQueryState<TQueryState>  where TQueryState : class
{
    TQueryState As { get; }
}
    
