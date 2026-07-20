namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for actor state management in event-driven systems, supporting state persistence and retrieval
/// for actors of type <typeparamref name="TState"/>.
/// </summary>
/// <typeparam name="TState">The type of actor state managed by the implementation. Must implement <see cref="IActorState"/>.</typeparam>
public interface IEventActorState<TState> : IActorState<TState>
    where TState : IActorState
{
}
