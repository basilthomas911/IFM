using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;

/// <summary>Resolves MarketCondition lifecycle policy without owning timeout mechanics or transport.</summary>
public static class ResolveMarketConditionExecutionPolicy
{
    /// <summary>Returns the domain clock and the stage's existing absolute deadline policy.</summary>
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this ExecuteMarketConditionAssessmentCommand command,
        FunctionFailureStage stage, IMarketConditionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        var deadline = stage == FunctionFailureStage.Loading
            ? context.TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(command.ParameterSet.MaximumExecutionMilliseconds)
            : command.ExpiresAtUtc;
        return new(context.TimeProvider, deadline);
    }
}
