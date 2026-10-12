using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateExists
{
    public static async ValueTask<ScalarReadModel<bool>> ExecuteAsync(
        this GetYieldCurveRateExistsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(q);
        return new(await dbFactory.MarketDataDb
                .GetYieldCurveRateExistsAsync(q.ValueDate, cancellationToken)
                .ConfigureAwait(false));
    }
}
