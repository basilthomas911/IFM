using TomasAI.IFM.Application.Storage;
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
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(
        this GetFundDrawdownBalancesQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.FundDb;
        var startingBalance = await db.GetFundStartingBalanceAsync(q.FundId, q.StartDate);
        var endingBalance = await db.GetFundEndingBalanceAsync(q.FundId, q.EndDate);
        return new FundDrawdownBalancesReadModel(q.FundId, startingBalance, endingBalance);
    }
  
}
