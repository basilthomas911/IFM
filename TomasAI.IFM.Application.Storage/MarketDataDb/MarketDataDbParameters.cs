using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal readonly record struct DeleteFuturesAdxSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct DeleteFuturesAtrSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct DeleteFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, valueDate };
}
internal readonly record struct DeleteFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteFuturesEodDataByMonth(int yearMonth, DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, valueDate, contractId };
}
internal readonly record struct DeleteFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod };
}
internal readonly record struct DeleteFuturesItiSignalByContractDayV2(string contractId, DateOnly valueDate, string intrinsicTimeMode, long sequenceId, string timePeriod, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode, sequenceId, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId };
}
internal readonly record struct DeleteFuturesItiSignalByContractMonthV2(string contractId, int yearMonth, DateOnly valueDate, long sequenceId, string timePeriod, string intrinsicTimeMode, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId };
}
internal readonly record struct DeleteFuturesItiSignalByTrendModeMonthV2(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly valueDate, long sequenceId, string timePeriod, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeGroupId };
}
internal readonly record struct DeleteFuturesItiTrendClassData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, startDate, endDate };
}
internal readonly record struct DeleteFuturesItiTrendDeltaData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, startDate, endDate };
}
internal readonly record struct DeleteFuturesOptionQuoteData(int quoteId) : IBindValue
{
    public object Bind() => new object?[] { quoteId };
}
internal readonly record struct DeleteFuturesOptionQuotes(int quoteId) : IBindValue
{
    public object Bind() => new object?[] { quoteId };
}
internal readonly record struct DeleteFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}

internal readonly record struct DeleteFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}

internal readonly record struct DeleteFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteFuturesTickDataByTime(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteMarketHoliday(string currencyType, DateOnly holidayDate) : IBindValue
{
    public object Bind() => new object?[] { currencyType, holidayDate };
}
internal readonly record struct DeleteMarketHolidays(string currencyType) : IBindValue
{
    public object Bind() => new object?[] { currencyType };
}
internal readonly record struct DeleteRateOfReturn(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate };
}
internal readonly record struct DeleteVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct DeleteVixFuturesContractIndex(int bucket, string contractId) : IBindValue
{
    public object Bind() => new object?[] { bucket, contractId };
}
internal readonly record struct DeleteYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { valueDate };
}
internal readonly record struct GetCurrentFuturesEodDataByDateRange(int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, startDate, endDate };
}
internal readonly record struct GetCurrentFuturesEodDataByMonth(int yearMonth, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, valueDate };
}
internal readonly record struct GetFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, valueDate, startDate, endDate };
}
internal readonly record struct GetFuturesBarDataCount(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, valueDate };
}
internal readonly record struct GetFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetFuturesDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetFuturesEodDataByDateRange(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, startDate, endDate };
}
internal readonly record struct GetFuturesEodClosingPrices(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, startDate, endDate };
}
internal readonly record struct GetFuturesIntraDayData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetFuturesItiSignals(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod };
}
internal readonly record struct GetFuturesItiTrendClassData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, startDate, endDate };
}
internal readonly record struct GetFuturesItiTrendClassModel(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendClassModelMaxValueDate(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendDeltaData(string symbol, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, startDate, endDate };
}
internal readonly record struct GetFuturesItiTrendDeltaModel(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate };
}
internal readonly record struct GetFuturesItiTrendDeltaModelMaxValueDate(string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate };
}
internal readonly record struct GetFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesOptionTickPriceData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}

internal readonly record struct GetFuturesRsiSignalsForTrend(string contractId, string timePeriod, int periodLength, DateOnly valueDate, TimeOnly startTime, TimeOnly endTime) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate, startTime, endTime };
}
internal readonly record struct GetFuturesItiSignalsCanonicalByContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { contractId };
}
internal readonly record struct GetFuturesItiSignalsCanonicalByContractDay(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetFuturesItiSignalsByContractMonthV2(string contractId, int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, startDate, endDate };
}
internal readonly record struct GetFuturesItiSignalsByContractDayModeV2(string contractId, DateOnly valueDate, string intrinsicTimeMode) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode };
}
internal readonly record struct GetFuturesItiSignalsByContractDayModeAfterSequenceV2(string contractId, DateOnly valueDate, string intrinsicTimeMode, long sequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode, sequenceId };
}
internal readonly record struct GetLastFuturesItiSignalByTrendModeMonthV2(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate };
}
internal readonly record struct GetFuturesItiSignalsByTrendModeMonthV2(string contractId, string intrinsicTimeTrend, string intrinsicTimeMode, int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, startDate, endDate };
}
internal readonly record struct GetFuturesTickData(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesTickDataPriceByTickId(string contractId, DateOnly valueDate, long tickId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId };
}
internal readonly record struct GetFuturesTickHLVData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesAdxSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct GetLastFuturesAdxDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength };
}
internal readonly record struct GetLastFuturesAtrSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct GetLastFuturesAtrDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength };
}
internal readonly record struct GetLastFuturesBarData(string contractId, string symbol, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, valueDate };
}
internal readonly record struct GetLastFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignalByTimePeriod(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod };
}
internal readonly record struct GetLastFuturesItiSignalTrendDirectionChange(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesItiSignalTrendExtremeChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}
internal readonly record struct GetLastFuturesItiSignalTrendReversalChange(string contractId, DateOnly valueDate, long lastTrendDirectionChangedSequenceId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, lastTrendDirectionChangedSequenceId };
}
internal readonly record struct GetLastFuturesMacdSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct GetLastFuturesMacdDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength };
}
internal readonly record struct GetLastFuturesOptionTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesOptionTickPriceData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}

