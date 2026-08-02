using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query;

internal static class GetFundTransactions
{
    /// <summary>
    /// Get fund transactions for a given fund and date range.
    /// </summary>
    /// <param name="q">The query for retrieving fund transactions.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundTransactionReadModel[]> GetFundTransactionsAsync(
        this GetFundTransactionsQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.FundDb;
        return [.. await db.GetFundTransactionsAsync(q.FundId, q.StartDate, q.EndDate)];
    }
}
