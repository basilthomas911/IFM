using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetTradingDates
{

    /// <param name="q">
    /// The query for which to retrieve trading dates.
    /// </param>
    extension(GetTradingDatesQuery q)
    {
        /// <summary>
        /// Handles the GetTradingDatesQuery by retrieving trading dates from the database and replying with the result.
        /// </summary>
        ///
        /// <param name="dbFactory">The database context factory.</param>
        /// <param name="context">The query actor context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>

        public ValueTask<DateOnly[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return new(dbFactory.MarketDataDb.GetTradingDatesAsync(
                    q.StartDate, q.EndDate, q.MarketType, q.CurrencyType, cancellationToken));
        }
    }

    extension(GetTradingDatesQuery query)
    {
        /// <summary>Reads and replies with the requested market-data result.</summary>
        public async ValueTask ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
                new ServiceResult<DateOnly[]>(result)).ConfigureAwait(false);
        }
    }
}
