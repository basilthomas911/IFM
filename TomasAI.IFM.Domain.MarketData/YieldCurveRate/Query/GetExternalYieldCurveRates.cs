using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetExternalYieldCurveRates
{
    public static async ValueTask<YieldCurveRateReadModel[]> GetExternalYieldCurveRatesAsync(
        this GetExternalYieldCurveRatesQuery q, IDbContextFactory dbFactory)
        => await dbFactory.GetExternalYieldCurveRatesAsync();

    static async ValueTask<YieldCurveRateReadModel[]> GetExternalYieldCurveRatesAsync(this IDbContextFactory dbFactory)
    {
        if (dbFactory.YieldCurveRatesDb is not IYieldCurveRatesDbContext ycRatesDb)
            return [];
        return [.. await ycRatesDb.ReadAsync()];
    }
}
