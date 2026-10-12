using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;
public static class VerticalSpreadExitConditionModel
{
    public static VerticalSpreadTradePlanDecision Evaluate(StrategyPositionSnapshot position, decimal pnl, decimal forwardChange, TradePlanParameters parameters, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(position);
        if (!position.IsOpen || position.Phase == StrategyPositionPhase.Close)
            return new(TradePlanState.Closed, TradePlanAction.None, false, "PLAN.CLOSED", "Position is closed.");
        if (nowUtc - position.AsOfUtc > TimeSpan.FromSeconds(parameters.MaximumDataAgeSeconds))
            return new(TradePlanState.Hold, TradePlanAction.Hold, false, "PLAN.DATA.STALE", "Market data is stale.");
        if (-pnl >= parameters.MaximumLoss || -(pnl + forwardChange) >= parameters.MaximumLoss)
            return new(TradePlanState.ExitRequired, TradePlanAction.ExitAtMarket, true, "PLAN.MAX_LOSS", "Current or forward loss reached maximum loss.");
        if (pnl >= parameters.ProfitTarget)
            return new(TradePlanState.ExitRequired, TradePlanAction.ExitAtLimit, true, "PLAN.PROFIT_TARGET", "Profit target was reached.");
        if (-pnl >= parameters.WarningLoss)
            return new(TradePlanState.Warning, TradePlanAction.Monitor, false, "PLAN.LOSS_WARNING", "Loss is inside the warning band.");
        return new(TradePlanState.Normal, TradePlanAction.Monitor, false, "PLAN.NORMAL", "Position remains inside configured limits.");
    }
}
