using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Verifies ownership against the committed strategy approval rather than caller-supplied order fields.</summary>
public sealed class StrategyFundOrderRegistrationProof(IPostgresEventTransaction transactions)
{
    /// <summary>Requires both the Trade owner's no-execution fence and the matching immutable financial release.</summary>
    public Task<bool> VerifyExpiryAsync(ReleaseExpiredStrategyFundOrderCommand command, CancellationToken token = default)
        => transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync(ExecutionCapacitySql.Source, [command.SourceCommandId],
                row => new EventLogReadModel(row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetInt64(3),
                    row.GetFieldValue<byte[]>(4), row.GetGuid(5), row.GetString(6), row.GetInt64(7)), ct).ConfigureAwait(false);
            var source = rows.Select(row => row.ToDomainEvent()).OfType<TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderChangedEvent>().SingleOrDefault();
            var order = source?.TradeOrderDefinition;
            if (source is null || source.EventId <= 0 || order is not
                { Status: TomasAI.IFM.Domain.Trade.Shared.TradeOrderStatus.Expired, BoundExecutionAttemptId: null, StrategyWorkflowId: not null } ||
                order.Id.PortfolioId != command.EntityId.PortfolioId || order.Id.FundId != command.EntityId.FundId || order.Id.OrderId != command.OrderId)
                return false;
            var receipts = await db.QueryAsync(PortfolioDbSql.Financial.FinancialQueryStore.ReadOutcome,
                [command.EntityId.PortfolioId, command.FinancialOperationId], row => (Type: row.GetString(0), Payload: row.GetString(1)), ct).ConfigureAwait(false);
            if (receipts.Count != 1 || receipts[0].Type != typeof(LedgerPostingBatchCompletedEvent).FullName)
                return false;
            var batch = Financial.Repository.FinancialRepositorySupport.Decode<LedgerPostingBatchCompletedEvent>(receipts[0].Payload);
            return batch.ProjectionFacts?.ExecutionCapacity?.Outcome is { UnsubmittedExpiry: true, FilledUnits: 0 } facts &&
                facts.OrderId == command.OrderId && facts.FundId == command.EntityId.FundId && facts.CancelledUnits == facts.ApprovedUnits &&
                facts.SourceCommandId == command.SourceCommandId && facts.ExecutionHash == FinancialCanonicalHash.Compute(order);
        }, token);

    public Task<bool> VerifyAsync(RegisterStrategyFundOrderCommand command, CancellationToken token = default)
        => transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var rows = await db.QueryAsync(ExecutionCapacitySql.Source, [command.SourceCommandId],
                row => new EventLogReadModel(row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetInt64(3),
                    row.GetFieldValue<byte[]>(4), row.GetGuid(5), row.GetString(6), row.GetInt64(7)), ct).ConfigureAwait(false);
            var source = rows.Select(row => row.ToDomainEvent()).OfType<WorkflowStrategyStateUpdatedEvent>()
                .SingleOrDefault(value => value.EventId == command.SourceEventVersion);
            var view = source?.State;
            if (view is not { Status: WorkflowStrategyMachineStatus.Completed, Outcome: StrategyWorkflowOutcome.Completed } ||
                view.SelectionBinding?.SchemaVersion != 2 || view.OpeningExecutionRoute is null ||
                command.UnderlyingContractId != view.TriggerEvent.EntityId.ContractId ||
                view.WorkflowId.Value != command.WorkflowId || command.Order.Id.PortfolioId != command.EntityId.PortfolioId ||
                command.Order.Id.FundId != command.EntityId.FundId)
                return false;
            var original = view.PortfolioRiskDecision?.TradeOrders.SingleOrDefault(order => order.Id.OrderId == command.Order.Id.OrderId && order.Id.FundId == command.EntityId.FundId);
            return original is not null && FinancialCanonicalHash.Compute(original.ToPortfolioInstruction()) ==
                FinancialCanonicalHash.Compute(command.Order with { SetupTrade = null });
        }, token);
}
