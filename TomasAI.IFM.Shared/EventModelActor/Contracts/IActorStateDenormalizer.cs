using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IActorStateDenormalizer
{
    Task ExecuteAsync(DomainEventCollection domainEvents);
    Task DenormalizeEventsAsync(DomainEventCollection domainEvents);
}

public interface IActorStateDenormalizer<TState> 
    : IActorStateDenormalizer where TState : IActorState<TState>
{
}
