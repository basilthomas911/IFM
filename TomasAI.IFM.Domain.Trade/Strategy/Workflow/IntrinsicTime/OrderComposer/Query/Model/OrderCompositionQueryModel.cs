using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
/// <summary>Provides shared authorization and exact-read calculations for order-composition queries.</summary>
internal static class OrderCompositionQueryModel
{
    internal static async Task AuthorizeAsync(IOrderCompositionQueryContext services, CompositionQueryAccess access, int portfolioId, int fundId, CancellationToken token)
    {
        if (access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length == 0)
            throw new UnauthorizedAccessException("Portfolio read authority is required.");
        using var scope = PortfolioAccessScope.Push(new() { Principal = access.Principal, Roles = access.Roles });
        var result = await (services.PortfolioQueries.GetFundAsync(portfolioId, fundId, cancellationToken: token)).ConfigureAwait(false);
        if (!result.Success || result.Value is null || result.Value.PortfolioId != portfolioId || result.Value.FundId != fundId)
            throw new UnauthorizedAccessException("Portfolio/Fund access was denied.");
    }

    internal static async Task<OrderCompositionProjection> ExactAsync(IOrderCompositionQueryContext services, CompositionQueryAccess access, StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token)
    {
        if (workflowId.Value == Guid.Empty || invocationId == Guid.Empty)
            throw new ArgumentException("Exact workflow and invocation required.", nameof(workflowId));
        var completed = await (services.DbFactory.TradeDb.GetOrderCompositionInvocationAsync(workflowId, invocationId, token)).ConfigureAwait(false) ?? throw new KeyNotFoundException("Exact composition invocation not found.");
        var result = OrderCompositionContracts.ReadResult(completed.Result);
        await (AuthorizeAsync(services, access, result.DecisionContext.PortfolioId, result.DecisionContext.FundId, token)).ConfigureAwait(false);
        var current = await ProjectedWorkflowView.ReadCurrentAsync(services.DbFactory.TradeDb.DbReader, completed.EntityId.Format(), token).ConfigureAwait(false);
        var view = current?.WorkflowId == workflowId ? current : null;
        var accepted = view?.OrderComposition.SourceEventId == completed.Id
            && string.Equals(view.OrderComposition.Result?.PayloadSha256, completed.Result.PayloadSha256, StringComparison.Ordinal);
        return new(completed, accepted, view is null, !accepted && view is { Status: not WorkflowStrategyMachineStatus.Started });
    }
}
