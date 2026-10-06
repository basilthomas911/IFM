using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.State;

/// <summary>Owns replayed VX paired-leg state for one rollover-compatible stream.</summary>
public sealed class FuturesVxTermStructureSignalCommandState
    : BaseEventSourceActorState<FuturesVxTermStructureSignalCommandState>,
      IEventSourceActorState<FuturesVxTermStructureSignalCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the latest replayed pair checkpoint.</summary>
    public FuturesVxTermStructureCheckpoint? FuturesVxTermStructureCheckpoint { get; private set; }
    /// <summary>Gets the latest valid paired signal.</summary>
    public FuturesVxTermStructureSignalReadModel? FuturesVxTermStructureSignal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesVxTermStructureSignalUpdatedEvent updated => On(updated),
            _ => false
        };
    }

    /// <summary>Mutates the authoritative business values only from the accepted domain event.</summary>
    bool On(FuturesVxTermStructureSignalUpdatedEvent updated)
    {
        if (updated.FuturesVxTermStructureCheckpoint is null) return false;
        FuturesVxTermStructureCheckpoint = updated.FuturesVxTermStructureCheckpoint;
        if (updated.FuturesVxTermStructureSignal is not null) FuturesVxTermStructureSignal = updated.FuturesVxTermStructureSignal;
        return true;
    }
}
