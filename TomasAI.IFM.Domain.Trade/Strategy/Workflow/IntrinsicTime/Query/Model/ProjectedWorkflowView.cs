using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

/// <summary>Reads and verifies a complete workflow view from persisted projections.</summary>
internal static class ProjectedWorkflowView
{
    internal static async ValueTask<IntrinsicTimeStrategyWorkflowView?> ReadCurrentAsync(
        ITradeDbReadContext store, string entityId, CancellationToken token)
    {
        var history = await store.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
            entityId, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 1, token).ConfigureAwait(false);
        var head = history.SingleOrDefault();
        if (head is null)
            return null;
        var view = await store.GetIntrinsicTimeStrategyWorkflowViewAsync(head.WorkflowId, token).ConfigureAwait(false);
        if (view is null || view.WorkflowId != head.WorkflowId || view.WorkflowRevision < head.WorkflowRevision
            || !string.Equals(view.EntityId.Format(), entityId, StringComparison.Ordinal))
            throw new InvalidDataException("Workflow detail and entity projections disagree.");
        return view;
    }
}
