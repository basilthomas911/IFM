using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures ATR signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures ATR signal operations by applying domain events
/// such as <see cref="FuturesAtrSignalGeneratedEvent"/>. It provides methods to evaluate trend direction using an
/// internal <see cref="FuturesAtrSignalModel"/>.</remarks>
public class FuturesAtrSignalCommandState
    : BaseEventSourceActorState<FuturesAtrSignalCommandState>, IEventSourceActorState<FuturesAtrSignalCommandState>
{
    FuturesAtrAccumulatorCheckpoint? _calculationState;
    FuturesAtrSignalReadModel? _atrSignal;

    /// <summary>
    /// Gets or sets the unique identifier for the actor thread associated with this state.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies the specified domain event to update the state of the current object.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply. Must be of a supported type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesAtrSignalStartedEvent e => Reset(e.ResetForHistoricalSeed),
            FuturesAtrSignalStoppedEvent => true,
            FuturesAtrSignalGeneratedEvent e => On(e.CalculationState, e.FuturesAtrSignal),
            FuturesAtrDailySignalGeneratedEvent e => On(e.CalculationState, e.FuturesAtrSignal),
            _ => false
        };

        bool Reset(bool reset)
        {
            if (reset)
            {
                _calculationState = null;
                _atrSignal = null;
            }
            return true;
        }

        bool On(FuturesAtrAccumulatorCheckpoint? calculationState, FuturesAtrSignalReadModel? signal)
        {
            if (calculationState is null)
                return false;
            _calculationState = calculationState with
            {
                SeedTrueRanges = [.. calculationState.SeedTrueRanges],
                CompletedAtrValues = [.. calculationState.CompletedAtrValues]
            };
            _atrSignal = signal ?? _atrSignal;
            return true;
        }
    }

    /// <summary>Gets the replayed Wilder checkpoint for the current aggregate stream.</summary>
    internal FuturesAtrAccumulatorCheckpoint? CalculationState => _calculationState;

    /// <summary>Gets the latest event-sourced ATR signal restored with the checkpoint.</summary>
    internal FuturesAtrSignalReadModel? AtrSignal => _atrSignal;

}
