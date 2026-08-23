using TomasAI.IFM.Domain.Fund.Query.Actor;
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
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns></returns>
    internal static async ValueTask<decimal> GetFundBalanceAsync(
        this GetFundBalanceQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await context.DbFactory.FundDb.GetFundBalanceAsync(q.FundId, cancellationToken).ConfigureAwait(false)
            : await context.DbFactory.FundDb.GetFundBalanceAsync(q.FundId).ConfigureAwait(false);
}
