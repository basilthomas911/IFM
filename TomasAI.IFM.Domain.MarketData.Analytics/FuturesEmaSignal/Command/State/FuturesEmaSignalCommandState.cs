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
    public FuturesEmaAccumulatorCheckpoint? FuturesEmaCheckpoint { get; private set; }
    /// <summary>Gets the most recently generated signal.</summary>
    public FuturesEmaSignalReadModel? FuturesEmaSignal { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesEmaSignalGeneratedEvent generated => On(generated),
            _ => false
        };
    }

    /// <summary>Mutates the authoritative business values only from the accepted domain event.</summary>
    bool On(FuturesEmaSignalGeneratedEvent generated)
    {
        if (generated.FuturesEmaCheckpoint is null || generated.FuturesEmaSignal is null) return false;
        FuturesEmaCheckpoint = generated.FuturesEmaCheckpoint;
        FuturesEmaSignal = generated.FuturesEmaSignal;
        return true;
    }
}
