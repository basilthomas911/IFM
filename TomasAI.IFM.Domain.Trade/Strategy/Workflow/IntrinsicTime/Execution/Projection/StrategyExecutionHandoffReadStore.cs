using TomasAI.IFM.Application.Serialization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;

/// <summary>Reads projected handoff state and verifies command-ingress source references without replaying an aggregate.</summary>
public sealed class StrategyExecutionHandoffReadStore(IPostgresEventTransaction transactions)
{
    /// <summary>Validates ingress against the committed authorization, rather than trusting caller-supplied event metadata.</summary>
    public Task<bool> VerifyAuthorizationAsync(StrategyExecutionHandoffDefinition proposed, CancellationToken token = default)
        => transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var claimed = proposed.Authorization;
            var row = await db.ReadStrategyHandoffAuthorizationAsync(claimed.EventId, claimed.CommandId, ct).ConfigureAwait(false);
            return row?.ToDomainEvent() is WorkflowStrategyStateUpdatedEvent source &&
                source.Id == claimed.Id && source.WorkflowId == claimed.WorkflowId && source.WorkflowRevision == claimed.WorkflowRevision &&
                StrategyOpeningIntentFactory.Create(source, source.UpdatedAtUtc).Any(value => value.EntityId == proposed.Id &&
                    value.Intent.IntentHash == proposed.IntentHash && FinancialCanonicalHash.Compute(value.Intent.Authorization) == FinancialCanonicalHash.Compute(claimed) &&
                    FinancialCanonicalHash.Compute(value.Intent.Order) == FinancialCanonicalHash.Compute(proposed.Order));
        }, token);

    public Task ProjectAsync(StrategyExecutionHandoffDefinition value, CancellationToken token = default)
        => transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.WriteStrategyHandoffAsync(value, ct).ConfigureAwait(false);
            return true;
        }, token);

    public Task<StrategyExecutionHandoffDefinition?> ReadAsync(OrderExecutionId id, CancellationToken token = default)
        => transactions.ExecuteOnceAsync((db, ct) => db.ReadStrategyHandoffAsync(id.Format(), ct), token);

    public Task<IReadOnlyList<StrategyExecutionHandoffDefinition>> PendingAsync(string after, CancellationToken token = default)
        => transactions.ExecuteOnceAsync((db, ct) => db.ReadPendingStrategyHandoffsAsync(after, ct), token);

    public Task<IReadOnlyList<StrategyExecutionHandoffDefinition>> ReadWorkflowAsync(Guid workflow, CancellationToken token = default)
        => transactions.ExecuteOnceAsync((db, ct) => db.ReadWorkflowStrategyHandoffsAsync(workflow, ct), token);
}
