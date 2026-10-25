using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextFactory
{
    IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>(DomainEventCollection domainEvents)
        where TBoundedContextState : IBoundedContextState<TBoundedContextState>;

    IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>(ICollection<EventStreamReadModel> eventStream)
        where TBoundedContextState : IBoundedContextState<TBoundedContextState>;

    IBoundedContext<TBoundedContextState> CreateBoundedContext<TBoundedContextState>()
        where TBoundedContextState : IBoundedContextState<TBoundedContextState>;

}
