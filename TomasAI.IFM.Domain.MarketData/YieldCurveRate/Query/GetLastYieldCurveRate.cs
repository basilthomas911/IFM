using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

internal static class GetLastYieldCurveRate
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetLastYieldCurveRateQuery q)
    {
        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="dbFactory"></param>
        /// <returns></returns>
        public ValueTask<YieldCurveRateReadModel?> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
            => new(dbFactory.MarketDataDb.GetLastYieldCurveRateAsync(cancellationToken));
    }
}
