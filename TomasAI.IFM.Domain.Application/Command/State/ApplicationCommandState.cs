using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Command.State;

/// <summary>Tracks the latest accepted lifecycle request in the application stream.</summary>
/// <remarks>Each request event fully replaces this state. Runtime readiness is observed separately.</remarks>
public sealed class ApplicationCommandState
    : BaseEventSourceActorState<ApplicationCommandState>, IEventSourceActorState<ApplicationCommandState>
{
    /// <summary>Gets or sets the actor thread associated with the state.</summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Gets the latest accepted startup or shutdown request.</summary>
    public IEvent? LatestRequest { get; private set; }

    /// <summary>Restores the lifecycle request, including its command identity and audit fields.</summary>
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not (ApplicationStartupEvent or ApplicationShutdownEvent))
            return false;

        LatestRequest = domainEvent;
        return true;
    }
}
