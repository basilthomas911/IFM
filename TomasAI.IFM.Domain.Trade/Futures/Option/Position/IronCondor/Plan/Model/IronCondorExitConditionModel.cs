using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
public static class IronCondorExitConditionModel
{
    public static TradePlanDecision Evaluate(StrategyPositionSnapshot position, decimal pnl, decimal forwardChange, TradePlanParameters parameters, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(position);
        if (!position.IsOpen || position.Phase == StrategyPositionPhase.Close)
            return new(TradePlanState.Closed, TradePlanAction.None, false, "PLAN.CLOSED", "Position is closed.");
        if (nowUtc - position.AsOfUtc > TimeSpan.FromSeconds(parameters.MaximumDataAgeSeconds))
            return new(TradePlanState.Hold, TradePlanAction.Hold, false, "PLAN.DATA.STALE", "Market data is stale; hold and await a current position update.");
        if (-pnl >= parameters.MaximumLoss)
            return new(TradePlanState.ExitRequired, TradePlanAction.ExitAtMarket, true, "PLAN.MAX_LOSS", "Maximum loss was reached; close all remaining legs at market.");
        if (pnl >= parameters.ProfitTarget)
            return new(TradePlanState.ExitRequired, TradePlanAction.ExitAtLimit, true, "PLAN.PROFIT_TARGET", "Profit target was reached; close all remaining legs.");
        if (-(pnl + forwardChange) >= parameters.MaximumLoss)
            return new(TradePlanState.ExitRequired, TradePlanAction.ExitAtMarket, true, "PLAN.FORWARD_MAX_LOSS", "Forward loss reaches the maximum-loss limit.");
        if (-pnl >= parameters.WarningLoss)
            return new(TradePlanState.Warning, TradePlanAction.Monitor, false, "PLAN.LOSS_WARNING", "Loss is inside the warning band.");
        return new(TradePlanState.Normal, TradePlanAction.Monitor, false, "PLAN.NORMAL", "Position remains inside configured limits.");
    }
}
