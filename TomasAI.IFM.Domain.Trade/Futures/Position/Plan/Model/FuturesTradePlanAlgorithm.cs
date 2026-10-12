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
            CalculatedAtUtc = nowUtc
        };
        plan = plan with
        {
            MaterialChange = FuturesTradePlanComparisonModel.IsMaterial(previous, plan)
        };
        return new(plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) });
    }
}
