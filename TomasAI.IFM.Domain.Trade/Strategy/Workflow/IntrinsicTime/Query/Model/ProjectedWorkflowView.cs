using MessagePack;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

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
        var detail = await store.GetIntrinsicTimeStrategyWorkflowAsync(head.WorkflowId, token).ConfigureAwait(false);
        if (detail is null || detail.WorkflowId != head.WorkflowId || detail.WorkflowRevision < head.WorkflowRevision
            || !string.Equals(detail.WorkflowEntityId, entityId, StringComparison.Ordinal))
            throw new InvalidDataException("Workflow detail and entity projections disagree.");
        return Decode(detail, token);
    }

    internal static IntrinsicTimeStrategyWorkflowView Decode(IntrinsicTimeStrategyWorkflowReadModel detail, CancellationToken token)
    {
        if (detail.StateSchemaVersion is < 1 or > 2)
            throw new NotSupportedException($"Workflow projection schema {detail.StateSchemaVersion} requires migration.");
        var view = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(detail.StatePayload, cancellationToken: token);
        if (view is null || view.WorkflowId != detail.WorkflowId || view.WorkflowRevision != detail.WorkflowRevision
            || !string.Equals(view.EntityId.Format(), detail.WorkflowEntityId, StringComparison.Ordinal))
            throw new InvalidDataException("Workflow projection payload does not match its stored identity and revision.");
        return view;
    }
}
