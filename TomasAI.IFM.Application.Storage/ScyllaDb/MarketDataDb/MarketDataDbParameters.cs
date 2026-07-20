using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

internal readonly record struct DeleteFuturesAdxSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteFuturesAtrSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, valueDate };
}
internal readonly record struct DeleteFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timePeriod };
}
internal readonly record struct DeleteFuturesItiTrendClassData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { symbol, startDate, endDate };
}
internal readonly record struct DeleteFuturesItiTrendDeltaData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { symbol, startDate, endDate };
}
internal readonly record struct DeleteFuturesOptionQuoteData(int quoteId) : IBindValue
{
    public object Bind() => new { quoteId };
}
internal readonly record struct DeleteFuturesOptionQuotes(int quoteId) : IBindValue
{
    public object Bind() => new { quoteId };
}
internal readonly record struct DeleteFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}

internal readonly record struct DeleteFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}

internal readonly record struct DeleteFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteMarketHoliday(string currencyType, DateOnly holidayDate) : IBindValue
{
    public object Bind() => new { currencyType, holidayDate };
}
internal readonly record struct DeleteMarketHolidays(string currencyType) : IBindValue
{
    public object Bind() => new { currencyType };
}
internal readonly record struct DeleteRateOfReturn(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { symbol, valueDate };
}
internal readonly record struct DeleteVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct DeleteYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetCurrentFuturesEodDataByDateRange(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { startDate, endDate };
}
internal readonly record struct GetCurrentFuturesEodDataIndex(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, valueDate, startDate, endDate };
}
internal readonly record struct GetFuturesBarDataCount(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, valueDate };
}
internal readonly record struct GetFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetFuturesDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetFuturesEodDataByDateRange(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { contractId, startDate, endDate };
}
internal readonly record struct GetFuturesEodClosingPrices(string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays) : IBindValue
{
    public object Bind() => new { contractId, symbol, startDate, endDate, maxDays };
}
internal readonly record struct GetFuturesEodMovingAverages(string symbol, DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new { symbol, startDate, endDate };
}
internal readonly record struct GetFuturesIntraDayData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetFuturesItiSignalAverageInfo(string contractId, DateOnly valueDate, long maxSequenceId, string intrinsicTimeTrend, List<string> intrinsicTimeModes) : IBindValue
{
    public object Bind() => new { contractId, valueDate, maxSequenceId, intrinsicTimeTrend, intrinsicTimeModes };
}
internal readonly record struct GetFuturesItiSignalAvgPredictedDelta(List<string> contractIds, DateOnly startDate, DateOnly endDate, string intrinsicTimeTrend, List<string> intrinsicTimeModes) : IBindValue
{
    public object Bind() => new { contractIds, startDate, endDate, intrinsicTimeTrend, intrinsicTimeModes };
}
internal readonly record struct GetFuturesItiSignalMaxTimeGroupId(string contractId, DateOnly maxValueDate, List<string> intrinsicTimeModes, string intrinsicTimeTrend) : IBindValue
{
    public object Bind() => new { contractId, maxValueDate, intrinsicTimeModes, intrinsicTimeTrend };
}
internal readonly record struct GetFuturesItiSignalMaxTrendSequenceId(string contractId, DateOnly maxTrendValueDate, string intrinsicTimeTrend, string intrinsicTimeMode) : IBindValue
{
    public object Bind() => new { contractId, maxTrendValueDate, intrinsicTimeTrend, intrinsicTimeMode };
}
internal readonly record struct GetFuturesItiSignalMaxTrendValueDate(string contractId, DateOnly valueDate, string intrinsicTimeTrend) : IBindValue
{
    public object Bind() => new { contractId, valueDate, intrinsicTimeTrend };
}
internal readonly record struct GetFuturesItiSignalMaxValueDateByTrend(string contractId, DateOnly valueDate, List<string> intrinsicTimeModes, string intrinsicTimeTrend) : IBindValue
{
    public object Bind() => new { contractId, valueDate, intrinsicTimeModes, intrinsicTimeTrend };
}
internal readonly record struct GetFuturesItiSignalMDI(string contractId, DateOnly maxValueDate, List<string> intrinsicTimeModes, string intrinsicTimeTrend) : IBindValue
{
    public object Bind() => new { contractId, maxValueDate, intrinsicTimeModes, intrinsicTimeTrend };
}
internal readonly record struct GetFuturesItiSignalMDIByTrend(string contractId, DateOnly maxValueDate, List<string> intrinsicTimeModes, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new { contractId, maxValueDate, intrinsicTimeModes, intrinsicTimeTrend, intrinsicTimeGroupId };
}
internal readonly record struct GetFuturesItiSignals(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timePeriod };
}
internal readonly record struct GetFuturesItiSignalsByDateRange(List<string> contractIds, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { contractIds, startDate, endDate };
}
internal readonly record struct GetFuturesItiSignalTrendDataByDateRange(List<string> contractIds, DateOnly startDate, DateOnly endDate, List<string> intrinsicTimeModes) : IBindValue
{
    public object Bind() => new { contractIds, startDate, endDate, intrinsicTimeModes };
}
internal readonly record struct GetFuturesItiTrendClassData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { symbol, startDate, endDate };
}
internal readonly record struct GetFuturesItiTrendClassModel(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendClassModelMaxValueDate(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendDeltaData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { symbol, startDate, endDate };
}
internal readonly record struct GetFuturesItiTrendDeltaModel(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendDeltaModelMaxValueDate(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendDirectionChangedSignals(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesOptionTickPriceData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId };
}

internal readonly record struct GetFuturesRsiSignalDownTrendCount(string contractId, DateOnly valueDate, DateTime startTime, DateTime endTime) : IBindValue
{
    public object Bind() => new { contractId, valueDate, startTime, endTime };
}
internal readonly record struct GetFuturesRsiSignalUpTrendCount(string contractId, DateOnly valueDate, DateTime startTime, DateTime endTime) : IBindValue
{
    public object Bind() => new { contractId, valueDate, startTime, endTime };
}
internal readonly record struct GetFuturesTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesTickDataPriceByTickId(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesTickHLVData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesAdxSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesAtrSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, valueDate };
}
internal readonly record struct GetLastFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignalTrendDirectionChange(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignalTrendExtremeChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}
internal readonly record struct GetLastFuturesItiSignalTrendReversalChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}
internal readonly record struct GetLastFuturesMacdSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}

internal readonly record struct GetLastFuturesOptionTickDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesRsiSignal(string contractId, DateOnly valueDate, string signalType) : IBindValue
{
    public object Bind() => new { contractId, valueDate, signalType };
}
internal readonly record struct GetLastFuturesTdiSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesTickDataByTickTime(string contractId, DateOnly valueDate, TimeOnly tickTime) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickTime };
}
internal readonly record struct GetLastFuturesTradeSignalById(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetLastFuturesTradeSignalBySymbol(List<string> contractIds, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractIds, valueDate };
}
internal readonly record struct GetLastRateOfReturn(string symbol) : IBindValue
{
    public object Bind() => new { symbol };
}
internal readonly record struct GetLastVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetMarketHolidays(string currencyType) : IBindValue
{
    public object Bind() => new { currencyType };
}
internal readonly record struct GetMarketHolidaysByDateRange(string currencyType, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { currencyType, startDate, endDate };
}
internal readonly record struct GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetMinFuturesTickDataTickId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetNextFuturesTickId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetVixFuturesEodDataByValueDate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetYesterdaysFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct GetYesterdaysFuturesEodData(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new { valueDate };
}
internal readonly record struct GetYieldCurveRates(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new { startDate, endDate };
}
internal readonly record struct InsertFuturesAdxSignal(string contractId, DateOnly valueDate, TimeOnly timestamp, double plusDI, double minusDI, double adxValue, string adx, string adxStrength) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timestamp, plusDI, minusDI, adxValue, adx, adxStrength };
}
internal readonly record struct InsertFuturesAtrSignal(string contractId, DateOnly valueDate, string timePeriod, string atrSignalSource, TimeOnly timestamp, double atrValue, double trueRange, string atr, string atrStrength) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timePeriod, atrSignalSource, timestamp, atrValue, trueRange, atr, atrStrength };
}
internal readonly record struct InsertFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime barDate, string barRateType, decimal barValue, double upTrendTrigger, double downTrendTrigger) : IBindValue
{
    public object Bind() => new { contractId, symbol, valueDate, barDate, barRateType, barValue, upTrendTrigger, downTrendTrigger };
}
internal readonly record struct InsertFuturesClosingPrice(string contractId, DateOnly valueDate, decimal closingPrice, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new { contractId, valueDate, closingPrice, createdOn, createdBy };
}
internal readonly record struct InsertFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new { contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct InsertFuturesEodDataIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new { valueDate, contractId };
}
internal readonly record struct InsertFuturesIntraDayData(string contractId, DateOnly valueDate, long sequenceId, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new { contractId, valueDate, sequenceId, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct InsertFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState };
}
internal readonly record struct InsertFuturesItiSignalIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new { valueDate, contractId };
}
internal readonly record struct InsertFuturesItiTrendClassModel(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, int count, double maximum, double mean, double median, double minimum, double skewness, double stdDev, double variance, double accuracy, double areaUnderPrecisionRecallCurve, double areaUnderRocCurve, double entropy, double f1Score, byte[] modelData) : IBindValue
{
    public object Bind() => new { symbol, valueDate, startDate, endDate, count, maximum, mean, median, minimum, skewness, stdDev, variance, accuracy, areaUnderPrecisionRecallCurve, areaUnderRocCurve, entropy, f1Score, modelData };
}
internal readonly record struct InsertFuturesItiTrendDeltaModel(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, int count, double maximum, double mean, double median, double minimum, double skewness, double stdDev, double variance, double meanAbsoluteError, double meanSquaredError, double rootMeanSquaredError, double lossFunction, double rSquared, byte[] modelData) : IBindValue
{
    public object Bind() => new { symbol, valueDate, startDate, endDate, count, maximum, mean, median, minimum, skewness, stdDev, variance, meanAbsoluteError, meanSquaredError, rootMeanSquaredError, lossFunction, rSquared, modelData };
}
internal readonly record struct InsertFuturesMacdSignal(string contractId, DateOnly valueDate, TimeOnly timestamp, double macdLine, double signalLine, double histogram, string macd, string macdStrength) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timestamp, macdLine, signalLine, histogram, macd, macdStrength };
}
internal readonly record struct InsertFuturesOptionQuote(int quoteId, string contractId, int requestId, string createdBy, DateTime createdOn) : IBindValue
{
    public object Bind() => new { quoteId, contractId, requestId, createdBy, createdOn };
}
internal readonly record struct InsertFuturesOptionQuoteData(int quoteId, string contractId, int requestId, long sequenceId, decimal bidPrice, int bidSize, decimal askPrice, int askSize) : IBindValue
{
    public object Bind() => new { quoteId, contractId, requestId, sequenceId, bidPrice, bidSize, askPrice, askSize };
}
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}
internal readonly record struct InsertFuturesRsiSignal(string contractId, DateOnly valueDate, TimeOnly timestamp, string signalType, decimal price, decimal priceChange, decimal priceGain, decimal priceLoss, decimal averagePriceGain, decimal averagePriceLoss, double rs, double rsi, double rsiAverage, double rsiSlope, int windowSize) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timestamp, signalType, price, priceChange, priceGain, priceLoss, averagePriceGain, averagePriceLoss, rs, rsi, rsiAverage, rsiSlope, windowSize };
}
internal readonly record struct InsertFuturesTdiSignal(string contractId, DateOnly valueDate, string timePeriod, TimeOnly timestamp, int upTrendCount, int downTrendCount, string tdi, string tdiStrength) : IBindValue
{
    public object Bind() => new { contractId, valueDate, timePeriod, timestamp, upTrendCount, downTrendCount, tdi, tdiStrength };
}
internal readonly record struct InsertFuturesTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, decimal price, int size) : IBindValue
{
    public object Bind() => new { contractId, valueDate, tickId, tickTime, price, size };
}
internal readonly record struct InsertFuturesTradeSignal(string contractId, DateOnly valueDate, long sequenceId, TimeOnly timestamp, double mean, double stdDev, double futuresPrice, double priceChangePercent, double fundRiskPercent, double rsi, double rsiSlope, string trendType, string trendStrength, string tradeSignal, string tdi, string tdiStrength, double mdi, string mdiTrend, double mdiUpTrendLimit, double mdiDownTrendLimit, double upTrendingTrigger, double downTrendingTrigger, double entryTrigger, double exitTrigger, double trendDelta, double trendExtreme, double trendReversal, decimal fiftyDma, decimal twoHundredDma, string tradeExecuteState) : IBindValue
{
    public object Bind() => new { contractId, valueDate, sequenceId, timestamp, mean, stdDev, futuresPrice, priceChangePercent, fundRiskPercent, rsi, rsiSlope, trendType, trendStrength, tradeSignal, tdi, tdiStrength, mdi, mdiTrend, mdiUpTrendLimit, mdiDownTrendLimit, upTrendingTrigger, downTrendingTrigger, entryTrigger, exitTrigger, trendDelta, trendExtreme, trendReversal, fiftyDma, twoHundredDma, tradeExecuteState };
}
internal readonly record struct InsertMarketHoliday(string currencyType, DateOnly holidayDate, string description) : IBindValue
{
    public object Bind() => new { currencyType, holidayDate, description };
}
internal readonly record struct InsertRateOfReturn(string symbol, DateOnly valueDate, double rateOfReturn) : IBindValue
{
    public object Bind() => new { symbol, valueDate, rateOfReturn };
}
internal readonly record struct InsertTradeLiveFeed(int orderId, int tradeId, string tradeLiveFeedState) : IBindValue
{
    public object Bind() => new { orderId, tradeId, tradeLiveFeedState };
}
internal readonly record struct GetTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new { orderId, tradeId };
}
internal readonly record struct InsertVixFuturesEodData(string contractId, DateOnly valueDate, decimal price, int size) : IBindValue
{
    public object Bind() => new { contractId, valueDate, price, size };
}
internal readonly record struct InsertYieldCurveRate(int id, DateOnly valueDate, double oneMonth, double twoMonth, double threeMonth, double sixMonth, double oneYear, double twoYear, double threeYear, double fiveYear, double sevenYear, double tenYear, double twentyYear, double thirtyYear) : IBindValue
{
    public object Bind() => new { id, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear };
}
internal readonly record struct UpdateFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new { contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct UpdateNextFuturesTickId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new { contractId, valueDate };
}
internal readonly record struct UpdateVixFuturesEodData(string contractId, DateOnly valueDate, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume) : IBindValue
{
    public object Bind() => new { contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume };
}
