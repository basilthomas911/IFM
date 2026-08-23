using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundDrawdownBalances
{
    /// <summary>
    /// Handles the GetFundDrawdownBalancesQuery by retrieving the starting and ending balances for a specified fund and date range, 
    /// then replies with the results encapsulated in a ServiceResult.
    /// </summary>
    /// <param name="q">The query.</param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(
        this GetFundDrawdownBalancesQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => await FundQueryCalculations.GetDrawdownBalancesAsync(
            context.DbFactory.FundDb,
            q.FundId,
            q.StartDate,
            q.EndDate,
            cancellationToken).ConfigureAwait(false);
  
}
