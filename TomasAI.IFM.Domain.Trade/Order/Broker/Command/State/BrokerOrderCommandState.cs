using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;

/// <summary>Reconstructed durable state for one logical component broker order.</summary>
public sealed class BrokerOrderCommandState : BaseEventSourceActorState<BrokerOrderCommandState>
{
    private readonly Dictionary<Guid, string> _observationHashes = [];
    public override ActorThreadId Id { get; set; } = default!;
    public BrokerOrderDefinition? Current { get; private set; }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not BrokerOrderChangedEvent changed || !changed.State.Id.IsValid ||
            Current is not null && changed.State.Revision != Current.Revision + 1)
            return false;
        if (changed.State.LastObservation is { } observation)
        {
            if (_observationHashes.TryGetValue(observation.ObservationId, out var hash) && hash != observation.ContentHash)
                return false;
            _observationHashes[observation.ObservationId] = observation.ContentHash;
        }
        Current = changed.State;
        return true;
    }

    /// <summary>Finds the durable hash already applied for an observation identity.</summary>
    public bool TryGetObservationHash(Guid observationId, out string contentHash) =>
        _observationHashes.TryGetValue(observationId, out contentHash!);
}
