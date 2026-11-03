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

public static class GetFuturesItiSignal
{

    extension(GetFuturesItiSignalQuery q)
    {
        /// <summary>
        /// Handles <see cref="GetFuturesItiSignalQuery"/> by retrieving the latest ITI signal snapshot for the
        /// requested contract and value date, then replying to the caller.
        /// </summary>
        internal async ValueTask<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => await dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(
                    q.ContractId, q.ValueDate, q.TimePeriod, cancellationToken).ConfigureAwait(false);

        /// <summary>Reads and replies to the GetFuturesItiSignalQuery message.</summary>
        public async ValueTask ExecuteAsync(
            IQueryActorContext<FuturesItiSignalQueryActor> ctx,
            IDbContextFactory db,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ctx);
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(q);
            var query = q;
            var result = await query.GetLastFuturesItiSignalAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalQuery.Verb,
                new ServiceResult<FuturesItiSignalV2ReadModel?>(result)).ConfigureAwait(false);
        }
    }
}
