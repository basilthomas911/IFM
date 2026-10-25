namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a factory for managing the lifecycle of actor state, including loading and saving state data.
/// </summary>
/// <remarks>This interface provides methods to load and save actor state, which can be used to persist and
/// retrieve state information associated with an actor. Implementations of this interface are responsible for ensuring
/// the durability and consistency of the state.</remarks>
public interface IActorStateFactory
{
    ValueTask<TState> LoadStateAsync<TState>(ActorThreadId stateId) where TState : IActorState;
    ValueTask SaveStateAsync<TState>(TState state) where TState : IActorState;
}
