namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the state of an actor, including its unique identifier.
/// </summary>
/// <remarks>This interface provides access to the actor's state information, such as its unique thread
/// identifier. It is typically used in scenarios where actor-based concurrency models are implemented.</remarks>
public interface IActorState
{
    ActorThreadId Id { get; set; }
}

/// <summary>
/// Represents the state of an actor with a specific type parameter.
/// </summary>
/// <typeparam name="TState">The type of the actor state. Must implement <see cref="IActorState"/>.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IActorState<TState> : IActorState
    where TState : IActorState
{
}
