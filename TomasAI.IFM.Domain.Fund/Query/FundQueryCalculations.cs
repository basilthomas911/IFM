using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

/// <summary>
/// Shared, allocation-conscious calculations used by both actor queries and the direct query API.
/// </summary>
internal static class FundQueryCalculations
{
    internal static async Task<FundPnlReportReadModel> GetPnlReportAsync(
        IFundDbContext db,
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var lossOrdersTask = db.GetFundLossOrdersAsync(fundId, startDate, endDate);
        var profitOrdersTask = db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
        var startingBalanceTask = db.GetFundStartingBalanceAsync(fundId, startDate);
        var endingBalanceTask = db.GetFundEndingBalanceAsync(fundId, endDate);
        var tradeCommissionTask = db.GetFundTradeCommissionAsync(fundId, startDate, endDate);
        var dailyBalancesTask = db.GetFundDailyBalancesAsync(fundId, startDate, endDate);

        await Task.WhenAll(
            lossOrdersTask,
            profitOrdersTask,
            startingBalanceTask,
            endingBalanceTask,
            tradeCommissionTask,
            dailyBalancesTask).ConfigureAwait(false);

        var lossOrders = await lossOrdersTask.ConfigureAwait(false);
        var profitOrders = await profitOrdersTask.ConfigureAwait(false);
        var startingBalance = await startingBalanceTask.ConfigureAwait(false);
        var endingBalance = await endingBalanceTask.ConfigureAwait(false);
        var tradeCommission = await tradeCommissionTask.ConfigureAwait(false);
        var dailyBalances = await dailyBalancesTask.ConfigureAwait(false);

        var lossCount = (double)lossOrders.Count;
        var winCount = (double)profitOrders.Count;
        var totalCount = winCount + lossCount;
        var winRate = totalCount > 0 ? winCount / totalCount : 0;
        var lossRate = totalCount > 0 ? lossCount / totalCount : 0;
        var averageLoss = AverageAmount(lossOrders);
        var averageProfit = AverageAmount(profitOrders);
        var sharpeRatio = CalculateSharpeRatio(dailyBalances);

        return new FundPnlReportReadModel(
            WinRate: winRate,
            AverageLoss: averageLoss,
            LossRate: lossRate,
            AverageProfit: averageProfit,
            WinLossRatio: CalculateWinLossRatio(winRate, (double)averageProfit, lossRate, (double)averageLoss),
            TargetSharpeRatio: sharpeRatio,
            ActualSharpeRatio: sharpeRatio,
            PnlAmount: startingBalance != 0m ? endingBalance - startingBalance : 0m,
            PnlPercent: startingBalance != 0m
                ? (double)((endingBalance - startingBalance) / startingBalance)
                : 0,
            TradeCommission: tradeCommission);
    }

    internal static async Task<FundWinLossRatioReadModel> GetWinLossRatioAsync(
        IFundDbContext db,
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var lossOrdersTask = db.GetFundLossOrdersAsync(fundId, startDate, endDate);
        var profitOrdersTask = db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
        await Task.WhenAll(lossOrdersTask, profitOrdersTask).ConfigureAwait(false);

        var lossOrders = await lossOrdersTask.ConfigureAwait(false);
        var profitOrders = await profitOrdersTask.ConfigureAwait(false);
        var lossCount = (double)lossOrders.Count;
        var winCount = (double)profitOrders.Count;
        var totalCount = winCount + lossCount;
        var winRate = totalCount > 0 ? winCount / totalCount : 0;
        var lossRate = totalCount > 0 ? lossCount / totalCount : 0;
        var averageProfit = (double)AverageAmount(profitOrders);
        var averageLoss = (double)AverageAmount(lossOrders);
        var winLossRatio = CalculateWinLossRatio(winRate, averageProfit, lossRate, averageLoss);
        var kellyDenominator = lossRate * averageProfit;
        var kellyCriteria = kellyDenominator == 0
            ? 0
            : winRate * Math.Abs(averageLoss) / kellyDenominator;

        return new FundWinLossRatioReadModel(winLossRatio, kellyCriteria);
    }

    internal static async Task<FundDrawdownBalancesReadModel> GetDrawdownBalancesAsync(
        IFundDbContext db,
        int fundId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var startingBalanceTask = db.GetFundStartingBalanceAsync(fundId, startDate);
        var endingBalanceTask = db.GetFundEndingBalanceAsync(fundId, endDate);
        await Task.WhenAll(startingBalanceTask, endingBalanceTask).ConfigureAwait(false);

        return new FundDrawdownBalancesReadModel(
            fundId,
            await startingBalanceTask.ConfigureAwait(false),
            await endingBalanceTask.ConfigureAwait(false));
    }

