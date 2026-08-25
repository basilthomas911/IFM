using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;

/// <summary>Defines the process-local immutable cache for active Intrinsic Time Strategy workflow projections.</summary>
public interface IIntrinsicTimeStrategyWorkflowProjectionCache
{
    /// <summary>Gets an active projection when the cache contains the workflow entity.</summary>
    bool TryGet(string workflowEntityId, out ActiveIntrinsicTimeStrategyWorkflowReadModel? workflow);

    /// <summary>Stores the latest active projection when its revision is not older than the cached revision.</summary>
    void Set(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow);

    /// <summary>Removes the active projection for a terminal workflow entity.</summary>
    bool Remove(string workflowEntityId);

    /// <summary>Removes all cached active projections, primarily for deterministic rebuild and tests.</summary>
    void Clear();
}
