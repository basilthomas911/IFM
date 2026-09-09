using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function;

/// <summary>Resolves RiskManagement lifecycle policy without owning timeout mechanics or transport.</summary>
public static class ResolveRiskManagementExecutionPolicy
{
    /// <summary>Returns the domain clock and the stage's existing absolute deadline policy.</summary>
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this ExecuteRiskManagementPipelineCommand command,
        FunctionFailureStage stage, IRiskManagementFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var budget = stage == FunctionFailureStage.Loading ? 1000 : stage == FunctionFailureStage.Execution ? 250 : 1000;
        var deadline = stage == FunctionFailureStage.Loading ? now.AddMilliseconds(budget)
            : new DateTime(Math.Min(command.ExpiresAtUtc.Ticks, now.AddMilliseconds(budget).Ticks), DateTimeKind.Utc);
        return new(context.TimeProvider, deadline);
    }
}
