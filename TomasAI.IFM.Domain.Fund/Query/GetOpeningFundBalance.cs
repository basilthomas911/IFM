using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetOpeningFundBalance
{
    /// <summary>
    /// handle GetOpeningFundBalanceQuery, return the opening fund balance for the given fund and value date. 
    /// The opening fund balance is the balance of the first transaction of the day with open trade status.
    /// If there is no transaction with open trade status, return 0.
    /// </summary>
    /// <param name="q"> The query to handle </param>
    /// <param name="dbFactory"> The database context factory </param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundBalanceReadModel> GetOpeningFundBalanceAsync(
        this GetOpeningFundBalanceQuery q, IDbContextFactory dbFactory)
        => new(await dbFactory.FundDb.GetOpeningFundBalanceAsync( q.FundId, q.ValueDate));
    
}
