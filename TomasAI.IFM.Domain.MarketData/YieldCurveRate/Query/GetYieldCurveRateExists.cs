using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateExists
{
    public static async ValueTask<ScalarReadModel<bool>> GetYieldCurveRateExistsAsync(
        this GetYieldCurveRateExistsQuery q, IDbContextFactory dbFactory)
        => new(await dbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(q.ValueDate));
}
