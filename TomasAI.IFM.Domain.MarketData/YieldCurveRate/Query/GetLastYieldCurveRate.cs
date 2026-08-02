using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

internal static class GetLastYieldCurveRate
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync(
        this GetLastYieldCurveRateQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastYieldCurveRateAsync();
}
