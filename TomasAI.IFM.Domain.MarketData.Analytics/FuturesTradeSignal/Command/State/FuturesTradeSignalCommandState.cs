using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures Trade Signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures Trade Signal operations by applying domain events
/// such as <see cref="FuturesTradeSignalUpdatedEvent"/>. It provides change-detection and signal computation
/// for the trade signal actor.</remarks>
public class FuturesTradeSignalCommandState
    : BaseEventSourceActorState<FuturesTradeSignalCommandState>, IEventSourceActorState<FuturesTradeSignalCommandState>
{
    FuturesTradeSignalV2ReadModel? _futuresTradeSignal;

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
                FuturesTradeSignalUpdatedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesTradeSignalUpdatedEvent e)
        {
            _futuresTradeSignal = e.FuturesTradeSignal;
            return true;
        }
    }

    internal FuturesTradeSignalV2ReadModel? FuturesTradeSignal => _futuresTradeSignal;

    /// <summary>
    /// Checks whether the trade signal has changed compared to the current state.
    /// </summary>
    internal bool HasFuturesTradeSignalChanged(FuturesTradeSignalV2ReadModel e)
        => default(bool) switch
        {
            _ when _futuresTradeSignal is null => true,
            _ when _futuresTradeSignal.ValueDate != e.ValueDate => true,
            _ when _futuresTradeSignal.FuturesPrice != e.FuturesPrice => true,
            _ when _futuresTradeSignal.FundRiskPercent != e.FundRiskPercent => true,
            _ when _futuresTradeSignal.RSI != e.RSI => true,
            _ when _futuresTradeSignal.RSISlope != e.RSISlope => true,
            _ when _futuresTradeSignal.TrendType != e.TrendType => true,
            _ when _futuresTradeSignal.TrendStrength != e.TrendStrength => true,
            _ when _futuresTradeSignal.TradeSignal != e.TradeSignal => true,
            _ when _futuresTradeSignal.TDI != e.TDI => true,
            _ when _futuresTradeSignal.TDIStrength != e.TDIStrength => true,
            _ when _futuresTradeSignal.MDI != e.MDI => true,
            _ when _futuresTradeSignal.TradeExecuteState != e.TradeExecuteState => true,
            _ => false
        };

    /// <summary>
    /// Checks whether the ITI signal hold-trade state has changed.
    /// </summary>
    internal bool HasFuturesItiSignalHoldTradeChanged(FuturesTradeSignalV2ReadModel e)
        => default(bool) switch
        {
            _ when _futuresTradeSignal is null => false,
            _ when _futuresTradeSignal.TradeExecuteState == TradeExecuteState.Hold && e.TradeExecuteState != TradeExecuteState.Hold => true,
            _ when _futuresTradeSignal.TradeExecuteState != TradeExecuteState.Hold && e.TradeExecuteState == TradeExecuteState.Hold => true,
            _ => false
        };

    
}
