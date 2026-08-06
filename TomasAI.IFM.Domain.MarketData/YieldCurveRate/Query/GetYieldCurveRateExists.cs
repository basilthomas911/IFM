using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateExists
{
    public static async ValueTask<ScalarReadModel<bool>> GetYieldCurveRateExistsAsync(
        this GetYieldCurveRateExistsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => new(cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb
                .GetYieldCurveRateExistsAsync(q.ValueDate, cancellationToken)
                .ConfigureAwait(false)
            : await dbFactory.MarketDataDb
                .GetYieldCurveRateExistsAsync(q.ValueDate)
                .ConfigureAwait(false));
}
