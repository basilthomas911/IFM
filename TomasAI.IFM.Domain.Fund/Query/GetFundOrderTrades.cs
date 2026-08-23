using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Query;

internal static class GetFundOrderTrades
{
    /// <summary>
    /// Handles the GetFundOrderTradesQuery by retrieving fund order trade data from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query to handle.</param>
    /// <param name="context">The Fund-specific query context.</param>
    /// <param name="cancellationToken">The token used to cancel the query.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FundOrderTradeReadModel[]> GetFundOrderTradesAsync(
        this GetFundOrderTradesQuery q,
        IFundQueryContext context,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? [.. await context.DbFactory.FundDb.GetFundOrderTradesAsync(cancellationToken).ConfigureAwait(false)]
            : [.. await context.DbFactory.FundDb.GetFundOrderTradesAsync().ConfigureAwait(false)];
    
}
