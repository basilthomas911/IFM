using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Applies the common start invariants used by every strategy-specific exit workflow.</summary>
public static class ExitPositionWorkflowStart
{
    /// <summary>Computes exact exit-plan ownership and lifecycle guards without mutation.</summary>
    /// <param name="command">The concrete exit intent.</param>
    /// <param name="started">The committed prior start, if any.</param>
    /// <param name="plan">The committed exit plan.</param>
    /// <param name="strategyKind">The expected owning strategy.</param>
    /// <param name="sourcePlanEventId">The source event authorizing exit.</param>
    /// <returns>The proposed immutable exit workflow or business rejection.</returns>
    internal static ExitPositionWorkflowChange Compute(ICommand<ExitPositionWorkflowId> command,
        ExitPositionWorkflowStartedEvent? started, StrategyTradePlanSnapshot plan, TradeStrategyKind strategyKind, Guid sourcePlanEventId)
    {
        if (started is not null)
            return started.SourcePlanEventId == sourcePlanEventId
                ? new(plan, strategyKind, sourcePlanEventId, AlreadyStarted: true)
                : new(plan, strategyKind, sourcePlanEventId, RejectionReason:
                    "EXIT.WORKFLOW.ALREADY_STARTED; the stream belongs to a different exit decision.");

        if (!command.EntityId.IsValid || sourcePlanEventId == Guid.Empty ||
            plan.Position.Id != command.EntityId.Position || plan.ValueDate != command.EntityId.ValueDate ||
            plan.Position.StrategyKind != strategyKind || plan.State != TradePlanState.ExitRequired ||
            !plan.RequiresExit || plan.Action is not (TradePlanAction.ExitAtLimit or TradePlanAction.ExitAtMarket))
            return new(plan, strategyKind, sourcePlanEventId, RejectionReason:
                "EXIT.WORKFLOW.INVALID_START; a committed exit-required plan with exact identity is required.");

        return new(plan, strategyKind, sourcePlanEventId);
    }
}
/// <summary>Immutable exit workflow proposal retaining exact plan identity.</summary>
internal sealed record ExitPositionWorkflowChange(StrategyTradePlanSnapshot ExitPlan, TradeStrategyKind StrategyKind, Guid SourcePlanEventId,
    bool AlreadyStarted = false, string? RejectionReason = null);
