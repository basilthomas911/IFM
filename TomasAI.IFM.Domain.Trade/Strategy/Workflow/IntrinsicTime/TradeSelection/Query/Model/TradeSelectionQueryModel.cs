using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;

/// <summary>Provides shared authorization and exact-read calculations for Trade Selection queries.</summary>
internal static class TradeSelectionQueryModel
{
    /// <summary>Authorizes access to the requested Portfolio and Fund.</summary>
    internal static async Task AuthorizeAsync(ITradeSelectionQueryContext services, SelectionQueryAccess access, int portfolioId, int fundId, CancellationToken cancellationToken)
    {
        if (access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length == 0)
            throw new UnauthorizedAccessException("Portfolio read authority is required.");
        using var scope = PortfolioAccessScope.Push(new() { Principal = access.Principal, Roles = access.Roles });
        var result = await services.PortfolioQueries.GetFundAsync(portfolioId, fundId, cancellationToken: cancellationToken);
        if (!result.Success || result.Value is null || result.Value.PortfolioId != portfolioId || result.Value.FundId != fundId)
            throw new UnauthorizedAccessException("Portfolio/Fund access was denied.");
    }

    /// <summary>Reads an exact invocation and determines whether the workflow accepted it.</summary>
    internal static async Task<TradeSelectionProjection> ExactAsync(ITradeSelectionQueryContext services, SelectionQueryAccess access, StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken)
    {
        var completed = await services.DbFactory.TradeDb.GetTradeSelectionInvocationAsync(workflowId, invocationId, cancellationToken)
            ?? throw new KeyNotFoundException("Exact selector invocation not found.");
        var result = TradeSelectionContracts.ReadResult(completed.Result);
        await AuthorizeAsync(services, access, result.PortfolioId, result.FundId, cancellationToken);
        var workflow = await services.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(workflowId, cancellationToken);
        var view = workflow is null ? null : MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(workflow.StatePayload);
        var accepted = view?.TradeSelection.Result?.PayloadSha256 == completed.Result.PayloadSha256 && view.TradeSelection.SourceEventId == completed.Id;
        return new(completed, accepted, view is null, !accepted && view is { Status: not WorkflowStrategyMachineStatus.Started });
    }
}