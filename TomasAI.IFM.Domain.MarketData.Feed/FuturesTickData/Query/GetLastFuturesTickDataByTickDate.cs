using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;

public static class GetLastFuturesTickDataByTickDate
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetLastFuturesTickDataByTickDateQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="dbFactory"></param>
        /// <returns></returns>
        public async ValueTask<FuturesTickDataV2ReadModel?> ExecuteAsync(
        IDbContextFactory dbFactory)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return await (dbFactory.MarketDataDb.GetLastFuturesTickDataByTickDateAsync(q.ContractId, q.TickDate)).ConfigureAwait(false);
        }
    }
}
