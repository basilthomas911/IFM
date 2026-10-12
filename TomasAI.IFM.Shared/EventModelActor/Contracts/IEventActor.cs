using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for an event-driven actor that processes messages, manages state, and handles lifecycle events.
/// </summary>
/// <remarks>This interface extends <see cref="IActor"/> to provide additional methods for handling actor
/// lifecycle events, message processing, state management, and exception handling in an event-driven system.
/// Implementations of this interface are expected to define the behavior of the actor during its lifecycle, including
/// startup, shutdown, and state persistence.</remarks>
/// <typeparam name="TActor">The type of the actor implementing this interface. Must implement <see cref="IActor"/>.</typeparam>
public interface IEventActor<TActor> : IActor<TActor>
    where TActor : IActor
{
    ValueTask OnStartup(IEventActorContext<TActor> context);
    ValueTask OnShutdown(IEventActorContext<TActor> context);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask OnValidateAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask ReceiveAsync(IEventActorContext<TActor> context, IEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask OnExceptionAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event, Exception ex);
}