internal readonly record struct GetLastFuturesOptionTickDataId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesRsiSignal(string contractId, string timePeriod, int periodLength, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength, valueDate };
}
internal readonly record struct GetLastFuturesRsiDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    public object Bind() => new object?[] { contractId, timePeriod, periodLength };
}
internal readonly record struct GetLastFuturesTdiSignal(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesTickData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetLastFuturesTickDataByTickTime(string contractId, DateOnly valueDate, TimeOnly tickTime) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime };
}
internal readonly record struct GetLastFuturesTradeSignalById(string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod };
}
internal readonly record struct GetLastFuturesTradeSignal(string scope) : IBindValue
{
    public object Bind() => new object?[] { scope };
}
internal readonly record struct GetLastFuturesTradeSignalBySymbol(List<string> contractIds, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractIds, valueDate };
}
internal readonly record struct GetLastRateOfReturn(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct GetLastVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetMarketHolidays(string currencyType) : IBindValue
{
    public object Bind() => new object?[] { currencyType };
}
internal readonly record struct GetMarketHolidaysByDateRange(string currencyType, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { currencyType, startDate, endDate };
}
internal readonly record struct GetFuturesTradeSignalIdByValueDate(string scope) : IBindValue
{
    public object Bind() => new object?[] { scope };
}
internal readonly record struct GetMarketDataProjectionMonths(string projectionName, int yearMonth) : IBindValue
{
    public object Bind() => new object?[] { projectionName, yearMonth };
}
internal readonly record struct GetMarketDataProjectionState(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct GetMarketDataProjectionMutation(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct GetMarketDataProjectionScopeStatesV3(
    string projectionName,
    ICollection<string> scopeKeys) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKeys };
}
internal readonly record struct GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetMinFuturesTickDataTickId(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetVixFuturesEodDataThroughDate(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetVixFuturesContractIds(int bucket) : IBindValue
{
    public object Bind() => new object?[] { bucket };
}
internal readonly record struct GetYesterdaysFuturesClosingPrice(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetYesterdaysFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
internal readonly record struct GetYieldCurveRate(DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { valueDate };
}
internal readonly record struct GetYieldCurveRates(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { startDate, endDate };
}
internal readonly record struct InsertFuturesAdxSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double plusDI, double minusDI, double adxValue, string adx, string adxStrength) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, plusDI, minusDI, adxValue, adx, adxStrength };
}
internal readonly record struct InsertFuturesAtrSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double atrValue, double trueRange, string atr, string atrStrength) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, atrValue, trueRange, atr, atrStrength };
}
internal readonly record struct InsertFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime barDate, string barRateType, decimal barValue, double upTrendTrigger, double downTrendTrigger) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, valueDate, barDate, barRateType, barValue, upTrendTrigger, downTrendTrigger };
}
internal readonly record struct InsertFuturesClosingPrice(string contractId, DateOnly valueDate, decimal closingPrice, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, closingPrice, createdOn, createdBy };
}
internal readonly record struct InsertFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct InsertFuturesEodDataByMonth(int yearMonth, string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct InsertFuturesEodDataIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { valueDate, contractId };
}
internal readonly record struct InsertFuturesIntraDayData(string contractId, DateOnly valueDate, long sequenceId, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, sequenceId, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
internal readonly record struct InsertFuturesItiSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState };
}
internal readonly record struct InsertFuturesItiSignalIndex(DateOnly valueDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { valueDate, contractId };
}
internal readonly record struct InsertFuturesItiSignalByContractMonthV2(int yearMonth, string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState };
}
internal readonly record struct InsertFuturesItiTrendClassData(string symbol, DateOnly valueDate, DateTime timestamp, long sequenceId, float trendClass, float trendDirection, float trendDirectionMode, float trendDelta, float futuresRSI) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate, timestamp, sequenceId, trendClass, trendDirection, trendDirectionMode, trendDelta, futuresRSI };
}
internal readonly record struct InsertFuturesItiTrendDeltaData(string symbol, DateOnly valueDate, DateTime timestamp, long sequenceId, float trendDelta, float trendDirection, float trendDirectionMode, float futuresPrice, float trendExtreme, float futuresRSI) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate, timestamp, sequenceId, trendDelta, trendDirection, trendDirectionMode, futuresPrice, trendExtreme, futuresRSI };
}
internal readonly record struct InsertFuturesItiTrendClassModel(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, int count, double maximum, double mean, double median, double minimum, double skewness, double stdDev, double variance, double accuracy, double areaUnderPrecisionRecallCurve, double areaUnderRocCurve, double entropy, double f1Score, byte[] modelData) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate, startDate, endDate, count, maximum, mean, median, minimum, skewness, stdDev, variance, accuracy, areaUnderPrecisionRecallCurve, areaUnderRocCurve, entropy, f1Score, modelData };
}
internal readonly record struct InsertFuturesItiTrendDeltaModel(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, int count, double maximum, double mean, double median, double minimum, double skewness, double stdDev, double variance, double meanAbsoluteError, double meanSquaredError, double rootMeanSquaredError, double lossFunction, double rSquared, byte[] modelData) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate, startDate, endDate, count, maximum, mean, median, minimum, skewness, stdDev, variance, meanAbsoluteError, meanSquaredError, rootMeanSquaredError, lossFunction, rSquared, modelData };
}
internal readonly record struct InsertFuturesMacdSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double macdLine, double signalLine, double histogram, string macd, string macdStrength) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, macdLine, signalLine, histogram, macd, macdStrength };
}
internal readonly record struct InsertFuturesOptionQuote(int quoteId, string contractId, int requestId, string createdBy, DateTime createdOn) : IBindValue
{
    public object Bind() => new object?[] { quoteId, contractId, requestId, createdBy, createdOn };
}
internal readonly record struct InsertFuturesOptionQuoteData(int quoteId, string contractId, int requestId, long sequenceId, decimal bidPrice, int bidSize, decimal askPrice, int askSize) : IBindValue
{
    public object Bind() => new object?[] { quoteId, contractId, requestId, sequenceId, bidPrice, bidSize, askPrice, askSize };
}
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}
internal readonly record struct InsertFuturesRsiSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal price, decimal priceChange, decimal priceGain, decimal priceLoss, decimal averagePriceGain, decimal averagePriceLoss, double rs, double rsi, double rsiAverage, double rsiSlope) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, price, priceChange, priceGain, priceLoss, averagePriceGain, averagePriceLoss, rs, rsi, rsiAverage, rsiSlope };
}
internal readonly record struct InsertFuturesTdiSignal(string contractId, DateOnly valueDate, string timePeriod, TimeOnly timestamp, int upTrendCount, int downTrendCount, string tdi, string tdiStrength) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, timestamp, upTrendCount, downTrendCount, tdi, tdiStrength };
}
internal readonly record struct InsertTickTradeData(object?[] Values) : IBindValue
{
    public object Bind() => Values;
}

