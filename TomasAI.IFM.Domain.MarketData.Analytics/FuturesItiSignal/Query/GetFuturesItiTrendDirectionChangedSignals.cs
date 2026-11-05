using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiTrendDirectionChangedSignals
{

    /// <param name="q">
    /// The query for retrieving trend direction changed signals.
    /// </param>
    extension(GetFuturesItiTrendDirectionChangedSignalsQuery q)
    {
        /// <summary>
        /// Handles the GetFuturesItiTrendDirectionChangedSignalsQuery by retrieving the relevant Futures ITI trend direction changed signals from the database and replying with the results.
        /// </summary>
        ///
        /// <param name="dbFactory">The database context factory.</param>
        /// <param name="context">The query actor context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal async ValueTask<FuturesItiSignalV2ReadModel[]> GetFuturesItiTrendDirectionChangedSignalsAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => [.. await dbFactory.MarketDataDb
                    .GetFuturesItiTrendDirectionChangedSignalsAsync(q.ContractId, q.ValueDate, cancellationToken)
                    .ConfigureAwait(false)];

        /// <summary>Reads the GetFuturesItiTrendDirectionChangedSignalsQuery message.</summary>
        public async ValueTask<ServiceResult<FuturesItiSignalV2ReadModel[]>> ExecuteAsync(
            IQueryActorContext<FuturesItiSignalQueryActor> ctx,
            IDbContextFactory db,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var result = await query.GetFuturesItiTrendDirectionChangedSignalsAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<FuturesItiSignalV2ReadModel[]>(result);
        }
    }
}
