using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures RSI signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures RSI signal operations by applying domain events
/// such as <see cref="FuturesRsiSignalGeneratedEvent"/>. It provides methods to check signal existence, retrieve
/// RSI values, and manage the collection of RSI signals.</remarks>
public class FuturesRsiSignalCommandState
    : BaseEventSourceActorState<FuturesRsiSignalCommandState>, IEventSourceActorState<FuturesRsiSignalCommandState>
{
    readonly List<FuturesRsiSignalReadModel> _futuresRsiSignals = [];

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
                FuturesRsiSignalStartedEvent => true,
                FuturesRsiSignalStoppedEvent => true,
                FuturesRsiSignalGeneratedEvent e => On(e),
                FuturesRsiDailySignalGeneratedEvent e => OnDaily(e),
                FuturesRsiSignalsGeneratedEvent => true,
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesRsiSignalGeneratedEvent e)
        {
            if (e.FuturesRsiSignal is not null)
            {
                _futuresRsiSignals.Add(e.FuturesRsiSignal);
                return true;
            }
            return false;
        }

        bool OnDaily(FuturesRsiDailySignalGeneratedEvent e)
        {
            if (e.FuturesRsiSignal is not null)
            {
                _futuresRsiSignals.Add(e.FuturesRsiSignal);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the collection of generated futures RSI signals.
    /// </summary>
    internal IReadOnlyCollection<FuturesRsiSignalReadModel> FuturesRsiSignals
        => [.. _futuresRsiSignals];

    /// <summary>
    /// Determines whether futures RSI signals can be generated based on the specified window size.
    /// </summary>
    /// <param name="windowSize">The number of valid RSI signals required.</param>
    /// <returns><see langword="true"/> if there are valid RSI signals within the window; otherwise, <see langword="false"/>.</returns>
    internal bool CanGenerateFuturesRsiSignals(int windowSize)
        => _futuresRsiSignals.Where(o => o.RSI != -1).Take(windowSize).Count() > 0;
}
