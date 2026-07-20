using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a mechanism to create instances of actors based on their type.
/// </summary>
/// <remarks>This factory uses a delegate to resolve actor instances. The delegate must return an object that
/// implements the <see cref="IActor"/> interface for the specified actor type.</remarks>
/// <param name="actorResolver">A function that takes a <see cref="Type"/> representing the actor type and returns an instance of the actor. The
/// returned object must implement the <see cref="IActor"/> interface.</param>
public class ActorFactory(Func<Type, object> actorResolver) 
    : IActorFactory
{
    public IActor GetActor(Type actorType)
        => (actorResolver.Invoke(actorType) as IActor)!;
}
