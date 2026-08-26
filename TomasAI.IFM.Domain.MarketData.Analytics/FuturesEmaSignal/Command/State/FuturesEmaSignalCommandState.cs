using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;

/// <summary>Owns replayed EMA calculation state for one market series and timeframe.</summary>
public sealed class FuturesEmaSignalCommandState
    : BaseEventSourceActorState<FuturesEmaSignalCommandState>, IEventSourceActorState<FuturesEmaSignalCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the replayed EMA checkpoint.</summary>
    public FuturesEmaAccumulatorCheckpoint? Checkpoint { get; private set; }
    /// <summary>Gets the most recently generated signal.</summary>
    public FuturesEmaSignalReadModel? Signal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesEmaSignalGeneratedEvent generated) return false;
        Checkpoint = generated.Checkpoint;
        Signal = generated.Signal;
        return true;
    }
}
