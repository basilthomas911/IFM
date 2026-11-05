using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetFuturesTradeSignalIds
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetFuturesTradeSignalIdsQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="dbFactory"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        internal async ValueTask<FuturesTradeSignalId[]> GetFuturesTradeSignalIdsAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => [.. await dbFactory.MarketDataDb
                    .GetFuturesTradeSignalIdByValueDateAsync(q.ValueDate, cancellationToken).ConfigureAwait(false)];

        /// <summary>Reads the GetFuturesTradeSignalIdsQuery message.</summary>
        public async ValueTask<ServiceResult<FuturesTradeSignalId[]>> ExecuteAsync(
            IQueryActorContext<FuturesTradeSignalQueryActor> ctx,
            IDbContextFactory db,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var result = await query.GetFuturesTradeSignalIdsAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<FuturesTradeSignalId[]>(result);
        }
    }
}
