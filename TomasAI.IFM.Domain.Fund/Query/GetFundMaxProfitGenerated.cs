using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundMaxProfitGenerated
{
    /// <summary>
    /// handle GetFundMaxProfitGeneratedQuery, calculate fund max profit generated based on fund transactions, and reply with FundMaxProfitGeneratedReadModel
    /// </summary>
    /// <param name="q"> The query to handle </param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundMaxProfitGeneratedReadModel> GetFundMaxProfitGeneratedAsync(
        this GetFundMaxProfitGeneratedQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => await FundQueryCalculations.GetMaxProfitGeneratedAsync(
            context.DbFactory.FundDb,
            q.FundId,
            q.TradeDate,
            cancellationToken).ConfigureAwait(false);

}