internal readonly record struct InsertTickQuoteData(object?[] Values) : IBindValue
{
    public object Bind() => Values;
}

internal readonly record struct InsertFuturesTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, price, size };
}
internal readonly record struct InsertFuturesTickDataByTime(string contractId, DateOnly valueDate, TimeOnly tickTime, long tickId, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime, tickId, price, size };
}
internal readonly record struct InsertFuturesTradeSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, TimeOnly timestamp, double mean, double stdDev, double futuresPrice, double priceChangePercent, double fundRiskPercent, double rsi, double rsiSlope, string trendType, string trendStrength, string tradeSignal, string tdi, string tdiStrength, double mdi, string mdiTrend, double mdiUpTrendLimit, double mdiDownTrendLimit, double upTrendingTrigger, double downTrendingTrigger, double entryTrigger, double exitTrigger, double trendDelta, double trendExtreme, double trendReversal, decimal fiftyDma, decimal twoHundredDma, string tradeExecuteState) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, timePeriod, sequenceId, timestamp, mean, stdDev, futuresPrice, priceChangePercent, fundRiskPercent, rsi, rsiSlope, trendType, trendStrength, tradeSignal, tdi, tdiStrength, mdi, mdiTrend, mdiUpTrendLimit, mdiDownTrendLimit, upTrendingTrigger, downTrendingTrigger, entryTrigger, exitTrigger, trendDelta, trendExtreme, trendReversal, fiftyDma, twoHundredDma, tradeExecuteState };
}
internal readonly record struct InsertFuturesTradeSignalIndex(string scope, string entryId, long sequenceId, string contractId, DateOnly valueDate, string timePeriod) : IBindValue
{
    public object Bind() => new object?[] { scope, entryId, sequenceId, contractId, valueDate, timePeriod };
}
internal readonly record struct InsertMarketHoliday(string currencyType, DateOnly holidayDate, string description) : IBindValue
{
    public object Bind() => new object?[] { currencyType, holidayDate, description };
}
internal readonly record struct InsertMarketDataProjectionMonth(string projectionName, int yearMonth) : IBindValue
{
    public object Bind() => new object?[] { projectionName, yearMonth };
}
internal readonly record struct InsertMarketDataProjectionMutation(string projectionName, Guid mutationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId, startedOn };
}
internal readonly record struct DeleteMarketDataProjectionMutation(string projectionName, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId };
}
internal readonly record struct FailMarketDataProjectionMutation(
    string projectionName,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { startedOn, projectionName, mutationId };
}
internal readonly record struct BeginMarketDataProjectionOperation(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}
internal readonly record struct EndMarketDataProjectionOperation(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}
internal readonly record struct RemoveMarketDataProjectionOperations(
    string projectionName,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName };
}
internal readonly record struct RestoreMarketDataProjectionState(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        completedOn,
        projectionName,
        generation,
        expectedActiveOperations
    };
}
internal readonly record struct CompleteMarketDataProjectionState(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations,
    long sourceRowCount,
    long projectedRowCount,
    string sourceFingerprint,
    string projectedFingerprint,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        sourceRowCount,
        projectedRowCount,
        sourceFingerprint,
        projectedFingerprint,
        completedOn,
        projectionName,
        generation,
        expectedActiveOperations
    };
}
internal readonly record struct BeginMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, scopeKey };
}
internal readonly record struct EndMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, scopeKey };
}
internal readonly record struct MarkMarketDataProjectionScopeAtomicWriteV3(
    string projectionName,
    string scopeKey,
    Guid generation) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName, scopeKey };
}
internal readonly record struct RegisterMarketDataProjectionGuardOperationV3(
    string projectionName,
    string scopeKey,
    HashSet<Guid> activeOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, scopeKey };
}
internal readonly record struct CompleteMarketDataProjectionGuardOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        generation,
        activeOperations,
        completedOn,
        projectionName,
        scopeKey,
        expectedActiveOperations
    };
}
internal readonly record struct CompleteMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        completedOn,
        projectionName,
        scopeKey,
        generation,
        expectedActiveOperations
    };
}
internal readonly record struct RemoveMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName, scopeKey };
}
internal readonly record struct InsertMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKey, mutationId, startedOn };
}
internal readonly record struct FailMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { startedOn, projectionName, scopeKey, mutationId };
}
internal readonly record struct DeleteMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKey, mutationId };
}
internal readonly record struct InsertRateOfReturn(string symbol, DateOnly valueDate, double rateOfReturn) : IBindValue
{
    public object Bind() => new object?[] { symbol, valueDate, rateOfReturn };
}
internal readonly record struct InsertTradeLiveFeed(int orderId, int tradeId, string tradeLiveFeedState) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeLiveFeedState };
}
internal readonly record struct GetTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct DeleteTradeLiveFeed(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
internal readonly record struct InsertVixFuturesEodData(
    string contractId,
    DateOnly valueDate,
    decimal openPrice,
    decimal highPrice,
    decimal lowPrice,
    decimal closePrice,
    int volume) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume };
}
internal readonly record struct InsertVixFuturesContractIndex(int bucket, string contractId) : IBindValue
{
    public object Bind() => new object?[] { bucket, contractId };
}
internal readonly record struct InsertYieldCurveRate(int id, DateOnly valueDate, double oneMonth, double twoMonth, double threeMonth, double sixMonth, double oneYear, double twoYear, double threeYear, double fiveYear, double sevenYear, double tenYear, double twentyYear, double thirtyYear) : IBindValue
{
    public object Bind() => new object?[] { id, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear };
}
internal readonly record struct UpdateFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public object Bind() => new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, contractId, valueDate, symbol };
}
internal readonly record struct UpdateVixFuturesEodData(string contractId, DateOnly valueDate, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume) : IBindValue
{
    public object Bind() => new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, contractId, valueDate };
}
