using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetExternalYieldCurveRates
{
    public static async ValueTask GetExternalYieldCurveRatesAsync(this GetExternalYieldCurveRatesQuery q,IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var externalRates = await dbFactory.GetExternalYieldCurveRatesAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetExternalYieldCurveRatesQuery.Verb, new ServiceResult<YieldCurveRateReadModel[]>(externalRates));
    }

    static async ValueTask<YieldCurveRateReadModel[]> GetExternalYieldCurveRatesAsync(this IDbContextFactory dbFactory)
    {
        if (dbFactory.YieldCurveRatesDb is not IYieldCurveRatesDbContext ycRatesDb)
            return [];
        return [.. await ycRatesDb.ReadAsync()];
    }
}
