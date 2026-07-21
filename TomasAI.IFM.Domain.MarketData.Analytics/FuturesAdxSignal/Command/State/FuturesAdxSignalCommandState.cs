using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures ADX signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures ADX signal operations by applying domain events
/// such as <see cref="FuturesAdxSignalGeneratedEvent"/>.</remarks>
public class FuturesAdxSignalCommandState
    : BaseEventSourceActorState<FuturesAdxSignalCommandState>, IEventSourceActorState<FuturesAdxSignalCommandState>
{
    readonly List<FuturesAdxSignalReadModel> _adxSignals = [with(32)];
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
        try
        {
            return domainEvent switch
            {
                FuturesAdxSignalGeneratedEvent e => On(e.FuturesAdxSignal),
                FuturesAdxDailySignalGeneratedEvent e => On(e.FuturesAdxSignal),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesAdxSignalReadModel signal)
        {
            _adxSignals.Add(signal);
            _adxSignal = signal;
            return true;
        }
    }

    /// <summary>
    /// Gets the view model that provides ADX signal data for futures trading analysis.
    /// </summary>
    public FuturesAdxSignalReadModel AdxSignal => _adxSignal!;

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyCollection< FuturesAdxSignalReadModel> AdxSignals => _adxSignals;
}
