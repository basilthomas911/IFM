using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetVerticalSpreadOptionTrades
{
    /// <summary>
    /// Reads the requested page of Vertical Spread trades for one portfolio and fund.
    /// </summary>
    /// <param name="query">The query containing ownership, date-range, and paging criteria.</param>
    /// <param name="context">The typed query context that provides storage and reply operations.</param>
    /// <param name="cancellationToken">The token that cancels the storage operation.</param>
    /// <returns>A task that completes after the typed query reply is sent.</returns>
    public static async ValueTask ExecuteAsync(
        this GetVerticalSpreadOptionTradesQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var page = await context.DbFactory.TradeDb.GetEstablishedTradesAsync(
            query.PortfolioId,
            query.FundId,
            TradeStrategyKind.VerticalSpread,
            query.FromUtc,
            query.ToUtc,
            query.PageSize,
            query.PagingState,
            cancellationToken).ConfigureAwait(false);

        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<EstablishedTradeDefinition[]>(page.Items)).ConfigureAwait(false);
    }
}
