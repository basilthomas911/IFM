using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Evaluates the recovered first-match monitoring rules; it never places or closes an order.</summary>
/// <remarks>Precedence follows ExecuteShortIronCondorAlgorithmCommandHandler and its long counterpart at 281550666^.
/// Gamma and trend rules were not dispatched by those handlers and are retained as observations only.</remarks>
public static class IronCondorLegacyRuleCompute
{
    /// <summary>Applies trailing, daily profit, maximum loss and MDI forward-loss rules to an immutable proposal.</summary>
    /// <param name="snapshot">The proposed legacy monitoring snapshot.</param>
    /// <param name="inputs">Captured limits, fund cash, MDI, average profit and forward-loss classification.</param>
    /// <returns>The computed action and proposed stop ratio, or unchanged unavailable action fields when inputs are missing.</returns>
    public static IronCondorTradePlanSnapshot Evaluate(IronCondorTradePlanSnapshot snapshot, IronCondorTradePlanInputs inputs)
    {
        if (inputs.TradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor)
            || inputs.AverageTradePnl is not { } profit || inputs.FundBalance is not { } cash
            || snapshot.MaxProfit is not > 0 || snapshot.MaxLoss is not { } maximumLoss
            || snapshot.DailyProfitTarget is not { } dailyTarget || snapshot.StopLossLimit is not { } stop
            || !double.IsFinite(stop) || stop < 0 || snapshot.ForwardLossRatio is not { } ratio
            || !double.IsFinite(ratio) || inputs.TradeSignal is not { } signal || !double.IsFinite(signal.MDI)
            || inputs.ForwardLossLimit is null)
            return snapshot;
        var shortTrade = inputs.TradeType == TradeType.ShortIronCondor;
        var warning = IronCondorLegacyValueInitializers.CalculateMdiWarningRatio(inputs.TradeType, signal.MDI);
        var limit = IronCondorLegacyValueInitializers.CalculateMdiLimitRatio(inputs.TradeType, signal.MDI);
        var forwardExceeded = shortTrade ? ratio >= limit : ratio <= limit;
        var forwardWarning = shortTrade ? ratio >= warning : ratio <= warning;
        var inProfit = profit >= 0;
        var normalReason = $"Trend Direction is {signal.TrendType} and Trend Strength is {signal.TrendStrength} := hold trade";
        return snapshot switch
        {
            _ when inProfit && ShouldRaiseTrailingStop(profit, snapshot.MaxProfit.Value, stop)
                => Action(snapshot, shortTrade ? ActionType.WarnTradePosition : ActionType.HoldTradePosition,
                    ActionSubType.RaiseTrailingStopLimit, ActionState.Warning, "Trailing Stop Limit Raised := hold trade",
                    IronCondorLegacyValueInitializers.CalculateNextTrailingStopRatio(stop)),
            _ when inProfit && stop > 0 && TrailingStopReached(profit, snapshot.MaxProfit.Value, stop)
                => Action(snapshot, ActionType.ExitTradePosition, ActionSubType.TrailingStopLimitReached,
                    ActionState.RedAlert, "Trailing Stop Limit Reached := exit trade @ ask price", 0),
            _ when inProfit && stop > 0
                => Action(snapshot, ActionType.WarnTradePosition, ActionSubType.InTrailingStop,
                    ActionState.Warning, "In Trailing Stop := hold trade", stop),
            _ when inProfit && profit > dailyTarget
                => Action(snapshot, ActionType.WarnTradePosition, ActionSubType.DailyProfitTargetExceeded,
                    shortTrade ? ActionState.Warning : ActionState.Critical, "Daily Profit Target Exceeded := exit trade @ mid price", stop),
            _ when inProfit
                => Action(snapshot, ActionType.HoldTradePosition, ActionSubType.TradeInProfitPosition, ActionState.Normal, normalReason, stop),
            _ when stop > 0
                => Action(snapshot, ActionType.HoldTradePosition, ActionSubType.ClearStopLossLimit,
                    ActionState.Normal, "Clear Trailing Stop Limit := hold trade", 0),
            _ when cash > 0 && profit < maximumLoss
                => Action(snapshot, ActionType.ExitTradePosition, ActionSubType.MaxLossLimitReached,
                    ActionState.RedAlert, "Max Loss Limit Reached := exit trade @ ask price", stop),
            _ when forwardExceeded && inputs.ForwardLossLimit == ForwardLossLimitType.LimitReached
                => Action(snapshot, ActionType.ExitTradePosition, ActionSubType.ForwardLossRiskLimitReached,
                    ActionState.RedAlert, "Forward Loss Risk Exceeded := exit trade @ ask price", stop),
            _ when forwardExceeded
                => Action(snapshot, ActionType.WarnTradePosition, ActionSubType.ForwardLossRiskLimitReachedWarning,
                    ActionState.Critical, "Forward Loss Risk Exceeded Warning := exit trade @ mid price", stop),
            _ when forwardWarning
                => Action(snapshot, ActionType.WarnTradePosition, ActionSubType.ForwardLossRiskLimitWarning,
                    ActionState.Warning, "Forward Loss Risk Warning := hold trade", stop),
            _ => Action(snapshot, ActionType.HoldTradePosition, ActionSubType.TradeInLossPosition, ActionState.Normal, normalReason, stop)
        };
    }

    /// <summary>Calculates raise = averageProfit &gt; (stop == 0 ? 0.20 : stop + 0.05) * maximumProfit.</summary>
    /// <param name="averageProfit">Current position profit in currency for monitoring; callers may supply the legacy ten-observation average for parity tests.</param>
    /// <param name="maximumProfit">Configured maximum profit in currency.</param>
    /// <param name="stop">The accepted trailing-stop fraction.</param>
    /// <returns>Whether the next trailing-stop step is earned; equality does not raise it.</returns>
    public static bool ShouldRaiseTrailingStop(decimal averageProfit, decimal maximumProfit, double stop)
        => averageProfit > (decimal)(stop == 0 ? 0.20 : stop + 0.05) * maximumProfit;

    /// <summary>Calculates reached = averageProfit &lt; (stop - 0.05) * maximumProfit; equality does not trigger.</summary>
    /// <param name="averageProfit">Current position profit in currency for monitoring; callers may supply the legacy ten-observation average for parity tests.</param>
    /// <param name="maximumProfit">Configured maximum profit in currency.</param>
    /// <param name="stop">The accepted trailing-stop fraction.</param>
    /// <returns>Whether the active trailing threshold has been breached.</returns>
    public static bool TrailingStopReached(decimal averageProfit, decimal maximumProfit, double stop)
        => averageProfit < ((decimal)stop - 0.05m) * maximumProfit;

    /// <summary>Copies the calculated business action and proposed stop fraction; mutation occurs only when its event is applied.</summary>
    /// <param name="snapshot">The immutable proposed snapshot.</param>
    /// <param name="action">The business action recommendation.</param>
    /// <param name="reason">The matched legacy rule.</param>
    /// <param name="severity">The legacy action severity.</param>
    /// <param name="description">The monitoring explanation.</param>
    /// <param name="stop">The proposed trailing-stop fraction.</param>
    /// <returns>A new snapshot; the source and its captured inputs remain unchanged.</returns>
    static IronCondorTradePlanSnapshot Action(IronCondorTradePlanSnapshot snapshot, ActionType action,
        ActionSubType reason, ActionState severity, string description, double stop)
        => snapshot with { ActionType = action.ToString(), ActionSubType = reason.ToString(),
            ActionState = severity.ToString(), ActionReason = description, StopLossLimit = stop };
}
