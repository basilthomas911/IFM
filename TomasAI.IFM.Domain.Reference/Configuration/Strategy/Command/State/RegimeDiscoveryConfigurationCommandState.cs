using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;

/// <summary>Reconstructs the immutable Regime Discovery configuration lifecycle.</summary>
public sealed class RegimeDiscoveryConfigurationCommandState
    : BaseEventSourceActorState<RegimeDiscoveryConfigurationCommandState>,
      IEventSourceActorState<RegimeDiscoveryConfigurationCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the typed entity identity.</summary>
    public RegimeDiscoveryParameterSetEntityId EntityId { get; private set; }
    /// <summary>Gets the immutable typed payload after creation.</summary>
    public RegimeDiscoveryParameterSet? ParameterSet { get; private set; }
    /// <summary>Gets the lifecycle status.</summary>
    public string Status { get; private set; } = "Empty";
    /// <summary>Gets the effective UTC timestamp.</summary>
    public DateTime? EffectiveFromUtc { get; private set; }
    /// <summary>Gets the retirement UTC timestamp.</summary>
    public DateTime? RetiredAtUtc { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case RegimeDiscoveryParameterSetCreatedEvent created:
                EntityId = created.EntityId;
                ParameterSet = created.ParameterSet;
                Status = "Draft";
                return true;
            case RegimeDiscoveryParameterSetPublishedEvent published when Status == "Draft":
                EffectiveFromUtc = published.EffectiveFromUtc;
                Status = "Published";
                return true;
            case RegimeDiscoveryParameterSetRetiredEvent retired when Status == "Published":
                RetiredAtUtc = retired.RetiredAtUtc;
                Status = "Retired";
                return true;
            default:
                return false;
        }
    }
}
