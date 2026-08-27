using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;

/// <summary>Owns replayed exact futures-session VWAP state.</summary>
public sealed class FuturesVwapSignalCommandState
    : BaseEventSourceActorState<FuturesVwapSignalCommandState>,
      IEventSourceActorState<FuturesVwapSignalCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the latest replayed accumulator checkpoint.</summary>
    public FuturesVwapCheckpoint? Checkpoint { get; private set; }
    /// <summary>Gets the latest projected signal.</summary>
    public FuturesVwapSignalReadModel? Signal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesVwapSignalUpdatedEvent updated) return false;
        Checkpoint = updated.Checkpoint;
        Signal = updated.Signal;
        return true;
    }
}
