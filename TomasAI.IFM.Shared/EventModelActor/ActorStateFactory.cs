using Microsoft.Extensions.Caching.Distributed;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides functionality to create, load, and save actor state instances.
/// </summary>
/// <remarks>This factory is responsible for managing the lifecycle of actor state objects,  including loading
/// state from a distributed cache and saving state back to it.  It relies on an <see
/// cref="IActorStateFactoryResolver"/> to resolve state dependencies  and an <see cref="IDistributedCache"/> for state
/// persistence.</remarks>
/// <param name="resolver"></param>
/// <param name="cache"></param>
public class ActorStateFactory(IActorStateFactoryResolver resolver, IDistributedCache cache)
    : IActorStateFactory
{
    readonly IActorStateFactoryResolver _resolver = IsArgumentNull.Set(resolver);
    readonly IDistributedCache _cache = IsArgumentNull.Set(cache);

    public async ValueTask<TState> LoadStateAsync<TState>(ActorThreadId stateId) where TState : IActorState
    {
        var result = CreateState<TState>(stateId);
        return await ValueTask.FromResult(result);
    }

    public async ValueTask SaveStateAsync<TState>(TState state) where TState : IActorState
    {
        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates and initializes an actor state of the specified type.
    /// </summary>
    /// <typeparam name="TState">The type of the actor state to create. Must implement <see cref="IActorState"/>.</typeparam>
    /// <param name="stateId">The unique identifier to assign to the created actor state.</param>
    /// <returns>An instance of the specified actor state type, initialized with the provided <paramref name="stateId"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the actor state of the specified type cannot be resolved.</exception>
   TState CreateState<TState>(ActorThreadId stateId) where TState : IActorState
    {
        var actorStateType = typeof(IActorState<>);
        var actorStateGenericType = actorStateType.MakeGenericType(typeof(TState));
        var result = _resolver.Resolve(actorStateGenericType) is TState state
            ? state
            : throw new InvalidOperationException($"Could not resolve actor state of type {typeof(TState).FullName}");
        result.Id = stateId;
        return result;
    }
}
