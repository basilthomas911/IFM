using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundIdFromOrderId
{
    /// <summary>
    /// Handles the GetFundIdFromOrderIdQuery by querying the fund_order table in the FundDb to retrieve the associated FundId for the given OrderId. 
    /// The result is then sent back as a reply to the query actor context.
    /// </summary>
    /// <param name="q">The query for retrieving the fund ID.</param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<int> GetFundIdFromOrderIdAsync(
        this GetFundIdFromOrderIdQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await context.DbFactory.FundDb.GetFundIdFromOrderIdAsync(q.OrderId, cancellationToken).ConfigureAwait(false)
            : await context.DbFactory.FundDb.GetFundIdFromOrderIdAsync(q.OrderId).ConfigureAwait(false);

}
