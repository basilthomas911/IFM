using TomasAI.IFM.Domain.Fund.Query.Actor;
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
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns> A task representing the asynchronous operation </returns>
    internal static async ValueTask<FundBalanceReadModel> GetOpeningFundBalanceAsync(
        this GetOpeningFundBalanceQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? await context.DbFactory.FundDb.GetOpeningFundBalanceAsync(q.FundId, q.ValueDate, cancellationToken).ConfigureAwait(false)
            : await context.DbFactory.FundDb.GetOpeningFundBalanceAsync(q.FundId, q.ValueDate).ConfigureAwait(false));
    
}
