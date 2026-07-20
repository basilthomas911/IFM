using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundMaxProfitGenerated
{
    /// <summary>
    /// handle GetFundMaxProfitGeneratedQuery, calculate fund max profit generated based on fund transactions, and reply with FundMaxProfitGeneratedReadModel
    /// </summary>
    /// <param name="q"> The query to handle </param>
    /// <param name="dbFactory"> The database context factory </param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundMaxProfitGeneratedReadModel> GetFundMaxProfitGeneratedAsync(
        this GetFundMaxProfitGeneratedQuery q, IDbContextFactory dbFactory)
    {
        var ordersStartDate = new DateOnly(q.TradeDate.Year, q.TradeDate.Month, 1);
        var ordersEndDate = new DateOnly(q.TradeDate.Year, q.TradeDate.Month, q.TradeDate.Day);
        var yearStart = new DateOnly(q.TradeDate.Year, 1, 1);
        var yearEnd = new DateOnly(q.TradeDate.Year, 12, 31);

        var db = dbFactory.FundDb;
        return new (
            fundId: q.FundId,
            tradeDate: q.TradeDate,
            fundBalance: await db.GetFundBalanceAsync(q.FundId),
            fundProfitOrders: await db.GetFundProfitOrdersAsync(q.FundId, ordersStartDate, ordersEndDate),
            fundLossOrders: await db.GetFundLossOrdersAsync(q.FundId, ordersStartDate, ordersEndDate),
            fundDrawdownBalances: new FundDrawdownBalancesReadModel
            (
                FundId: q.FundId,
                StartBalance: await db.GetFundStartingBalanceAsync(q.FundId, yearStart),
                EndBalance: await db.GetFundEndingBalanceAsync(q.FundId, yearEnd)
            )
        );
    }

}
