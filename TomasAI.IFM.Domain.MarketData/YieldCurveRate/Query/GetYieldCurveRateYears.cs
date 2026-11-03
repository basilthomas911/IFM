using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateYears
{

    extension(GetYieldCurveRateYearsQuery q)
    {
        public async ValueTask<YieldCurveRateYearsReadModel> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            return new([.. await dbFactory.MarketDataDb
                    .GetYieldCurveRateYearsAsync(cancellationToken)
                    .ConfigureAwait(false)]);
        }
    }

}
