using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;

/// <summary>Reconstructed durable state for one logical component broker order.</summary>
public sealed class BrokerOrderCommandState : BaseEventSourceActorState<BrokerOrderCommandState>
{
    private readonly Dictionary<Guid, string> _observationHashes = [];
    public override ActorThreadId Id { get; set; } = default!;
    public BrokerOrderDefinition? BrokerOrderDefinition { get; private set; }

    /// <summary>Mutates authoritative broker-order data only from a committed source-event definition.</summary>
    /// <param name="domainEvent">The event to apply during command execution or reconstruction.</param>
    /// <returns>True when the event and its revision/observation fences are accepted.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case BrokerOrderChangedEvent changed:
                if (!changed.BrokerOrderDefinition.Id.IsValid ||
                    BrokerOrderDefinition is not null && changed.BrokerOrderDefinition.Revision != BrokerOrderDefinition.Revision + 1)
                    return false;
                if (changed.BrokerOrderDefinition.LastObservation is { } observation)
                {
                    if (_observationHashes.TryGetValue(observation.ObservationId, out var hash) && hash != observation.ContentHash)
                        return false;
                    _observationHashes[observation.ObservationId] = observation.ContentHash;
                }
                BrokerOrderDefinition = changed.BrokerOrderDefinition;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Finds the durable hash already applied for an observation identity.</summary>
    public bool TryGetObservationHash(Guid observationId, out string contentHash) =>
        _observationHashes.TryGetValue(observationId, out contentHash!);
}
