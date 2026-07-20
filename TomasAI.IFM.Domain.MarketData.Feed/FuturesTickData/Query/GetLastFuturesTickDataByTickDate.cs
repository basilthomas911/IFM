using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;

public static class GetLastFuturesTickDataByTickDate
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetLastFuturesTickDataByTickDateAsync(
        this GetLastFuturesTickDataByTickDateQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesTickDataByTickDateAsync(q.ContractId, q.TickDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTickDataByTickDateQuery.Verb, new ServiceResult<FuturesTickDataV2ReadModel?>(result));
    }
}
