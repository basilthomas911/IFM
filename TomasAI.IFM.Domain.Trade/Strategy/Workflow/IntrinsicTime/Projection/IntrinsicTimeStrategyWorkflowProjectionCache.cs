using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Projection;

/// <summary>Provides a thread-safe revision-monotonic cache of active workflow projections.</summary>
public sealed class IntrinsicTimeStrategyWorkflowProjectionCache
    : IIntrinsicTimeStrategyWorkflowProjectionCache
{
    /// <summary>Gets the process-wide workflow projection cache shared by projector and Query actor.</summary>
    public static IIntrinsicTimeStrategyWorkflowProjectionCache Shared { get; }
        = new IntrinsicTimeStrategyWorkflowProjectionCache();

    readonly ConcurrentDictionary<string, ActiveIntrinsicTimeStrategyWorkflowReadModel> _workflows =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(string workflowEntityId, out ActiveIntrinsicTimeStrategyWorkflowReadModel? workflow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityId);
        return _workflows.TryGetValue(workflowEntityId, out workflow);
    }

    /// <inheritdoc />
    public void Set(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow)
    {
        IsArgumentNull.Check(workflow);
        _workflows.AddOrUpdate(
            workflow.WorkflowEntityId,
            workflow,
            (_, current) => workflow.WorkflowRevision >= current.WorkflowRevision ? workflow : current);
    }

    /// <inheritdoc />
    public bool Remove(string workflowEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityId);
        return _workflows.TryRemove(workflowEntityId, out _);
    }

    /// <inheritdoc />
    public void Clear() => _workflows.Clear();
}
