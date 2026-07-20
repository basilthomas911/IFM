using MathNet.Numerics.Distributions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundPnlReport
{
    /// <summary>
    /// handle GetFundPnlReportQuery, calculate fund pnl report based on fund transactions, and reply with FundPnlReportReadModel
    /// </summary>
    /// <param name="q"> The query to handle </param>
    /// <param name="dbFactory"> The database context factory </param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundPnlReportReadModel> GetFundPnlReportAsync(
        this GetFundPnlReportQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.FundDb;
        var lossOrders = await db.GetFundLossOrdersAsync(q.FundId, q.StartDate, q.EndDate);
        var lossCount = Convert.ToDouble(lossOrders.Count);
        var profitOrders = await db.GetFundProfitOrdersAsync(q.FundId, q.StartDate, q.EndDate);
        var winCount = Convert.ToDouble(profitOrders.Count);
        var winRate = (winCount + lossCount) > 0 ? winCount / (winCount + lossCount) : 0;
        var lossRate = (winCount + lossCount) > 0 ? lossCount / (winCount + lossCount) : 0;
        var avgTradeLoss = lossOrders.Count > 0 ? lossOrders.Average(e => e.Amount) : 0;
        var avgTradeProfit = profitOrders.Count > 0 ? profitOrders.Average(e => e.Amount) : 0;
        var startingFundBalance = await db.GetFundStartingBalanceAsync(q.FundId, q.StartDate);
        var endingFundBalance = await db.GetFundEndingBalanceAsync(q.FundId, q.EndDate);
        var tradeCommission = await db.GetFundTradeCommissionAsync(q.FundId, q.StartDate, q.EndDate);
        var fundPnlReport = new FundPnlReportReadModel
        (
            WinRate: winRate,
            AverageLoss: avgTradeLoss,
            LossRate: lossRate,
            AverageProfit: avgTradeProfit,
            WinLossRatio: GetWinLossRatio(winRate, Convert.ToDouble(avgTradeProfit), lossRate, Convert.ToDouble(avgTradeLoss)),
            TargetSharpeRatio: await GetSharpeRatioAsync(db, q.FundId, q.StartDate, q.EndDate),
            ActualSharpeRatio: await GetSharpeRatioAsync(db, q.FundId, q.StartDate, q.EndDate),
            PnlAmount: startingFundBalance != 0.0m ? endingFundBalance - startingFundBalance : 0.0m,
            PnlPercent: startingFundBalance != 0.0m ? (double)((endingFundBalance - startingFundBalance) / startingFundBalance) : 0,
            TradeCommission: tradeCommission
        );
        return fundPnlReport;

        /// <summary>
        /// Calculates the win-loss ratio based on win rate, average profit, loss rate, and average loss.
        /// </summary>
        /// <param name="winRate"> The win rate </param>
        /// <param name="avgProfit"> The average profit </param>
        /// <param name="lossRate"> The loss rate </param>
        /// <param name="avgLoss"> The average loss </param>
        /// <returns> The win-loss ratio </returns>
        static double GetWinLossRatio(double winRate, double avgProfit, double lossRate, double avgLoss)
        {
            var winRatio = winRate * avgProfit;
            var lossRatio = lossRate * avgLoss;
            return lossRatio == 0
                ? 0
                : Math.Abs(winRatio / lossRatio);
        }

        /// <summary>
        /// Gets the Sharpe ratio for the fund.
        /// </summary>
        /// <returns> The Sharpe ratio </returns>
        static async Task<double> GetSharpeRatioAsync(IFundDbContext db, int fundId, DateOnly startDate, DateOnly endDate)
        {
            var sharpeRatio = 0.0;
            try
            {
                var fundDailyBalances = await db.GetFundDailyBalancesAsync(fundId, startDate, endDate);
                if (fundDailyBalances.Count > 0)
                {
                    List<double> dailyReturns = [];
                    for (var index = 0; index < fundDailyBalances.Count - 1; index++)
                    {
                        var curBalance = Convert.ToDouble(fundDailyBalances.ElementAt(index).Balance);
                        var prevBalance = Convert.ToDouble(fundDailyBalances.ElementAt(index + 1).Balance);
                        var dailyReturn = (curBalance - prevBalance) / prevBalance;
                        dailyReturns.Add(dailyReturn);
                    }

                    var nd = Normal.Estimate(dailyReturns);
                    sharpeRatio = nd.StdDev > 0.0 ? (nd.Mean / nd.StdDev) * Math.Sqrt(252) : 0.0;
                }
            }
            catch
            {
                sharpeRatio = 0.0;
            }
            return sharpeRatio;
        }
    }

}
