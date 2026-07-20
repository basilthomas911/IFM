using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Query;

public static class GetClosingFundBalance
{
    /// <summary>
    /// Handles the GetClosingFundBalanceQuery by retrieving the closing fund balance for a specific fund and value date from the database, and then replies with the result encapsulated in a ServiceResult object.
    /// </summary>
    /// <param name="q"> The query for retrieving the closing fund balance. </param>
    /// <param name="dbFactory"> The factory for creating database contexts. </param>
    /// <returns> A task representing the asynchronous operation. </returns>
    internal static async ValueTask<decimal> GetClosingFundBalanceAsync(this GetClosingFundBalanceQuery q, IDbContextFactory dbFactory)
        => await dbFactory.FundDb.GetClosingFundBalanceAsync(q.FundId, q.ValueDate);
}
