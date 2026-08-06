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
        => await FundQueryCalculations.GetMaxProfitGeneratedAsync(
            dbFactory.FundDb,
            q.FundId,
            q.TradeDate).ConfigureAwait(false);

}
