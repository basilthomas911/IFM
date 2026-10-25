using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Repository capable of atomically committing a command audit with resident-state events.</summary>
public interface IResidentEventSourceActorStateRepository<TState> : IEventSourceActorStateRepository<TState>
    where TState : IEventSourceActorState<TState>
{
    ValueTask SaveResidentEventsAsync(
        ICommandActorContext context,
        DomainEventCollection events,
        ICommand command,
        long expectedStreamVersion,
        CancellationToken cancellationToken);
}
