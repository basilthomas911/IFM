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
    public FuturesBbAccumulatorCheckpoint? Checkpoint { get; private set; }
    /// <summary>Gets the most recently generated signal.</summary>
    public FuturesBbSignalReadModel? Signal { get; private set; }
    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesBbSignalGeneratedEvent generated) return false;
        Checkpoint = generated.Checkpoint with
        {
            Closes = [.. generated.Checkpoint.Closes],
            CompletedWidths20 = [.. generated.Checkpoint.CompletedWidths20]
        };
        Signal = generated.Signal;
        return true;
    }
}
