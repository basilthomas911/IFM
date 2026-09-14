using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Model;

public sealed record VerticalSpreadTradePlan(StrategyTradePlanSnapshot Snapshot);

public sealed class VerticalSpreadTradePlanAlgorithm
{
    public VerticalSpreadTradePlan Calculate(StrategyPositionSnapshot position, TradePlanParameters parameters,
        StrategyTradePlanSnapshot? previous, DateOnly valueDate, DateTime nowUtc)
    {
        VerticalSpreadTradePlanValidation.Validate(position, parameters, valueDate, nowUtc);
        var opening = position.Legs.Sum(static leg => leg.OpeningPrice * leg.SignedQuantity);
        var current = position.Legs.Sum(static leg => leg.CurrentPrice * leg.SignedQuantity);
        var pnl = position.RealizedPnl + position.UnrealizedPnl;
        var forward = VerticalSpreadForwardTradePriceModel.Calculate(current, previous?.CurrentValue);
        var decision = VerticalSpreadExitConditionModel.Evaluate(position, pnl, forward - current, parameters, nowUtc);
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position, ValueDate = valueDate, PlanRevision = (previous?.PlanRevision ?? 0) + 1,
            OpeningValue = opening, CurrentValue = current, TotalPnl = pnl, ForwardTradePrice = forward,
            ForwardPnl = pnl + forward - current, ForwardLoss = Math.Max(0, -(pnl + forward - current)),
            State = decision.State, Action = decision.Action, RequiresExit = decision.RequiresExit,
            ReasonCode = decision.ReasonCode, Explanation = decision.Explanation, Parameters = parameters,
            CalculatedAtUtc = nowUtc
        };
        plan = plan with { MaterialChange = VerticalSpreadTradePlanComparisonModel.IsMaterial(previous, plan) };
        return new(plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) });
    }
}

public static class VerticalSpreadForwardTradePriceModel
{
    public static decimal Calculate(decimal currentValue, decimal? previousValue) =>
        previousValue is null ? currentValue : currentValue + currentValue - previousValue.Value;
}

public static class VerticalSpreadExitConditionModel
{
    public static VerticalSpreadTradePlanDecision Evaluate(StrategyPositionSnapshot position, decimal pnl,
        decimal forwardChange, TradePlanParameters parameters, DateTime nowUtc)
    {
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

public static class VerticalSpreadTradePlanComparisonModel
{
    public static bool IsMaterial(StrategyTradePlanSnapshot? previous, StrategyTradePlanSnapshot current) =>
        previous is null || current.RequiresExit || previous.State != current.State || previous.Action != current.Action ||
        previous.Position.Phase != current.Position.Phase ||
        Math.Abs(previous.TotalPnl - current.TotalPnl) >= current.Parameters.MaterialPnlChange ||
        Math.Abs(previous.CurrentValue - current.CurrentValue) >= current.Parameters.MaterialPriceChange;
}

public static class VerticalSpreadTradePlanValidation
{
    public static void Validate(StrategyPositionSnapshot position, TradePlanParameters parameters, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position); parameters.Validate();
        if (!position.Id.IsValid || position.StrategyKind != TradeStrategyKind.VerticalSpread || position.Legs.Length != 2 ||
            position.PositionSequence < 1 || position.RouteGeneration < 1 || valueDate == default || nowUtc.Kind != DateTimeKind.Utc ||
            position.AsOfUtc.Kind != DateTimeKind.Utc || position.Legs.Any(static x => x.TradeLegId == Guid.Empty || x.SignedQuantity == 0 || string.IsNullOrWhiteSpace(x.ContractId) || x.CurrentPrice <= 0))
            throw new ArgumentException("A valid two-leg Vertical Spread position snapshot is required.");
    }
}

public readonly record struct VerticalSpreadTradePlanDecision(TradePlanState State, TradePlanAction Action,
    bool RequiresExit, string ReasonCode, string Explanation);
