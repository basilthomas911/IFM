using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetLastFuturesTradeSignal
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetLastFuturesTradeSignalQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="dbFactory"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        internal async ValueTask<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(cancellationToken).ConfigureAwait(false);

        /// <summary>Reads the GetLastFuturesTradeSignalQuery message.</summary>
        public async ValueTask<ServiceResult<FuturesTradeSignalV2ReadModel?>> ExecuteAsync(
            IQueryActorContext<FuturesTradeSignalQueryActor> ctx,
            IDbContextFactory db,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var result = await query.GetLastFuturesTradeSignalAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<FuturesTradeSignalV2ReadModel?>(result);
        }
    }
}
