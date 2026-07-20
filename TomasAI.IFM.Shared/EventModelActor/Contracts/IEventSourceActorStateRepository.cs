using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a repository for managing the state of event-sourced actors.
/// </summary>
/// <remarks>This interface provides methods to load and save the state of an actor in an event-sourced system. 
/// Implementations of this interface are responsible for persisting and retrieving actor state  based on the provided
/// commands and state types.</remarks>
public interface IEventSourceActorStateRepository<TState> 
    where TState : IEventSourceActorState<TState>
{
    ValueTask<TState> LoadStateAsync(ICommand command);

    ValueTask SaveStateAsync(ICommandActorContext context, TState state, ICommand command);
}