    internal static async Task<FundMaxProfitGeneratedReadModel> GetMaxProfitGeneratedAsync(
        IFundDbContext db,
        int fundId,
        DateOnly tradeDate)
    {
        var ordersStartDate = new DateOnly(tradeDate.Year, tradeDate.Month, 1);
        var yearStart = new DateOnly(tradeDate.Year, 1, 1);
        var yearEnd = new DateOnly(tradeDate.Year, 12, 31);
        var fundBalanceTask = db.GetFundBalanceAsync(fundId);
        var profitOrdersTask = db.GetFundProfitOrdersAsync(fundId, ordersStartDate, tradeDate);
        var lossOrdersTask = db.GetFundLossOrdersAsync(fundId, ordersStartDate, tradeDate);
        var startingBalanceTask = db.GetFundStartingBalanceAsync(fundId, yearStart);
        var endingBalanceTask = db.GetFundEndingBalanceAsync(fundId, yearEnd);

        await Task.WhenAll(
            fundBalanceTask,
            profitOrdersTask,
            lossOrdersTask,
            startingBalanceTask,
            endingBalanceTask).ConfigureAwait(false);

        return new FundMaxProfitGeneratedReadModel(
            fundId,
            tradeDate,
            await fundBalanceTask.ConfigureAwait(false),
            await profitOrdersTask.ConfigureAwait(false),
            await lossOrdersTask.ConfigureAwait(false),
            new FundDrawdownBalancesReadModel(
                fundId,
                await startingBalanceTask.ConfigureAwait(false),
                await endingBalanceTask.ConfigureAwait(false)));
    }

    internal static double CalculateSharpeRatio(ICollection<FundDailyBalanceReadModel> balances)
    {
        if (balances.Count < 3)
            return 0;

        var returnCount = 0;
        var sum = 0d;
        var sumOfSquares = 0d;

        if (balances is IList<FundDailyBalanceReadModel> indexedBalances)
        {
            for (var index = 0; index < indexedBalances.Count - 1; index++)
            {
                if (!AccumulateReturn(
                    indexedBalances[index].Balance,
                    indexedBalances[index + 1].Balance,
                    ref returnCount,
                    ref sum,
                    ref sumOfSquares))
                    return 0;
            }
        }
        else
        {
            using var enumerator = balances.GetEnumerator();
            if (!enumerator.MoveNext())
                return 0;

            var currentBalance = enumerator.Current.Balance;
            while (enumerator.MoveNext())
            {
                var previousBalance = enumerator.Current.Balance;
                if (!AccumulateReturn(
                    currentBalance,
                    previousBalance,
                    ref returnCount,
                    ref sum,
                    ref sumOfSquares))
                    return 0;
                currentBalance = previousBalance;
            }
        }

        if (returnCount < 2)
            return 0;

        var mean = sum / returnCount;
        var variance = (sumOfSquares - sum * mean) / (returnCount - 1);
        var standardDeviation = variance > 0 ? Math.Sqrt(variance) : 0;
        return standardDeviation > 0 && double.IsFinite(standardDeviation)
            ? mean / standardDeviation * Math.Sqrt(252)
            : 0;
    }

    static bool AccumulateReturn(
        decimal currentBalance,
        decimal previousBalance,
        ref int returnCount,
        ref double sum,
        ref double sumOfSquares)
    {
        if (previousBalance == 0m)
            return false;

        var current = (double)currentBalance;
        var previous = (double)previousBalance;
        var dailyReturn = (current - previous) / previous;
        if (!double.IsFinite(dailyReturn))
            return false;

        returnCount++;
        sum += dailyReturn;
        sumOfSquares += dailyReturn * dailyReturn;
        return true;
    }

    static decimal AverageAmount(ICollection<FundOrderAmountReadModel> orders)
    {
        if (orders.Count == 0)
            return 0m;

        var total = 0m;
        foreach (var order in orders)
            total += order.Amount;
        return total / orders.Count;
    }

    static double CalculateWinLossRatio(double winRate, double averageProfit, double lossRate, double averageLoss)
    {
        var lossRatio = lossRate * averageLoss;
        return lossRatio == 0 ? 0 : Math.Abs(winRate * averageProfit / lossRatio);
    }
}
