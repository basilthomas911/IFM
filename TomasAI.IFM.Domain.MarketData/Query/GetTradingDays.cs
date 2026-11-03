using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetTradingDays
{

    /// <param name="q">
    /// The query for which to retrieve the number of trading days.
    /// </param>
    extension(GetTradingDaysQuery q)
    {
        /// <summary>
        /// Handles the GetTradingDaysQuery by retrieving the number of trading days between the specified start and end dates for a given market and currency type.
        /// The result is sent back to the query actor context.
        /// </summary>
        ///
        /// <param name="dbFactory">The database context factory.</param>
        /// <param name="context">The query actor context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask<ScalarReadModel<int>> ExecuteAsync(
                IDbContextFactory dbFactory,
                CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            var tradingDayCount = await dbFactory.MarketDataDb.GetTradingDayCountAsync(
                    q.StartDate, q.EndDate, q.MarketType, q.CurrencyType, cancellationToken)
                    .ConfigureAwait(false);
            return new ScalarReadModel<int>(tradingDayCount);
        }
    }

    extension(GetTradingDaysQuery query)
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
                new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
        }
    }
}
