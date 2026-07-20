using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

public interface IMarketDataDbWriteContext
{
    Task CreateFuturesTradeSignalTableAsync();
    Task DeleteFuturesBarDataAsync(FuturesBarDataId e);
    Task DeleteFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task DeleteFuturesTickDataAsync(string contractId, DateOnly valueDate);
    Task DeleteVixFuturesEodDataAsync(string contractId, DateOnly valueDate);
    Task DeleteYieldCurveRateAsync(DateOnly valueDate);
    Task DeleteMarketHolidayAsync(MarketHolidayReadModel e);
    Task DeleteMarketHolidaysAsync(CurrencyType  currencyType);
    Task DeleteFuturesOptionQuotesAsync(int QuoteId);
    Task DeleteRateOfReturnAsync(string symbol, DateOnly valueDate);
    Task DeleteFuturesItiSignalAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod);
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
    Task DeleteFuturesAtrSignalAsync(string contractId, DateOnly valueDate);
    Task InsertFuturesAdxSignalAsync(FuturesAdxSignalReadModel e);
    Task DeleteFuturesAdxSignalAsync(string contractId, DateOnly valueDate);
    Task DeleteTradeLiveFeedAsync(int orderid, int tradeId);
    Task InsertFuturesTradeSignalAsync(FuturesTradeSignalV2ReadModel e);
    Task InsertFuturesTradeSignalsAsync(ICollection<FuturesTradeSignalV2ReadModel> futuresTradeSignals);
    Task<long> InsertFuturesTradeSignalsAsync(IEnumerable<FuturesTradeSignalV2ReadModel> futuresTradeSignals);
    Task InsertRateOfReturnAsync(RateOfReturnReadModel e);
    Task InsertYieldCurveRateAsync(YieldCurveRateReadModel e);
    Task InsertYieldCurveRatesAsync(ICollection<YieldCurveRateReadModel> e);
    Task InsertMarketHolidayAsync(MarketHolidayReadModel e);
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel e);

    Task InsertFuturesOptionQuoteAsync(ICollection<FuturesOptionQuoteReadModel> futuresOptionQuotes, ICollection<FuturesOptionQuoteDataReadModel> futuresOptionQuoteData);
    Task InsertFuturesOptionQuoteDataAsync(FuturesOptionQuoteDataReadModel e);

    Task InsertFuturesEodDataAsync(FuturesEodDataV2ReadModel e);
    Task InsertFuturesEodDataAsync(ICollection<FuturesEodDataV2ReadModel> futuresEodData);
    Task<long> InsertFuturesEodDataAsync(IEnumerable<FuturesEodDataV2ReadModel> futuresEodData);
    Task InsertVixFuturesEodDataAsync(FuturesTickDataV2ReadModel e);
}
