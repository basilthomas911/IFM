using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures TDI signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures TDI signal operations by applying domain events
/// such as <see cref="FuturesTdiSignalGeneratedEvent"/>. It provides methods to evaluate trend direction using an
/// internal <see cref="FuturesTdiSignalModel"/>.</remarks>
public class FuturesTdiSignalCommandState
    : BaseEventSourceActorState<FuturesTdiSignalCommandState>, IEventSourceActorState<FuturesTdiSignalCommandState>
{
    FuturesTdiSignalReadModel? _tdiSignal;

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
                FuturesTdiSignalGeneratedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesTdiSignalGeneratedEvent e)
        {
            _tdiSignal = e.FuturesTdiSignal;
            return true;
        }
    }

    /// <summary>
    /// Gets the view model that provides TDI (Trend Direction Index) signal data for futures trading analysis.
    /// </summary>
    /// <remarks>This property exposes the TDI signal data used to inform trading decisions in futures
    /// markets. The returned view model should be properly initialized before use.</remarks>
    internal FuturesTdiSignalReadModel TdiSignal => _tdiSignal!;

}
