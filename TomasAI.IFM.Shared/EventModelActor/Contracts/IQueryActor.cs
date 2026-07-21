using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for a query actor that processes messages, manages state, and handles lifecycle events.
/// </summary>
/// <remarks>A query actor is a specialized actor that interacts with its context to process messages, validate
/// events,  and manage its state. It provides hooks for lifecycle events such as startup and shutdown, as well as 
/// exception handling. Implementations of this interface are expected to define the behavior for each of these 
/// operations.</remarks>
/// <typeparam name="TActor">The type of the actor that implements this interface. Must derive from <see cref="IActor"/>.</typeparam>
public interface IQueryActor<TActor> : IActor<TActor>
    where TActor : IActor
{
    ValueTask OnStartup(IQueryActorContext context);
    ValueTask OnShutdown(IQueryActorContext context);
    ValueTask ReceiveAsync(IQueryActorContext context, IQuery qry);
    ValueTask OnValidateAsync(IQueryActorContext context, IQuery qry);
    ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery qry, string verb,Exception ex);
}

