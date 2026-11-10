using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Checks terminal claims against durable execution, accounting and Fund projections at command ingress.</summary>
public sealed class StrategyOpeningEvidenceVerifier(IDbContextFactory databases, IPortfolioFinancialApi financial, IPortfolioQueryApi portfolio)
{
    public async Task<bool> VerifyAsync(StrategyExecutionHandoffDefinition proposed, CancellationToken token = default)
    {
        if (proposed.FinancialReceipt is null || proposed.FundEvidence is null)
            return false;
        Guid operation;
        if (proposed.Phase == StrategyExecutionHandoffPhase.Expired)
        {
            var order = await databases.TradeDb.GetTradeOrderAsync(proposed.Order.Id, token).ConfigureAwait(false);
            if (proposed.Execution is not null || order is not { Status: Shared.TradeOrderStatus.Expired, BoundExecutionAttemptId: null } ||
                proposed.FinancialReceipt.ProjectionFacts?.ExecutionCapacity?.Outcome is not { UnsubmittedExpiry: true } facts ||
                facts.ExecutionHash != FinancialCanonicalHash.Compute(order)) return false;
            operation = TradeHandoffIdentity.Create("strategy-expiry-accounting", proposed.Id.Format());
        }
        else
        {
            if (proposed.Execution is not { } execution) return false;
            var actual = await databases.TradeDb.GetOrderExecutionAsync(proposed.Order.Id, proposed.Id.ExecutionAttemptId, token).ConfigureAwait(false);
            if (actual is null || FinancialCanonicalHash.Compute(actual) != FinancialCanonicalHash.Compute(execution)) return false;
            operation = TradeHandoffIdentity.Create("strategy-accounting", execution.Id.Format() + "|" + FinancialCanonicalHash.Compute(execution));
        }
        var scope = new FinancialReadScope { PortfolioId = proposed.Order.Id.PortfolioId, FundId = proposed.Order.Id.FundId,
            Access = new("StrategyExecutionHandoff", ["LedgerRead"], [proposed.Order.Id.PortfolioId]) };
        var receipt = await financial.GetPostingReceiptAsync(scope, new(operation), token).ConfigureAwait(false);
        if (!receipt.Success || receipt.Value?.Value?.Batch is not { } batch ||
            FinancialCanonicalHash.Compute(batch) != FinancialCanonicalHash.Compute(proposed.FinancialReceipt)) return false;
        var fund = await portfolio.GetOrderAsync(proposed.Order.Id.OrderId, token).ConfigureAwait(false);
        return fund.Success && fund.Value is not null &&
            FinancialCanonicalHash.Compute(fund.Value) == FinancialCanonicalHash.Compute(proposed.FundEvidence);
    }
}
