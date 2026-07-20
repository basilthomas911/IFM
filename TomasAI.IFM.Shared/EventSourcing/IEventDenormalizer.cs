namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEventDenormalizer
{
    Task ExecuteAsync(DomainEventCollection domainEvents);
    Task DenormalizeEventsAsync(DomainEventCollection domainEvents);
}

public interface IEventDenormalizer<TboundedContextState> : IEventDenormalizer where TboundedContextState : IBoundedContextState
{
}
