using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFunds
{
    /// <summary>
    /// Handles the GetFundsQuery by retrieving all funds from the database and replying with the results.
    /// </summary>
    /// <param name="q">The GetFundsQuery instance.</param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundReadModel[]> GetFundsAsync(
        this GetFundsQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? [.. await context.DbFactory.FundDb.GetFundsAsync(cancellationToken).ConfigureAwait(false)]
            : [.. await context.DbFactory.FundDb.GetFundsAsync().ConfigureAwait(false)];
}
