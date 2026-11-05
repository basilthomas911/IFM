using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;

/// <summary>Resolves TradeSelection lifecycle policy without owning timeout mechanics or transport.</summary>
public static class ResolveTradeSelectionExecutionPolicy
{

    extension(ExecuteTradeSelectionPipelineCommand command)
    {
        /// <summary>Returns the domain clock and the stage's existing absolute deadline policy.</summary>
        public FunctionExecutionPolicy ResolveExecutionPolicy(FunctionFailureStage stage, ITradeSelectionFunctionContext context)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            var deadline = stage == FunctionFailureStage.Loading
                ? context.TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(TradeSelectionContracts.CommonPolicy(command.SelectionBinding).MaximumExecutionMilliseconds)
                : command.ExpiresAtUtc;
            // Finishing an already calculated durable result has its own
            // bounded budget. The workflow still enforces the evidence expiry.
            if (stage == FunctionFailureStage.Persistence)
                deadline = new[] { command.ExpiresAtUtc.AddSeconds(2), command.WorkflowView.ExpiresAtUtc }.Min();
            return new(context.TimeProvider, deadline);
        }
    }
}
