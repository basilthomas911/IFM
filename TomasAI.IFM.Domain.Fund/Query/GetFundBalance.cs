using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Query;

public static class GetFundBalance
{
    /// <summary>
    /// Handles the GetFundBalanceQuery by querying the fund balance from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns></returns>
    internal static async ValueTask<decimal> GetFundBalanceAsync(this GetFundBalanceQuery q, IDbContextFactory dbFactory)
        => await dbFactory.FundDb.GetFundBalanceAsync(q.FundId);
}
