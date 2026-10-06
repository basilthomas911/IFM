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
    public FuturesVwapCheckpoint? FuturesVwapCheckpoint { get; private set; }
    /// <summary>Gets the latest projected signal.</summary>
    public FuturesVwapSignalReadModel? FuturesVwapSignal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesVwapSignalUpdatedEvent updated => On(updated),
            _ => false
        };
    }

    /// <summary>Mutates the authoritative business values only from the accepted domain event.</summary>
    bool On(FuturesVwapSignalUpdatedEvent updated)
    {
        if (updated.FuturesVwapCheckpoint is null) return false;
        FuturesVwapCheckpoint = updated.FuturesVwapCheckpoint;
        FuturesVwapSignal = updated.FuturesVwapSignal;
        return true;
    }
}
