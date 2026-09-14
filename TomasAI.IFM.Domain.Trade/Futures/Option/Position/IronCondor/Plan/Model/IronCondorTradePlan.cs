using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

public sealed record IronCondorTradePlan(StrategyTradePlanSnapshot Snapshot);

public sealed class IronCondorTradePlanAlgorithm
{
    public IronCondorTradePlan Calculate(StrategyPositionSnapshot position, TradePlanParameters parameters,
        StrategyTradePlanSnapshot? previous, DateOnly valueDate, DateTime nowUtc)
    {
        IronCondorTradePlanValidation.Validate(position, parameters, valueDate, nowUtc);
        var opening = position.Legs.Sum(static leg => leg.OpeningPrice * leg.SignedQuantity);
        var current = position.Legs.Sum(static leg => leg.CurrentPrice * leg.SignedQuantity);
        var pnl = position.RealizedPnl + position.UnrealizedPnl;
        var forward = IronCondorForwardTradePriceModel.Calculate(current, previous?.CurrentValue);
        var decision = IronCondorExitConditionModel.Evaluate(position, pnl, forward - current, parameters, nowUtc);
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position,
            ValueDate = valueDate,
            PlanRevision = (previous?.PlanRevision ?? 0) + 1,
            OpeningValue = opening,
            CurrentValue = current,
            TotalPnl = pnl,
            ForwardTradePrice = forward,
            ForwardPnl = pnl + (forward - current),
            ForwardLoss = Math.Max(0, -(pnl + (forward - current))),
            State = decision.State,
            Action = decision.Action,
            RequiresExit = decision.RequiresExit,
            ReasonCode = decision.ReasonCode,
            Explanation = decision.Explanation,
            Parameters = parameters,
            CalculatedAtUtc = nowUtc
        };
        plan = plan with { MaterialChange = IronCondorTradePlanComparisonModel.IsMaterial(previous, plan) };
        return new(plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) });
    }
}

public static class IronCondorForwardTradePriceModel
{
    public static decimal Calculate(decimal currentValue, decimal? previousValue) =>
        previousValue is null ? currentValue : currentValue + (currentValue - previousValue.Value);
}

public static class IronCondorExitConditionModel
{
    public static TradePlanDecision Evaluate(StrategyPositionSnapshot position, decimal pnl, decimal forwardChange,
        TradePlanParameters parameters, DateTime nowUtc)
    {
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

public static class IronCondorTradePlanComparisonModel
{
    public static bool IsMaterial(StrategyTradePlanSnapshot? previous, StrategyTradePlanSnapshot current) =>
        previous is null || current.RequiresExit || previous.State != current.State || previous.Action != current.Action ||
        previous.Position.Phase != current.Position.Phase ||
        Math.Abs(previous.TotalPnl - current.TotalPnl) >= current.Parameters.MaterialPnlChange ||
        Math.Abs(previous.CurrentValue - current.CurrentValue) >= current.Parameters.MaterialPriceChange;
}

public static class IronCondorTradePlanValidation
{
    public static void Validate(StrategyPositionSnapshot position, TradePlanParameters parameters,
        DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        if (!position.Id.IsValid || position.StrategyKind != TradeStrategyKind.IronCondor || position.Legs.Length != 4 ||
            position.PositionSequence < 1 || position.RouteGeneration < 1 || valueDate == default || nowUtc.Kind != DateTimeKind.Utc ||
            position.AsOfUtc == default || position.AsOfUtc.Kind != DateTimeKind.Utc ||
            position.Legs.Any(static leg => leg.TradeLegId == Guid.Empty || leg.SignedQuantity == 0 || string.IsNullOrWhiteSpace(leg.ContractId) || leg.CurrentPrice <= 0) ||
            position.Legs.Select(static leg => leg.TradeLegId).Distinct().Count() != 4)
            throw new ArgumentException("A valid four-leg Iron Condor position snapshot is required.");
    }
}

public readonly record struct TradePlanDecision(TradePlanState State, TradePlanAction Action, bool RequiresExit,
    string ReasonCode, string Explanation);
