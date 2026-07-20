using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

/// <summary>
/// Represents the event-sourced state of Futures ITI signal commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Futures ITI signal operations by applying domain events
/// such as <see cref="FuturesItiSignalGeneratedEvent"/>. It provides methods to check signal existence, retrieve
/// calculated values such as trend deltas, and manage trade states.</remarks>
public class FuturesItiSignalCommandState
    : BaseEventSourceActorState<FuturesItiSignalCommandState>, IEventSourceActorState<FuturesItiSignalCommandState>
{
    FuturesItiSignalV2ReadModel? _futuresItiSignal;

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
                FuturesItiSignalGeneratedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(FuturesItiSignalGeneratedEvent e)
        {
            _futuresItiSignal = e.FuturesItiSignal;
            return true;
        }
    }

    /// <summary>
    /// Gets the futures contract identifier.
    /// </summary>
    internal string ContractId => _futuresItiSignal!.ContractId;

    /// <summary>
    /// Gets the value date for the signal.
    /// </summary>
    internal DateOnly ValueDate => _futuresItiSignal!.ValueDate;

    /// <summary>
    /// Gets the trade time period type for the signal.
    /// </summary>
    internal TradeTimePeriodType TimePeriod => _futuresItiSignal!.TimePeriod;

    /// <summary>
    /// Gets the intrinsic time timestamp of the signal.
    /// </summary>
    internal DateTime IntrinsicTime => _futuresItiSignal!.IntrinsicTime;

    /// <summary>
    /// Gets the intrinsic time group identifier used to track trend direction changes.
    /// </summary>
    internal int IntrinsicTimeGroupId => _futuresItiSignal!.IntrinsicTimeGroupId;

    /// <summary>
    /// Gets the length of the current intrinsic time event.
    /// </summary>
    internal double IntrinsicTimeLength => _futuresItiSignal!.IntrinsicTimeLength;

    /// <summary>
    /// Gets the intrinsic price at the current signal point.
    /// </summary>
    internal double IntrinsicPrice => _futuresItiSignal!.IntrinsicPrice;

    /// <summary>
    /// Gets the current intrinsic time trend direction.
    /// </summary>
    internal IntrinsicTimeTrendType IntrinsicTimeTrend => _futuresItiSignal!.IntrinsicTimeTrend;

    /// <summary>
    /// Gets the current intrinsic time mode indicating the type of signal event.
    /// </summary>
    internal IntrinsicTimeModeType IntrinsicTimeMode => _futuresItiSignal!.IntrinsicTimeMode;

    /// <summary>
    /// Gets the price at which the current trend started.
    /// </summary>
    internal double TrendPrice => _futuresItiSignal!.TrendPrice;

    /// <summary>
    /// Gets the extreme price reached during the current trend.
    /// </summary>
    internal double TrendExtreme => _futuresItiSignal!.TrendExtreme;

    /// <summary>
    /// Gets the reversal price level for the current trend.
    /// </summary>
    internal double TrendReversal => _futuresItiSignal!.TrendReversal;

    /// <summary>
    /// Gets the current state of the trade as determined by the associated futures ITI signal.
    /// </summary>
    internal IntrinsicTimeTradeState TradeState => _futuresItiSignal!.TradeState;

    /// <summary>
    /// Gets the lambda threshold used for intrinsic time calculations.
    /// </summary>
    internal double Lambda => _futuresItiSignal!.Lambda;

    /// <summary>
    /// Gets the number of trading days available for futures signals.
    /// </summary>
    internal int TradingDays => _futuresItiSignal!.TradingDays;

    /// <summary>
    /// Gets the threshold value used for signal processing.
    /// </summary>
    internal double Threshold => _futuresItiSignal!.Threshold;

    /// <summary>
    /// Gets the target delta value for the current signal.
    /// </summary>
    internal double TargetDelta => _futuresItiSignal!.TargetDelta;

    /// <summary>
    /// Gets the change in trend value as determined by the underlying futures ITI signal.
    /// </summary>
    internal double TrendDelta => _futuresItiSignal!.TrendDelta;

    /// <summary>
    /// Gets the price level that triggers an uptrend direction change.
    /// </summary>
    internal double UpTrendTrigger => _futuresItiSignal!.UpTrendTrigger;

    /// <summary>
    /// Gets the price level that triggers a downtrend direction change.
    /// </summary>
    internal double DownTrendTrigger => _futuresItiSignal!.DownTrendTrigger;

    /// <summary>
    /// Determines whether a signal entity with the specified identifier exists.
    /// </summary>
    /// <param name="entityId">The identifier of the signal entity to check for existence.</param>
    /// <returns><see langword="true"/> if a signal entity with the specified identifier exists; otherwise, <see langword="false"/>.</returns>
    internal bool Exists(FuturesItiSignalEntityId entityId)
        => _futuresItiSignal is not null
            && _futuresItiSignal.Id.ContractId == entityId.ContractId
            && _futuresItiSignal.Id.ValueDate == entityId.ValueDate;

    /// <summary>
    /// Gets a value indicating whether the trade is in a ready state for execution.
    /// </summary>
    internal bool IsTradeInReadyState
        => _futuresItiSignal is not null && _futuresItiSignal.TradeState == IntrinsicTimeTradeState.Ready;

    /// <summary>
    /// Gets a value indicating whether the trade is currently in a hold state.
    /// </summary>
    internal bool IsTradeInHoldState
        => _futuresItiSignal is not null && _futuresItiSignal.TradeState == IntrinsicTimeTradeState.Hold;

    /// <summary>
    /// Calculates the difference between the trend extreme and the intrinsic price of the specified futures signal.
    /// </summary>
    /// <param name="e">The futures signal model containing the trend extreme and intrinsic price values.</param>
    /// <returns>The difference between the trend extreme and the intrinsic price.</returns>
    internal double GetTrendDelta(FuturesItiSignalV2ReadModel e)
        => e.TrendExtreme - e.IntrinsicPrice;

    /// <summary>
    /// Calculates the minimum threshold value for a trading signal based on the intrinsic price, trading days, and
    /// specified scaling parameters.
    /// </summary>
    /// <remarks>The returned threshold is influenced by the trend type and the relationship between the
    /// intrinsic price and trend-related properties. For downtrends, the minimum target delta is returned directly;
    /// otherwise, the method may return a higher value if certain trend conditions are met.</remarks>
    /// <param name="e">A view model containing the intrinsic price, trend information, and related properties used to determine the
    /// threshold.</param>
    /// <param name="tradingDays">The number of trading days to consider in the calculation. Must be a non-negative integer.</param>
    /// <param name="lambda">A scaling coefficient applied to the intrinsic price to influence the minimum target delta.</param>
    /// <param name="futuresPriceTick">The tick size for futures prices. Defaults to 0.25 if not specified.</param>
    /// <returns>The calculated threshold value, which reflects the minimum target delta or a higher value depending on the trend
    /// conditions.</returns>
    internal double GetThreshold(FuturesItiSignalV2ReadModel e, int tradingDays, double lambda, double futuresPriceTick = 0.25)
    {
        var minTargetDelta = e.IntrinsicPrice * lambda + Math.Sqrt(tradingDays) * futuresPriceTick;
        if (e.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend)
            return minTargetDelta;
        var trendDelta = GetTrendDelta(e);
        var minExtremeDelta = minTargetDelta * 1 / Math.PI;
        return (e.TrendExtreme - e.IntrinsicPrice) > minExtremeDelta && e.IntrinsicPrice > (e.TrendPrice + minTargetDelta)
            ? Math.Max(trendDelta, minTargetDelta)
            : minTargetDelta;
    }
 
}
