using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures MACD signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures MACD signal operations by applying domain events
/// such as <see cref="FuturesMacdSignalGeneratedEvent"/>.</remarks>
public class FuturesMacdSignalCommandState
    : BaseEventSourceActorState<FuturesMacdSignalCommandState>, IEventSourceActorState<FuturesMacdSignalCommandState>
{
    FuturesMacdSignalReadModel? _macdSignal;
    readonly List<FuturesMacdSignalReadModel> _macdSignals = new();

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
            FuturesMacdSignalStartedEvent => true,
            FuturesMacdSignalStoppedEvent => true,
            FuturesMacdSignalGeneratedEvent e => On(e.FuturesMacdSignal),
            FuturesMacdDailySignalGeneratedEvent e => On(e.FuturesMacdSignal),
            _ => false
        };

        bool On(FuturesMacdSignalReadModel signal)
        {
            _macdSignal = signal;
            _macdSignals.Add(signal);
            return true;
        }
    }

    /// <summary>
    /// Gets the view model that provides MACD signal data for futures trading analysis.
    /// </summary>
    /// <remarks>This property exposes the MACD signal data used to inform trading decisions in futures
    /// markets. The returned view model should be properly initialized before use.</remarks>
    public FuturesMacdSignalReadModel MacdSignal => _macdSignal!;
    public IReadOnlyCollection<FuturesMacdSignalReadModel> MacdSignals => _macdSignals;

}
