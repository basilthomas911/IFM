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
    /// <summary>Validates and records the first exit-workflow start for a strategy position.</summary>
    /// <typeparam name="TState">The concrete strategy workflow command state.</typeparam>
    /// <param name="command">The start command.</param>
    /// <param name="state">The event-sourced workflow state.</param>
    /// <param name="plan">The committed Trade Plan requiring an exit.</param>
    /// <param name="strategyKind">The strategy owned by the workflow.</param>
    /// <param name="sourcePlanEventId">The event that authorized the exit.</param>
    /// <returns>A successful command identifier or a classified business rejection.</returns>
    public static ServiceResult<GuidResult> Apply<TState>(ICommand<ExitPositionWorkflowId> command,
        TState state, StrategyTradePlanSnapshot plan, TradeStrategyKind strategyKind, Guid sourcePlanEventId)
        where TState : ExitPositionWorkflowCommandStateBase<TState>
    {
        if (state.Started is not null)
            return state.Started.SourcePlanEventId == sourcePlanEventId
                ? new ServiceOk<GuidResult>(new(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode,
                    "EXIT.WORKFLOW.ALREADY_STARTED; the stream belongs to a different exit decision.");

        if (!command.EntityId.IsValid || sourcePlanEventId == Guid.Empty ||
            plan.Position.Id != command.EntityId.Position || plan.ValueDate != command.EntityId.ValueDate ||
            plan.Position.StrategyKind != strategyKind || plan.State != TradePlanState.ExitRequired ||
            !plan.RequiresExit || plan.Action is not (TradePlanAction.ExitAtLimit or TradePlanAction.ExitAtMarket))
            return new ServiceFailed<GuidResult>(command.ErrorCode,
                "EXIT.WORKFLOW.INVALID_START; a committed exit-required plan with exact identity is required.");

        state.Update(new ExitPositionWorkflowStartedEvent
        {
            EntityId = command.EntityId,
            ExitPlan = plan,
            StrategyKind = strategyKind,
            SourcePlanEventId = sourcePlanEventId
        }, (ICommand)command);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
}
