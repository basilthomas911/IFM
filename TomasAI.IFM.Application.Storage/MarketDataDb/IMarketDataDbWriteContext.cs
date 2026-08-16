using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public interface IMarketDataDbWriteContext
{
    Task DeleteEconomicCalendarAsync(EconomicCalendarId id);
    Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
    Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars);
    Task InsertEconomicCalendarsAsync(
        ICollection<EconomicCalendarReadModel> economicCalendars,
        ImportDuplicatePolicy duplicatePolicy,
        Guid commandId);
    Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar);
    Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e);
    Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e);
    Task<MarketDataProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
        => throw new NotSupportedException();
    Task<MarketDataProjectionReadiness> GetQueryProjectionReadinessAsync(
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    Task<FmpQueryProjectionBackfillResult> BackfillFmpQueryProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task DeleteFuturesBarDataAsync(FuturesBarDataId e);
    Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task DeleteYieldCurveRateAsync(DateOnly valueDate);
    Task DeleteMarketHolidayAsync(MarketHolidayReadModel e);
    Task DeleteMarketHolidaysAsync(CurrencyType  currencyType);
    Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate);
    Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod);
    Task DeleteFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    Task DeleteFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate);
    Task DeleteFuturesClosingPriceAsync(string contractId, DateOnly valueDate);

    Task InsertFuturesBarDataAsync(FuturesBarDataReadModel e);
    Task InsertFuturesBarDataAsync(ICollection<FuturesBarDataReadModel> futuresBarData);
    Task<long> InsertFuturesBarDataAsync(IEnumerable<FuturesBarDataReadModel> futuresBarData);
    Task InsertFuturesClosingPriceAsync(FuturesClosingPriceReadModel e);
    Task InsertFuturesEodDataIndexAsync(FuturesEodDataIndexReadModel e);
    Task InsertFuturesTickDataAsync(FuturesTickDataV2ReadModel e);
    Task InsertFuturesTickDataAsync(ICollection<FuturesTickDataV2ReadModel> e);
    Task InsertFuturesOptionTickDataAsync(FuturesOptionTickDataV2ReadModel e);
    Task InsertFuturesOptionTickPriceDataAsync(FuturesOptionTickDataV2ReadModel e);
    Task InsertFuturesOptionTickDataAsync(ICollection<FuturesOptionTickDataV2ReadModel> e);
    Task InsertFuturesItiSignalAsync(FuturesItiSignalV2ReadModel e);
    Task InsertFuturesItiTrendClassModelAsync(FuturesItiTrendClassModelReadModel e);
    Task InsertFuturesItiTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel e);
    Task InsertFuturesRsiSignalAsync(FuturesRsiSignalReadModel e);
    Task InsertFuturesTdiSignalAsync(FuturesTdiSignalReadModel e);
    Task InsertFuturesMacdSignalAsync(FuturesMacdSignalReadModel e);
    Task InsertFuturesAtrSignalAsync(FuturesAtrSignalReadModel e);
    Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel e);
    Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, int periodLength);
    Task DeleteTradeLiveFeedAsync(int orderid, int tradeId);
    Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel e);
    Task InsertFuturesTradeSignalsAsync(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals);
    Task<long> InsertFuturesTradeSignalsAsync(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals);
    Task InsertRateOfReturnAsync(RateOfReturnReadModel e);
    Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e);
    Task InsertYieldCurveRatesAsync(ICollection<YieldCurveRateReadModel> e);
    Task InsertYieldCurveRatesAsync(
        ICollection<YieldCurveRateReadModel> e,
        ImportDuplicatePolicy duplicatePolicy,
        Guid commandId);
    Task InsertMarketHolidayAsync(MarketHolidayReadModel e);
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e);

    Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e);
    Task InsertFuturesEodDataAsync(ICollection<FuturesEodDataV2ReadModel> futuresEodData);
    Task<long> InsertFuturesEodDataAsync(IEnumerable<FuturesEodDataV2ReadModel> futuresEodData);
    Task InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel e);
}
