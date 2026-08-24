namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Provides an event-based actor context associated with a specific realtime actor type.
/// </summary>
/// <remarks>
/// Realtime actors currently use <see cref="IEventActorContext"/> and <c>BaseEventActor&lt;TActor&gt;</c> for their
/// runtime lifecycle. The closed-generic marker allows dependency injection to resolve a context for the concrete
/// realtime actor while retaining the shared event-context capabilities.
/// </remarks>
/// <typeparam name="TActor">The realtime actor type associated with the context.</typeparam>
public interface IRealtimeActorContext<TActor> : IEventActorContext<TActor>
    where TActor : IActor
{
}
