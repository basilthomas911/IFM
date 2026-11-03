using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRates
{

    extension(GetYieldCurveRatesQuery q)
    {
        public async ValueTask<YieldCurveRateReadModel[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return [.. await dbFactory.MarketDataDb
                    .GetYieldCurveRatesAsync(q.StartDate, q.EndDate, cancellationToken)
                    .ConfigureAwait(false)];
        }
    }
}
