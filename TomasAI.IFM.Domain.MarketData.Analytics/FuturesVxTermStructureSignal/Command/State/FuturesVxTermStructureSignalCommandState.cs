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
    public FuturesVxTermStructureCheckpoint? Checkpoint { get; private set; }
    /// <summary>Gets the latest valid paired signal.</summary>
    public FuturesVxTermStructureSignalReadModel? Signal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesVxTermStructureSignalUpdatedEvent updated) return false;
        Checkpoint = updated.Checkpoint;
        if (updated.Signal is not null) Signal = updated.Signal;
        return true;
    }
}
