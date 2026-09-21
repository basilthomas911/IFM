using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;

/// <summary>Handles one bounded prior-day Bollinger history query.</summary>
public static class GetFuturesBollingerBandHistory
{
    const int HistoricalLookbackCalendarDays = 365;
    const int MaximumChartHistoryDays = 40;

    /// <summary>
    /// Calculates prior-day Bollinger values from normalized Databento EOD observations and returns
    /// them in ascending value-date order. The current value-date point comes from Market Outlook.
    /// </summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesBollingerBandHistoryQuery query,
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
        IMarketOutlookSnapshotQueryContext domainContext,
        CancellationToken cancellationToken)
    {
        ServiceResult<FuturesBbSignalReadModel[]> result;
        if (string.IsNullOrWhiteSpace(query.RootSymbol) || query.ValueDate == default
            || query.MaxDays is <= 0 or > MaximumChartHistoryDays)
        {
            result = new ServiceFailed<FuturesBbSignalReadModel[]>(
                GetFuturesBollingerBandHistoryQuery.ErrorId,
                "A root symbol, value date, and maximum prior-day count from 1 through 40 are required.");
        }
        else
        {
            var identity = MarketSeriesIdentity.ForFuturesSeries(
                new FuturesSeriesId(query.RootSymbol, "calendar-front", "unadjusted", 1));
            var historyEndDate = query.ValueDate.AddDays(-1);
            var historyStartDate = historyEndDate.AddDays(-(HistoricalLookbackCalendarDays - 1));
            var observations = await domainContext.HistoricalObservationStore
                .GetRawEodRangeAsync(
                    identity,
                    historyStartDate,
                    historyEndDate,
                    cancellationToken)
                .ConfigureAwait(false);
            var values = FuturesBollingerBandHistoryCalculator.Calculate(
                observations,
                query.ValueDate,
                query.MaxDays);
            result = new ServiceOk<FuturesBbSignalReadModel[]>(values);
        }

        await context.ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBollingerBandHistoryQuery.Verb,
            result).ConfigureAwait(false);
    }
}
