using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;

/// <summary>Resolves OrderComposition lifecycle policy without owning timeout mechanics or transport.</summary>
public static class ResolveOrderCompositionExecutionPolicy
{
    /// <summary>Returns the domain clock and the stage's existing absolute deadline policy.</summary>
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this ExecuteOrderCompositionPipelineCommand command,
        FunctionFailureStage stage, IOrderCompositionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        var deadline = stage == FunctionFailureStage.Loading
            ? context.TimeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(command.CompositionBinding.Rules.VariantRules.Single(x => x.VariantKey == command.CompositionBinding.Selected.VariantKey).BaseParameters.LoadingMilliseconds)
            : command.ExpiresAtUtc;
        return new(context.TimeProvider, deadline);
    }
}
