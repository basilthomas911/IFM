using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetLastRateOfReturn
{

    /// <param name="q">
    /// The query for which to retrieve the last rate of return.
    /// </param>
    extension(GetLastRateOfReturnQuery q)
    {
        /// <summary>
        /// Handles the GetLastRateOfReturnQuery by retrieving the last rate of return for a given symbol from the database and replying with the result.
        /// </summary>
        ///
        /// <param name="dbFactory">The database context factory.</param>
        /// <param name="context">The query actor context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal ValueTask<RateOfReturnReadModel> ExecuteAsync(
                IDbContextFactory dbFactory,
                CancellationToken cancellationToken = default)
                => new(dbFactory.MarketDataDb.GetLastRateOfReturnAsync(q.Symbol, cancellationToken));
    }

    extension(GetLastRateOfReturnQuery query)
    {
        /// <summary>Reads and replies with the requested market-data result.</summary>
        public async ValueTask<ServiceResult<RateOfReturnReadModel>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Query.Actor.IMarketDataQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<RateOfReturnReadModel>(result);
        }
    }
}

