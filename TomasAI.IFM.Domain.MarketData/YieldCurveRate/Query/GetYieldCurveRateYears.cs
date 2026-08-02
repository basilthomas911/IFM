using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateYears
{
    public static async ValueTask<YieldCurveRateYearsReadModel> GetYieldCurveRateYearsAsync(
        this GetYieldCurveRateYearsQuery q, IDbContextFactory dbFactory)
        => new([.. await dbFactory.MarketDataDb.GetYieldCurveRateYearsAsync()]);

}
