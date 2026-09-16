using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetTradingDates
{
    /// <summary>
    /// Handles the GetTradingDatesQuery by retrieving trading dates from the database and replying with the result.
    /// </summary>
    /// <param name="q">The query for which to retrieve trading dates.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>

	public static ValueTask<DateOnly[]> ExecuteAsync(
        this GetTradingDatesQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? dbFactory.MarketDataDb.GetTradingDatesAsync(
                q.StartDate, q.EndDate, q.MarketType, q.CurrencyType, cancellationToken)
            : dbFactory.MarketDataDb.GetTradingDatesAsync(
                q.StartDate, q.EndDate, q.MarketType, q.CurrencyType));

    /// <summary>Reads and replies with the requested market-data result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradingDatesQuery query,
        TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<DateOnly[]>(result)).ConfigureAwait(false);
    }
}
