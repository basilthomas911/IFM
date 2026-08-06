using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundWinLossRatio
{
    /// <summary>
    /// Calculate fund win/loss ratio and Kelly criteria based on the fund orders with profit and loss amounts within the specified date range, then reply with the calculation result.
    /// </summary>
    /// <param name="q">The query for retrieving fund win/loss ratio</param>
    /// <param name="dbFactory">The database context factory</param>
    /// <returns></returns>
    internal static async ValueTask<FundWinLossRatioReadModel> GetFundWinLossRatioAsync(
        this GetFundWinLossRatioQuery q, IDbContextFactory dbFactory)
        => await FundQueryCalculations.GetWinLossRatioAsync(
            dbFactory.FundDb,
            q.FundId,
            q.StartDate,
            q.EndDate).ConfigureAwait(false);
}
