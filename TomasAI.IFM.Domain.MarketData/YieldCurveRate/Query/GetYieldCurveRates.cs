using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRates
{
    public static async ValueTask<YieldCurveRateReadModel[]> GetYieldCurveRatesAsync(
        this GetYieldCurveRatesQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => [.. cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb
                .GetYieldCurveRatesAsync(q.StartDate, q.EndDate, cancellationToken)
                .ConfigureAwait(false)
            : await dbFactory.MarketDataDb
                .GetYieldCurveRatesAsync(q.StartDate, q.EndDate)
                .ConfigureAwait(false)];
}
