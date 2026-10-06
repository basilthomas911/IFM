using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures ADX signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures ADX signal operations by applying domain events
/// such as <see cref="FuturesAdxSignalGeneratedEvent"/>.</remarks>
public class FuturesAdxSignalCommandState
    : BaseEventSourceActorState<FuturesAdxSignalCommandState>, IEventSourceActorState<FuturesAdxSignalCommandState>
{
    readonly List<FuturesAdxSignalReadModel> _adxSignals = new(32);
    FuturesAdxSignalReadModel? _adxSignal;

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
            FuturesAdxSignalStartedEvent e => OnStarted(e.RestoredSignal, e.ResetForHistoricalSeed),
            FuturesAdxSignalStoppedEvent => true,
            FuturesAdxSignalGeneratedEvent e => On(e.FuturesAdxSignal),
            FuturesAdxDailySignalGeneratedEvent e => On(e.FuturesAdxSignal),
            _ => false
        };

        bool OnStarted(FuturesAdxSignalReadModel? restored, bool reset)
        {
            if (reset)
            {
                _adxSignals.Clear();
                _adxSignal = null;
                return true;
            }
            if (restored is not null && _adxSignals.Count == 0)
            {
                _adxSignals.Add(restored);
                _adxSignal = restored;
            }
            return true;
        }

        bool On(FuturesAdxSignalReadModel signal)
        {
            if (signal is null)
                return false;
            _adxSignals.Add(signal);
            _adxSignal = signal;
            return true;
        }
    }

    /// <summary>Applies a validated start and its precomputed historical signals through event dispatch.</summary>
    /// <param name="startedEvent">The accepted ADX start event.</param>
    /// <param name="historicalSignals">Prevalidated signal events in observation order.</param>
    /// <param name="command">The originating start command used for event metadata.</param>
    /// <returns>True when all initialization events have been applied.</returns>
    public bool Update(FuturesAdxSignalStartedEvent startedEvent,
        IReadOnlyList<FuturesAdxSignalGeneratedEvent> historicalSignals, ICommand command)
    {
        if (!Update(startedEvent, command))
            return false;
        foreach (var historicalSignal in historicalSignals)
            if (!Update(historicalSignal, command))
                return false;
        return true;
    }

    /// <summary>
    /// Gets the view model that provides ADX signal data for futures trading analysis.
    /// </summary>
    public FuturesAdxSignalReadModel AdxSignal => _adxSignal!;

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyCollection<FuturesAdxSignalReadModel> AdxSignals => _adxSignals;
}
