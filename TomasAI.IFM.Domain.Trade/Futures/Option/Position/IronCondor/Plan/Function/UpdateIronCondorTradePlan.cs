using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;

public static class UpdateIronCondorTradePlan
{
    /// <summary>Calculates a full current-position snapshot; the last source snapshot supplies the revision and accepted stop ratio.</summary>
    /// <param name="command">The current coherent position and captured monitoring inputs.</param>
    /// <param name="state">The single latest source snapshot loaded for this request.</param>
    /// <param name="context">The pure valuation capability.</param>
    /// <param name="dispatch">Creates the result event with the originating command identity.</param>
    /// <param name="cancellationToken">Cancels before calculation begins.</param>
    /// <returns>The calculated snapshot event. The repository commits it before attempting Scylla projection.</returns>
    public static ValueTask<FunctionResult<IronCondorTradePlanUpdatedEvent,
        TradePlanFailedEvent<IronCondorTradePlanId>>> ExecuteAsync(
        this UpdateIronCondorTradePlanCommand command,
        IronCondorTradePlanFunctionState state,
        IIronCondorTradePlanFunctionContext context,
        Func<FunctionEventContext<UpdateIronCondorTradePlanCommand>,
            FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>> dispatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.LatestSourceTradePlan is { } latest && (command.RequestedAtUtc < latest.CalculatedAtUtc
            || command.Position.RouteGeneration < latest.Position.RouteGeneration
            || command.Position.RouteGeneration == latest.Position.RouteGeneration && command.Position.PositionSequence < latest.Position.PositionSequence))
            throw new InvalidOperationException("IronCondorTradePlan.OBSERVATION.SUPERSEDED: a newer source snapshot is already persisted.");
        // Valuation uses current inputs; only revision and accepted stop come from the single latest source snapshot.
        var calculated = context.Algorithm.Calculate(command.Position, command.Parameters,
            null, command.EntityId.ValueDate, command.RequestedAtUtc).Snapshot with
        {
            PlanRevision = checked(state.LatestPlanRevision + 1),
            MaterialChange = true
        };
        var inputs = CaptureCurrentRuleInputs(command.Position, command.IronCondorTradePlanInputs, state.LatestTradePlanSnapshot);
        if (inputs?.AverageTradePnl is { } currentPnl)
            calculated = calculated with { TotalPnl = currentPnl };
        var monitoring = TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model.IronCondorMonitoringSnapshotCompute.Create(
            calculated, command.SourceEventId, inputs);
        calculated = ApplyMonitoringRecommendation(calculated, monitoring);
        calculated = calculated with { ContentHash = TradePlanContractIdentity.PlanHash(calculated) };
        return ValueTask.FromResult(dispatch(new(typeof(IronCondorTradePlanUpdatedEvent), command, calculated)));
    }

    /// <summary>Uses current position PnL in currency and the accepted stop in the single loaded source snapshot.</summary>
    /// <param name="position">Current point-based position valuation and actual closing commissions.</param>
    /// <param name="inputs">Captured contract multiplier and opening execution commissions.</param>
    /// <param name="latestSnapshot">Only the last source snapshot; never rolling profit or plan history.</param>
    /// <returns>Current rule observations; initial stop is explicitly zero until a snapshot accepts a trailing stop.</returns>
    internal static IronCondorTradePlanInputs? CaptureCurrentRuleInputs(
        TomasAI.IFM.Domain.Trade.Shared.StrategyPositionSnapshot position, IronCondorTradePlanInputs? inputs,
        IronCondorTradePlanSnapshot? latestSnapshot)
    {
        if (inputs is null) return null;
        var currentPnl = inputs.ContractCashMultiplier is > 0 && inputs.OpeningCommission is >= 0
            ? (position.UnrealizedPnl + position.RealizedPnl) * inputs.ContractCashMultiplier.Value
                - inputs.OpeningCommission.Value - position.ClosingFills.Sum(fill => fill.Commission)
            : (decimal?)null;
        return inputs with
        {
            AverageTradePnl = currentPnl,
            StopLossLimit = latestSnapshot?.StopLossLimit is { } stop && double.IsFinite(stop) && stop >= 0 ? stop : 0,
            ForwardLossLimit = inputs.ForwardLossLimit ?? TomasAI.IFM.Domain.Trade.Shared.ForwardLossLimitType.Unknown
        };
    }

    /// <summary>Maps a computed legacy recommendation to the current monitoring contract without dispatching exit orders.</summary>
    /// <param name="plan">The coherent position valuation.</param>
    /// <param name="monitoring">The captured legacy calculation and unavailable reasons.</param>
    /// <returns>A monitoring plan that cannot report normal readiness or recommend exit from missing inputs.</returns>
    internal static StrategyTradePlanSnapshot ApplyMonitoringRecommendation(StrategyTradePlanSnapshot plan,
        IronCondorTradePlanSnapshot monitoring)
    {
        if (!monitoring.IsComplete)
            return plan with { IronCondorTradePlanSnapshot = monitoring, State = TradePlanState.CalculationFailed,
                Action = TradePlanAction.Hold, RequiresExit = false, ReasonCode = "IronCondorTradePlan.INPUTS.UNAVAILABLE",
                Explanation = string.Join("; ", monitoring.UnavailableReasons) };
        var exit = monitoring.ActionType == nameof(TomasAI.IFM.Domain.Trade.Shared.ActionType.ExitTradePosition);
        var state = monitoring.ActionState switch
        {
            nameof(TomasAI.IFM.Domain.Trade.Shared.ActionState.RedAlert) => exit ? TradePlanState.ExitRequired : TradePlanState.Breached,
            nameof(TomasAI.IFM.Domain.Trade.Shared.ActionState.Critical) => TradePlanState.Breached,
            nameof(TomasAI.IFM.Domain.Trade.Shared.ActionState.Warning) => TradePlanState.Warning,
            _ => TradePlanState.Normal
        };
        return plan with { IronCondorTradePlanSnapshot = monitoring, State = state,
            Action = exit ? TradePlanAction.ExitAtMarket : TradePlanAction.Monitor, RequiresExit = exit,
            ReasonCode = $"IronCondorTradePlan.{monitoring.ActionSubType}", Explanation = monitoring.ActionReason ?? string.Empty };
    }
}
