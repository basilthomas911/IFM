using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

public interface IMarketDataDbReadContext
{
    Task<FuturesDataId?> GetFuturesDataId(string contractId, DateOnly valueDate);
    Task<long> GetNextTickIdAsync(FuturesDataId e);
    Task<FuturesClosingPriceReadModel?> GetYesterdaysFuturesClosingPriceAsync(FuturesDataId id);
    Task<FuturesClosingPriceReadModel?> GetFuturesClosingPriceAsync(FuturesDataId e);
    FuturesClosingPriceReadModel? GetFuturesClosingPrice(FuturesDataId e);
    Task<FuturesTickHLVDataReadModel?> GetFuturesTickHLVDataAsync(FuturesDataId e);
    Task<FuturesTickDataId?> GetLastFuturesTickDataIdAsync(string contractId, DateOnly valueDate);
    Task<FuturesTickDataV2ReadModel?> GetFuturesTickDataAsync(FuturesTickDataId futuresTickDataId);
    Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataIdAsync(string contractId, DateOnly valueDate);
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickDataAsync(FuturesOptionTickDataId futuresTickDataId);
    Task<FuturesOptionTickDataV2ReadModel?> GetFuturesOptionTickPriceDataAsync(FuturesOptionTickDataId futuresTickDataId);
    Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate);
    Task<ICollection<FuturesBarDataReadModel>> GetFuturesBarDataAsync();
    Task<FuturesBarDataReadModel> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate);
    Task<int> GetFuturesBarDataCountAsync(FuturesBarDataId id);
    Task<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ICollection<FuturesIntraDayDataReadModel>> GetFuturesIntraDayDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync();
    Task<ICollection<FuturesEodDataV2ReadModel>> GetFuturesEodDataByDateRangeAsync(string contractId, DateOnly startDate, DateOnly endDate);
    Task<FuturesEodMovingAverageReadModel?> GetFuturesEodMovingAverageAsync(string symbol, DateTime startDate, DateTime endDate);
    Task<ICollection<FuturesEodClosingPriceReadModel>> GetFuturesEodClosingPricesAsync(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays);
    Task<FuturesEodDataV2ReadModel?> GetCurrentFuturesEodDataAsync(DateOnly valueDate);
    Task<ICollection<FuturesEodDataV2ReadModel>> GetCurrentFuturesEodDataByDateRangeAsync(DateOnly startDate, DateOnly endDate);
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(FuturesItiSignalEntityId e);
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalsAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiSignalTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(string contractId, DateOnly valueDate);

    Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateOnly valueDate, IntrinsicTimeTrendType intrinsicTimeTrend, int intrinsicTimeGroupId);
    Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<FuturesItiTrendModelDataStatistics> LoadFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FuturesItiTrendDeltaDataReadModel>> GetFuturesItiTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<FuturesItiTrendDeltaModelReadModel> GetFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate);
    Task<ICollection<FuturesItiTrendClassDataReadModel>> GetFuturesItiTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<FuturesItiTrendClassModelReadModel> GetFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate);
    Task<ICollection<FuturesItiSignalV2ReadModel>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate);
    Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(string contractId, DateOnly valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime);
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalAsync(string contractId, DateOnly valueDate);
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendDirectionChangeAsync(string contractId, DateOnly valueDate);
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendExtremeChangeAsync(string contractId, DateOnly valueDate);
    Task<FuturesItiSignalV2ReadModel?> GetLastFuturesItiSignalTrendReversalChangeAsync(string contractId, DateOnly valueDate);
	Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataByTickDateAsync(string contractId, DateTime tickDate);
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickPriceDataAsync(string contractId, DateOnly valueDate);
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesTdiSignalReadModel?> GetLastFuturesTdiSignalAsync(string contractId, DateOnly valueDate);
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(string contractId, TradeTimePeriodType timePeriod, int periodLength);
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(string contractId, DateOnly valueDate);
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync();
    Task<ICollection<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalsAsync();
    Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(string symbol, DateOnly valueDate);
    Task<RateOfReturnReadModel?> GetLastRateOfReturnAsync(string symbol);
    Task<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
	Task<ICollection<VixFuturesEodDataReadModel>> GetVixFuturesEodDataByValueDateAsync(DateOnly valueDate);
	Task<FuturesTickHLVDataReadModel?> GetVixFuturesTickHLVDataAsync(VixFuturesEodDataEntityId e);

    Task<YieldCurveRateReadModel?> GetLastYieldCurveRateAsync();
    Task<YieldCurveRateReadModel?> GetYieldCurveRateAsync(DateOnly valueDate);
    Task<ICollection<YieldCurveRateReadModel>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate);
    Task<ICollection<int>> GetYieldCurveRateYearsAsync();
    Task<bool> GetYieldCurveRateExistsAsync(DateOnly valueDate);
    Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysAsync(CurrencyType currencyType);
    Task<ICollection<MarketHolidayReadModel>> GetMarketHolidaysByDateRangeAsync(CurrencyType currencyType, DateOnly startDate, DateOnly endDate);
    Task<int> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType);
    Task<DateOnly[]> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType);
    Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel> GetFuturesItiSignalAveragePredictedTrendDeltaRangeAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel> GetFuturesItiSignalAveragePredictedTrendDeltaAsync(string contractId, DateOnly valueDate);

    Task<ICollection<NormalCurveDataReadModel>> GetNormalCurveDataAsync();
    Task<NormalCurveTableReadModel> GetNormalCurveTableAsync();
    Task<int> GetStreamingRequestIdAsync();
    Task<int> GetOptionQuoteIdAsync();
    Task<ICollection<FuturesTradeSignalId>> GetFuturesTradeSignalIdByValueDateAsync(DateOnly valueDate);
    Task<TradeLiveFeedReadModel?> GetTradeLiveFeedAsync(int orderId, int tradeId);






}
