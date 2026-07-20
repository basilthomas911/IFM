using TomasAI.IFM.Shared.Application.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.Command.State;

/// <summary>
/// Represents the event-sourced state of Application commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Application operations by applying domain events.
/// It mirrors the logic of <see cref="Application.ApplicationBoundedContextState"/>.</remarks>
public class ApplicationCommandState
    : BaseEventSourceActorState<ApplicationCommandState>, IEventSourceActorState<ApplicationCommandState>
{
    /// <summary>
    /// Gets or sets the unique identifier for the actor thread associated with this state.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies the specified domain event to update the state of the current object.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply. Must be of a supported type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                ApplicationStartupEvent e => On(e),
                ApplicationShutdownEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// application startup event
    /// </summary>
    /// <param name="e"></param>
    bool On(ApplicationStartupEvent e) => true;

    /// <summary>
    /// application shutdown event
    /// </summary>
    /// <param name="e"></param>
    bool On(ApplicationShutdownEvent e) => true;
}
