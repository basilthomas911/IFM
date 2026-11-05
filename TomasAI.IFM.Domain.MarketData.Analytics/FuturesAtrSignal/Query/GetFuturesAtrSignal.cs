using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;

public static class GetFuturesAtrSignal
{

    extension(GetFuturesAtrSignalQuery q)
    {
        /// <summary>
        /// Handles a <see cref="GetFuturesAtrSignalQuery"/> by retrieving the most recent ATR signal
        /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
        /// </summary>
        private async ValueTask<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => await dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                    q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false);

        /// <summary>Reads the GetFuturesAtrSignalQuery message.</summary>
        public async ValueTask<ServiceResult<FuturesAtrSignalReadModel?>> ExecuteAsync(
            IQueryActorContext<FuturesAtrSignalQueryActor> ctx,
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var queryResult = await query.GetLastFuturesAtrSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesAtrSignalReadModel?>(queryResult);
            return serviceResult;
        }
    }
}
