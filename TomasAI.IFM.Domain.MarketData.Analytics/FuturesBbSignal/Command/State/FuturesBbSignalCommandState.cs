using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;

/// <summary>Owns replayed Bollinger calculation state for one market series and timeframe.</summary>
public sealed class FuturesBbSignalCommandState
    : BaseEventSourceActorState<FuturesBbSignalCommandState>, IEventSourceActorState<FuturesBbSignalCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the replayed Bollinger checkpoint.</summary>
    public FuturesBbAccumulatorCheckpoint? FuturesBbCheckpoint { get; private set; }
    /// <summary>Gets the most recently generated signal.</summary>
    public FuturesBbSignalReadModel? FuturesBbSignal { get; private set; }
    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesBbSignalGeneratedEvent generated => On(generated),
            _ => false
        };
    }

    /// <summary>Mutates the authoritative business values only from the accepted domain event.</summary>
    bool On(FuturesBbSignalGeneratedEvent generated)
    {
        if (generated.FuturesBbCheckpoint is null || generated.FuturesBbSignal is null) return false;
        FuturesBbCheckpoint = generated.FuturesBbCheckpoint with
        {
            Closes = [.. generated.FuturesBbCheckpoint.Closes],
            CompletedWidths20 = [.. generated.FuturesBbCheckpoint.CompletedWidths20]
        };
        FuturesBbSignal = generated.FuturesBbSignal;
        return true;
    }
}
