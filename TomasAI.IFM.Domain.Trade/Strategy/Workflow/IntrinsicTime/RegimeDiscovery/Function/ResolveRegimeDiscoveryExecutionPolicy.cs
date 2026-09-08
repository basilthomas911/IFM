using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;

/// <summary>Resolves RegimeDiscovery lifecycle policy without owning timeout mechanics or transport.</summary>
public static class ResolveRegimeDiscoveryExecutionPolicy
{
    /// <summary>Returns the domain clock and the stage's existing absolute deadline policy.</summary>
    public static FunctionExecutionPolicy ResolveExecutionPolicy(this ExecuteRegimeDiscoveryPipelineCommand command,
        FunctionFailureStage stage, IRegimeDiscoveryFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        // Regime Discovery historically bounded capture/calculation only; other stages remain explicitly unbounded.
        return new(context.TimeProvider, stage == FunctionFailureStage.Execution ? command.ExpiresAtUtc : null);
    }
}
