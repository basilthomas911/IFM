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
    {
        var db = dbFactory.FundDb;
        var lossOrders = await db.GetFundLossOrdersAsync(q.FundId, q.StartDate, q.EndDate);
        var lossCount = Convert.ToDouble(lossOrders.Count);
        var profitOrders = await db.GetFundProfitOrdersAsync(q.FundId, q.StartDate, q.EndDate);
        var winCount = Convert.ToDouble(profitOrders.Count);
        var winRate = (winCount + lossCount) > 0 ? winCount / (winCount + lossCount) : 0;
        var lossRate = (winCount + lossCount) > 0 ? lossCount / (winCount + lossCount) : 0;
        var avgTradeProfit = Convert.ToDouble(profitOrders.Count > 0 ? profitOrders.Average(e => e.Amount) : 0);
        var avgTradeLoss = Convert.ToDouble(lossOrders.Count > 0 ? lossOrders.Average(e => e.Amount) : 0);
        var winRatio = winRate * avgTradeProfit;
        var lossRatio = lossRate * avgTradeLoss;
        var winLossRatio = lossRatio == 0 ? 0 : Math.Abs(winRatio / lossRatio);
        var kellyCriteria = (lossRate * avgTradeProfit) == 0 ? 0 : winRate * Math.Abs(avgTradeLoss) / (lossRate * avgTradeProfit);
        return new (winLossRatio, kellyCriteria);
    }
}
