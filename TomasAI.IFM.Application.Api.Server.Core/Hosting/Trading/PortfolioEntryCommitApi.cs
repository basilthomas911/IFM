using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Trading;

/// <summary>Financial effects belong here, after decisions. No method retries the eligibility calculation.</summary>
public sealed class PortfolioEntryCommitApi(PortfolioOrderCompositionStore store, IPortfolioBusinessIdAllocator identities,
    WorkflowDecisionSession session) : IPortfolioEntryCommitApi
{
    public async Task<PortfolioEntryCommitResult> CommitOnceAsync(PortfolioEntryApproval approval, CancellationToken cancellationToken)
    {
        if (approval.DecisionSessionId != session.Id)
            return await ReconcileAsync(approval, cancellationToken).ConfigureAwait(false);
        try
        {
            var completed = await store.CommitApprovedAsync(approval, identities, cancellationToken).ConfigureAwait(false);
            return new(PortfolioEntryCommitOutcome.Committed, completed, "ENTRY.COMMIT.CONFIRMED");
        }
        catch (Exception error) when (error is FinancialOperationException or ArgumentException or TimeoutException)
        { return new(PortfolioEntryCommitOutcome.Rejected, null, error.Message); }
        catch (Exception)
        { return new(PortfolioEntryCommitOutcome.Unknown, null, "ENTRY.COMMIT.OUTCOME_UNKNOWN"); }
    }

    public Task<PortfolioEntryCommitResult> ReconcileAsync(PortfolioEntryApproval approval, CancellationToken cancellationToken) =>
        store.ReconcileApprovedAsync(approval, cancellationToken);
}
