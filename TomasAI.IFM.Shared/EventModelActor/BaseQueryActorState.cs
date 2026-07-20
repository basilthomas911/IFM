using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the base state for an actor in a query system, providing a generic mechanism  to manage and identify
/// actor states.
/// </summary>
/// <typeparam name="TState">The type of the actor state, which must implement <see cref="IActorState{TState}"/>.</typeparam>
public abstract class BaseQueryActorState<TState> : IQueryActorState<TState> 
    where TState : class, IQueryActorState<TState>
{
    public abstract ActorThreadId Id { get; set; }
}
