using MathNet.Numerics.Distributions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Query.Api;

/// <summary>
/// Provides direct, in-process access to Fund domain queries without actor messaging.
/// </summary>
/// <remarks>
/// The implementation uses <see cref="IDbContextFactory"/> for Fund storage access. Every public query
/// returns a typed <see cref="ServiceOk{T}"/> or a query-specific <see cref="ServiceFailed{T}"/>.
/// Instances are safe to register as application singletons because they do not capture actor context.
/// </remarks>
public sealed class ActorFundQueryApi(IDbContextFactory dbFactory) : IActorFundQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets funds.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
    {
        try
        {
            FundReadModel[] result = [.. await _dbFactory.FundDb.GetFundsAsync()];
            return new ServiceOk<FundReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundReadModel[]>(GetFundsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund orders.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
    {
        try
        {
            FundOrderReadModel[] result = [.. await _dbFactory.FundDb.GetFundOrdersAsync()];
            return new ServiceOk<FundOrderReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundOrderReadModel[]>(GetFundOrdersQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund order trades.
    /// </summary>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
    {
        try
        {
            FundOrderTradeReadModel[] result = [.. await _dbFactory.FundDb.GetFundOrderTradesAsync()];
            return new ServiceOk<FundOrderTradeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundOrderTradeReadModel[]>(GetFundOrderTradesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund transactions.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            FundTransactionReadModel[] result =
                [.. await _dbFactory.FundDb.GetFundTransactionsAsync(fundId, startDate, endDate)];
            return new ServiceOk<FundTransactionReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundTransactionReadModel[]>(GetFundTransactionsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetFundBalanceAsync(fundId);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets opening fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(
        int fundId,
        DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetOpeningFundBalanceAsync(fundId, valueDate);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetOpeningFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets closing fund balance.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(
        int fundId,
        DateOnly valueDate)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetClosingFundBalanceAsync(fundId, valueDate);
            return new ServiceOk<FundBalanceReadModel>(new FundBalanceReadModel(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundBalanceReadModel>(GetClosingFundBalanceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund P&L report.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var db = _dbFactory.FundDb;
            var lossOrders = await db.GetFundLossOrdersAsync(fundId, startDate, endDate);
            var lossCount = Convert.ToDouble(lossOrders.Count);
            var profitOrders = await db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
            var winCount = Convert.ToDouble(profitOrders.Count);
            var winRate = winCount + lossCount > 0 ? winCount / (winCount + lossCount) : 0;
            var lossRate = winCount + lossCount > 0 ? lossCount / (winCount + lossCount) : 0;
            var averageLoss = lossOrders.Count > 0 ? lossOrders.Average(order => order.Amount) : 0;
            var averageProfit = profitOrders.Count > 0 ? profitOrders.Average(order => order.Amount) : 0;
            var startingBalance = await db.GetFundStartingBalanceAsync(fundId, startDate);
            var endingBalance = await db.GetFundEndingBalanceAsync(fundId, endDate);
            var tradeCommission = await db.GetFundTradeCommissionAsync(fundId, startDate, endDate);

            var result = new FundPnlReportReadModel(
                WinRate: winRate,
                AverageLoss: averageLoss,
                LossRate: lossRate,
                AverageProfit: averageProfit,
                WinLossRatio: CalculateWinLossRatio(
                    winRate,
                    Convert.ToDouble(averageProfit),
                    lossRate,
                    Convert.ToDouble(averageLoss)),
                TargetSharpeRatio: await GetSharpeRatioAsync(db, fundId, startDate, endDate),
                ActualSharpeRatio: await GetSharpeRatioAsync(db, fundId, startDate, endDate),
                PnlAmount: startingBalance != 0.0m ? endingBalance - startingBalance : 0.0m,
                PnlPercent: startingBalance != 0.0m
                    ? (double)((endingBalance - startingBalance) / startingBalance)
                    : 0,
                TradeCommission: tradeCommission);

            return new ServiceOk<FundPnlReportReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundPnlReportReadModel>(GetFundPnlReportQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund ID from order ID.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
    {
        try
        {
            var result = await _dbFactory.FundDb.GetFundIdFromOrderIdAsync(orderId);
            return new ServiceOk<ScalarReadModel<int>>(new ScalarReadModel<int>(result));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetFundIdFromOrderIdQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund win loss ratio.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var db = _dbFactory.FundDb;
            var lossOrders = await db.GetFundLossOrdersAsync(fundId, startDate, endDate);
            var lossCount = Convert.ToDouble(lossOrders.Count);
            var profitOrders = await db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
            var winCount = Convert.ToDouble(profitOrders.Count);
            var winRate = winCount + lossCount > 0 ? winCount / (winCount + lossCount) : 0;
            var lossRate = winCount + lossCount > 0 ? lossCount / (winCount + lossCount) : 0;
            var averageProfit = Convert.ToDouble(
                profitOrders.Count > 0 ? profitOrders.Average(order => order.Amount) : 0);
            var averageLoss = Convert.ToDouble(
                lossOrders.Count > 0 ? lossOrders.Average(order => order.Amount) : 0);
            var winRatio = winRate * averageProfit;
            var lossRatio = lossRate * averageLoss;
            var winLossRatio = lossRatio == 0 ? 0 : Math.Abs(winRatio / lossRatio);
            var kellyCriteria = lossRate * averageProfit == 0
                ? 0
                : winRate * Math.Abs(averageLoss) / (lossRate * averageProfit);

            return new ServiceOk<FundWinLossRatioReadModel>(
                new FundWinLossRatioReadModel(winLossRatio, kellyCriteria));
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundWinLossRatioReadModel>(GetFundWinLossRatioQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets fund drawdown balances.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var startingBalance = await _dbFactory.FundDb.GetFundStartingBalanceAsync(fundId, startDate);
            var endingBalance = await _dbFactory.FundDb.GetFundEndingBalanceAsync(fundId, endDate);
            var result = new FundDrawdownBalancesReadModel(fundId, startingBalance, endingBalance);
            return new ServiceOk<FundDrawdownBalancesReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundDrawdownBalancesReadModel>(
                GetFundDrawdownBalancesQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets fund max profit generated.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public async Task<ServiceResult<FundMaxProfitGeneratedReadModel>> GetFundMaxProfitGeneratedAsync(
        int fundId,
        DateOnly tradeDate)
    {
        try
        {
            var ordersStartDate = new DateOnly(tradeDate.Year, tradeDate.Month, 1);
            var yearStart = new DateOnly(tradeDate.Year, 1, 1);
            var yearEnd = new DateOnly(tradeDate.Year, 12, 31);
            var db = _dbFactory.FundDb;
            var result = new FundMaxProfitGeneratedReadModel(
                fundId: fundId,
                tradeDate: tradeDate,
                fundBalance: await db.GetFundBalanceAsync(fundId),
                fundProfitOrders: await db.GetFundProfitOrdersAsync(fundId, ordersStartDate, tradeDate),
                fundLossOrders: await db.GetFundLossOrdersAsync(fundId, ordersStartDate, tradeDate),
                fundDrawdownBalances: new FundDrawdownBalancesReadModel(
                    FundId: fundId,
                    StartBalance: await db.GetFundStartingBalanceAsync(fundId, yearStart),
                    EndBalance: await db.GetFundEndingBalanceAsync(fundId, yearEnd)));
            return new ServiceOk<FundMaxProfitGeneratedReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<FundMaxProfitGeneratedReadModel>(
                GetFundMaxProfitGeneratedQuery.ErrorId,
                ex.Message);
        }
    }

    static double CalculateWinLossRatio(double winRate, double averageProfit, double lossRate, double averageLoss)
    {
        var winRatio = winRate * averageProfit;
        var lossRatio = lossRate * averageLoss;
        return lossRatio == 0 ? 0 : Math.Abs(winRatio / lossRatio);
    }

    static async Task<double> GetSharpeRatioAsync(
        IFundDbContext db,
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        try
        {
            var balances = await db.GetFundDailyBalancesAsync(fundId, startDate, endDate);
            if (balances.Count == 0)
                return 0.0;

            List<double> dailyReturns = [];
            for (var index = 0; index < balances.Count - 1; index++)
            {
                var currentBalance = Convert.ToDouble(balances.ElementAt(index).Balance);
                var previousBalance = Convert.ToDouble(balances.ElementAt(index + 1).Balance);
                dailyReturns.Add((currentBalance - previousBalance) / previousBalance);
            }

            var distribution = Normal.Estimate(dailyReturns);
            return distribution.StdDev > 0.0
                ? distribution.Mean / distribution.StdDev * Math.Sqrt(252)
                : 0.0;
        }
        catch
        {
            return 0.0;
        }
    }
}
