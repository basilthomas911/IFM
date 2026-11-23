using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
public sealed class FuturesTradePlanAlgorithm
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the existing instance calculator contract used by dependency injection and its callers; calculation results do not depend on mutable service state.")]
    public FuturesTradePlan Calculate(StrategyPositionSnapshot position, TradePlanParameters parameters, StrategyTradePlanSnapshot? previous, DateOnly valueDate, DateTime nowUtc)
    {
        FuturesTradePlanValidation.Validate(position, parameters, valueDate, nowUtc);
        var leg = position.Legs[0];
        var opening = leg.OpeningPrice * leg.SignedQuantity;
        var current = leg.CurrentPrice * leg.SignedQuantity;
        var pnl = position.RealizedPnl + position.UnrealizedPnl;
        var forward = FuturesForwardTradePriceModel.Calculate(current, previous?.CurrentValue);
        var decision = FuturesExitConditionModel.Evaluate(position, pnl, forward - current, parameters, nowUtc);
        var trailingStop = FuturesTrailingStopModel.Calculate(position, parameters, previous, nowUtc);
        if (trailingStop.Reached && decision.ReasonCode is not ("PLAN.MAX_LOSS" or "PLAN.DAILY.SESSION_FLATTEN"))
            decision = new(TradePlanState.ExitRequired, TradePlanAction.ExitAtMarket, true,
                "PLAN.FUTURES.TRAILING_STOP", position.DailyFuturesEntry is { } entry
                    ? $"Frozen DC ATR(14)={entry.Threshold}; trail distance={entry.Threshold * (trailingStop.Tightened ? .5m : 1m)}; stop={trailingStop.StopPrice}; tightened={trailingStop.Tightened}."
                    : "Price retraced at least 10 points from its best observed price since entry.");
        if (position.IsOpen && position.DailyFuturesEntry is not null && previous is { RequiresExit: true })
        {
            decision = new(previous.State, previous.Action, true, previous.ReasonCode, previous.Explanation);
            trailingStop = new(previous.FuturesTrailingExtremePrice ?? leg.OpeningPrice,
                previous.FuturesStopPrice ?? trailingStop.StopPrice, false, previous.FuturesTrailTightened);
        }
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position,
            ValueDate = valueDate,
            PlanRevision = (previous?.PlanRevision ?? 0) + 1,
            OpeningValue = opening,
            CurrentValue = current,
            TotalPnl = pnl,
            ForwardTradePrice = forward,
            ForwardPnl = pnl + forward - current,
            ForwardLoss = Math.Max(0, -(pnl + forward - current)),
            State = decision.State,
            Action = decision.Action,
            RequiresExit = decision.RequiresExit,
            ReasonCode = decision.ReasonCode,
            Explanation = decision.Explanation,
            Parameters = parameters,
            CalculatedAtUtc = nowUtc,
            FuturesTrailingExtremePrice = trailingStop.ExtremePrice,
            FuturesStopPrice = position.DailyFuturesEntry is null ? null : trailingStop.StopPrice,
            FuturesTrailTightened = trailingStop.Tightened
        };
        plan = plan with
        {
            MaterialChange = FuturesTradePlanComparisonModel.IsMaterial(previous, plan)
        };
        return new(plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) });
    }
}
