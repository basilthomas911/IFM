using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Query;

/// <summary>Handles one historical data-load diagnostics query.</summary>
public static class GetFuturesAnalyticsHistoricalDataLoader
{

    extension(GetFuturesAnalyticsHistoricalDataLoaderQuery query)
    {
        /// <summary>Returns durable attempt diagnostics.</summary>
        public async ValueTask<ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>> ExecuteAsync(
            IQueryActorContext<FuturesAnalyticsHistoricalDataLoaderQueryActor> context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await context.DataLoaderContext.DbFactory.MarketDataDb.GetHistoricalAnalyticsAttemptProjectionAsync(
                query.DataLoadAttemptId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceResult<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?>(result);
        }
    }
}
