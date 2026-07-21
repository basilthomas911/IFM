using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

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
    FuturesAtrSignalReadModel? _atrSignal;
    readonly List<FuturesAtrSignalReadModel> _atrSignals = new(32);

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
                FuturesAtrSignalGeneratedEvent e => On(e.FuturesAtrSignal),
                FuturesAtrDailySignalGeneratedEvent e => On(e.FuturesAtrSignal),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesAtrSignalReadModel signal)
        {
            _atrSignal = signal;
            _atrSignals.Add(signal);
            return true;
        }
    }

    /// <summary>
    /// Gets the view model that provides ATR (Average True Range) signal data for futures trading analysis.
    /// </summary>
    /// <remarks>This property exposes the ATR signal data used to inform trading decisions in futures
    /// markets. The returned view model should be properly initialized before use.</remarks>
    internal FuturesAtrSignalReadModel  AtrSignal => _atrSignal!;
    internal IReadOnlyCollection<FuturesAtrSignalReadModel> AtrSignals => _atrSignals;

}
