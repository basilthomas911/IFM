using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

/// <summary>
/// Represents the event-sourced state of Spread Distribution commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Spread Distribution operations by applying domain events.
/// Currently no events are applied in this state, mirroring the <see cref="OptionPricer.SpreadDistribution.SpreadDistributionBoundedContextState"/>.</remarks>
public class SpreadDistributionCommandState
    : BaseEventSourceActorState<SpreadDistributionCommandState>, IEventSourceActorState<SpreadDistributionCommandState>
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
    protected override bool Apply(IEvent domainEvent) => false;
}
