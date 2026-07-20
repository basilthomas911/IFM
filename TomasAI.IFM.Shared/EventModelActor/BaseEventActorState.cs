using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a base implementation for event actor state objects, parameterized by the specific state type.
/// </summary>
/// <typeparam name="TState">The type of the event actor state. Must implement <see cref="IEventActorState{TState}"/> and be a reference type.</typeparam>
public abstract class BaseEventActorState<TState> : IEventActorState<TState> 
    where TState : class, IEventActorState<TState>
{
    public abstract ActorThreadId Id { get; set; }
}
