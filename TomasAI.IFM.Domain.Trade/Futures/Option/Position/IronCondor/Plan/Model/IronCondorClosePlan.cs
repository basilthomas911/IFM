using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Retains the authorizing decision while recording confirmed close execution facts.</summary>
internal static class IronCondorClosePlan
{
    internal static bool IsSettled(StrategyPositionSnapshot position) => !position.IsOpen
        && position.Phase == StrategyPositionPhase.Close && position.ClosingFills.Length > 0
        && position.Legs.Length == 4 && position.Legs.All(leg => leg.SignedQuantity == 0);

    internal static StrategyTradePlanSnapshot Create(StrategyTradePlanSnapshot decision, StrategyPositionSnapshot position,
        string progress, long revision, DateTime nowUtc)
    {
        if (!decision.RequiresExit || decision.Position.Id != position.Id || string.IsNullOrWhiteSpace(progress)
            || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A matching authorizing exit decision and confirmed close progress are required.");
        var closed = IsSettled(position);
        var gross = position.ContractCashMultiplier is > 0 ?
            (position.RealizedPnl + position.UnrealizedPnl) * position.ContractCashMultiplier.Value : (decimal?)null;
        var net = gross.HasValue && position.OpeningCommission.HasValue ?
            gross - position.OpeningCommission - position.ClosingFills.Sum(fill => fill.Commission) : null;
        var finalFillAt = position.ClosingFills.Select(fill=>fill.FilledAtUtc).Where(at=>at.Kind==DateTimeKind.Utc && at!=default)
            .DefaultIfEmpty(position.AsOfUtc).Max();
        var timing = closed ? $"Last closing fill at {finalFillAt:O}; closed position confirmed at {position.AsOfUtc:O}." : "Position remains open until all closing fills and settlement are confirmed.";
        var fillDebit=position.ClosingFills.Sum(fill=>fill.Price*fill.SignedQuantity);
        var description = string.Create(CultureInfo.InvariantCulture,
            $"Close triggered {decision.CalculatedAtUtc:O}: {decision.ReasonCode}. {decision.Explanation} Execution: {progress}. Decision frozen; no new exit evaluation. {timing} Recorded closing-fill debit: {fillDebit} quantity-weighted points. Gross P&L: {gross?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}; net P&L after recorded commissions: {net?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}.");
        var plan = decision with {
            Position = position, PlanRevision = revision, CalculatedAtUtc = nowUtc, MaterialChange = true,
            CurrentValue = position.MarketValue, TotalPnl = position.RealizedPnl + position.UnrealizedPnl,
            State = closed ? TradePlanState.Closed : TradePlanState.ExitRequired,
            Action = closed ? TradePlanAction.None : decision.Action, RequiresExit = !closed,
            Explanation = description,
            IronCondorTradePlanSnapshot = decision.IronCondorTradePlanSnapshot is { } captured ? captured with {
                Position = position, SequenceId = revision, ActionDate = nowUtc, ActionDateTime=nowUtc, TradePnl = net,
                DailyPnl=position.DailyPnl, DailyLoss=position.DailyPnl.HasValue ? Math.Max(0,-position.DailyPnl.Value) : null,
                CurrentPositionValue=position.ContractCashMultiplier is >0 ? position.MarketValue*position.ContractCashMultiplier.Value : null,
                Quantity=closed ? 0 : captured.Quantity,
                NetDelta=closed ? 0 : captured.NetDelta, NetGamma=closed ? 0 : captured.NetGamma,
                NetVega=closed ? 0 : captured.NetVega, NetTheta=closed ? 0 : captured.NetTheta,
                EstimatedCloseValue=closed ? 0 : captured.EstimatedCloseValue,
                EstimatedExitCommission=closed ? 0 : captured.EstimatedExitCommission,
                EstimatedExitSlippage=closed ? 0 : captured.EstimatedExitSlippage,
                EstimatedExitCosts=closed ? 0 : captured.EstimatedExitCosts,
                ActionType = "Exit", ActionState = closed ? "Closed" : "Closing",
                ActionReason = description, ExitReason = description, ExitRecommended = !closed
            } : null
        };
        return plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
    }
}
