namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Native PostgreSQL session snapshot and observation statements.</summary>
public static class MarketDataDbSql
{
    public const string InsertFuturesEodData = """
        INSERT INTO market_data.futures_eod_data ( contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22 ) ON CONFLICT (contractId,valueDate,symbol) DO UPDATE SET openPrice=EXCLUDED.openPrice,highPrice=EXCLUDED.highPrice,lowPrice=EXCLUDED.lowPrice,closePrice=EXCLUDED.closePrice,volume=EXCLUDED.volume,dailyPercentChange=EXCLUDED.dailyPercentChange,dailyStdDev=EXCLUDED.dailyStdDev,dailyStdDevAmount=EXCLUDED.dailyStdDevAmount,upperBand=EXCLUDED.upperBand,mean=EXCLUDED.mean,lowerBand=EXCLUDED.lowerBand,marketDirection=EXCLUDED.marketDirection,marketVolatility=EXCLUDED.marketVolatility,priceDirection=EXCLUDED.priceDirection,priceVolatility=EXCLUDED.priceVolatility,marketDirectionIndicator=EXCLUDED.marketDirectionIndicator,windowSize=EXCLUDED.windowSize,fiftyDMA=EXCLUDED.fiftyDMA,twoHundredDMA=EXCLUDED.twoHundredDMA;
        """;

    public const string InsertFuturesEodDataByMonth = """
        INSERT INTO market_data.futures_eod_data_by_month ( yearMonth, contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 ) ON CONFLICT (yearMonth,valueDate,contractId,symbol) DO UPDATE SET openPrice=EXCLUDED.openPrice,highPrice=EXCLUDED.highPrice,lowPrice=EXCLUDED.lowPrice,closePrice=EXCLUDED.closePrice,volume=EXCLUDED.volume,dailyPercentChange=EXCLUDED.dailyPercentChange,dailyStdDev=EXCLUDED.dailyStdDev,dailyStdDevAmount=EXCLUDED.dailyStdDevAmount,upperBand=EXCLUDED.upperBand,mean=EXCLUDED.mean,lowerBand=EXCLUDED.lowerBand,marketDirection=EXCLUDED.marketDirection,marketVolatility=EXCLUDED.marketVolatility,priceDirection=EXCLUDED.priceDirection,priceVolatility=EXCLUDED.priceVolatility,marketDirectionIndicator=EXCLUDED.marketDirectionIndicator,windowSize=EXCLUDED.windowSize,fiftyDMA=EXCLUDED.fiftyDMA,twoHundredDMA=EXCLUDED.twoHundredDMA;
        """;

    public const string InsertFuturesIntraDayData = """
        INSERT INTO market_data.futures_intra_day_data ( contractId, valueDate, sequenceId, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21 ) ON CONFLICT (contractId,valueDate,sequenceId) DO UPDATE SET symbol=EXCLUDED.symbol,openPrice=EXCLUDED.openPrice,highPrice=EXCLUDED.highPrice,lowPrice=EXCLUDED.lowPrice,closePrice=EXCLUDED.closePrice,volume=EXCLUDED.volume,dailyPercentChange=EXCLUDED.dailyPercentChange,dailyStdDev=EXCLUDED.dailyStdDev,dailyStdDevAmount=EXCLUDED.dailyStdDevAmount,upperBand=EXCLUDED.upperBand,mean=EXCLUDED.mean,lowerBand=EXCLUDED.lowerBand,marketDirection=EXCLUDED.marketDirection,marketVolatility=EXCLUDED.marketVolatility,priceDirection=EXCLUDED.priceDirection,priceVolatility=EXCLUDED.priceVolatility,marketDirectionIndicator=EXCLUDED.marketDirectionIndicator,windowSize=EXCLUDED.windowSize;
        """;

    public const string InsertFuturesEodDataIndex = """
        INSERT INTO market_data.futures_eod_data_index (valueDate, contractId) VALUES ($1, $2) ON CONFLICT (valueDate,contractId) DO NOTHING;
        """;

    public const string GetFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate = $2 LIMIT 1;
        """;

    public const string GetYesterdaysFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate < $2 ORDER BY valueDate DESC LIMIT 1;
        """;

    public const string GetLastFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate < $2 ORDER BY valueDate DESC, symbol ASC LIMIT 1;
        """;

    public const string GetFuturesEodDataAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data
        """;

    public const string GetFuturesIntraDayData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", sequenceId AS "SequenceId", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize" FROM market_data.futures_intra_day_data WHERE contractId = $1 AND valueDate = $2 ORDER BY sequenceId DESC;
        """;

    public const string GetFuturesDataId = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate = $2;
        """;


    public const string InsertFuturesTdiSignal = """
        INSERT INTO market_data.futures_traders_dynamic_index_signal ( contractId, timePeriod, configurationId, valueDate, timestamp, schemaVersion, rsiPeriod, priceLinePeriod, signalLinePeriod, marketBasePeriod, volatilityBandPeriod, volatilityBandDeviation, price, rsi, priceLine, signalLine, marketBaseLine, upperVolatilityBand, lowerVolatilityBand, bandWidth, priceSignalDivergence, crossType, marketState, trendDirection, trendStrength, sourceSequence, sourceEventTimestamp ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27 ) ON CONFLICT (contractId,timePeriod,configurationId,valueDate,timestamp) DO UPDATE SET schemaVersion=EXCLUDED.schemaVersion,rsiPeriod=EXCLUDED.rsiPeriod,priceLinePeriod=EXCLUDED.priceLinePeriod,signalLinePeriod=EXCLUDED.signalLinePeriod,marketBasePeriod=EXCLUDED.marketBasePeriod,volatilityBandPeriod=EXCLUDED.volatilityBandPeriod,volatilityBandDeviation=EXCLUDED.volatilityBandDeviation,price=EXCLUDED.price,rsi=EXCLUDED.rsi,priceLine=EXCLUDED.priceLine,signalLine=EXCLUDED.signalLine,marketBaseLine=EXCLUDED.marketBaseLine,upperVolatilityBand=EXCLUDED.upperVolatilityBand,lowerVolatilityBand=EXCLUDED.lowerVolatilityBand,bandWidth=EXCLUDED.bandWidth,priceSignalDivergence=EXCLUDED.priceSignalDivergence,crossType=EXCLUDED.crossType,marketState=EXCLUDED.marketState,trendDirection=EXCLUDED.trendDirection,trendStrength=EXCLUDED.trendStrength,sourceSequence=EXCLUDED.sourceSequence,sourceEventTimestamp=EXCLUDED.sourceEventTimestamp;
        """;

    public const string GetLastFuturesTdiSignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod AS "TimePeriod", Timestamp AS "Timestamp", SchemaVersion AS "SchemaVersion", ConfigurationId AS "ConfigurationId", RsiPeriod AS "RsiPeriod", PriceLinePeriod AS "PriceLinePeriod", SignalLinePeriod AS "SignalLinePeriod", MarketBasePeriod AS "MarketBasePeriod", VolatilityBandPeriod AS "VolatilityBandPeriod", VolatilityBandDeviation AS "VolatilityBandDeviation", Price AS "Price", Rsi AS "Rsi", PriceLine AS "PriceLine", SignalLine AS "SignalLine", MarketBaseLine AS "MarketBaseLine", UpperVolatilityBand AS "UpperVolatilityBand", LowerVolatilityBand AS "LowerVolatilityBand", BandWidth AS "BandWidth", PriceSignalDivergence AS "PriceSignalDivergence", CrossType AS "CrossType", MarketState AS "MarketState", TrendDirection AS "TrendDirection", TrendStrength AS "TrendStrength", SourceSequence AS "SourceSequence", SourceEventTimestamp AS "SourceEventTimestamp" FROM market_data.futures_traders_dynamic_index_signal WHERE ContractId = $1 AND TimePeriod = $2 AND ConfigurationId = $3 AND ValueDate = $4 ORDER BY timestamp DESC LIMIT 1;
        """;


    public const string GetFuturesEodDataByDateRange = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate >= $2 AND valueDate <= $3 ORDER BY valueDate DESC, symbol ASC;
        """;

    public const string DeleteFuturesEodData = """
        DELETE FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate = $2;
        """;

    public const string DeleteFuturesEodDataByMonth = """
        DELETE FROM market_data.futures_eod_data_by_month WHERE yearMonth = $1 AND valueDate = $2 AND contractId = $3;
        """;

    public const string GetCurrentFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE valueDate <= $1 ORDER BY valueDate DESC, contractId ASC, symbol ASC LIMIT 1;
        """;


    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    public const string InsertFuturesAtrSignal = """
        INSERT INTO market_data.futures_atr_signal ( contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, atrValue, trueRange, atr, atrStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, previousAtrValue, atrBaseline, atrRatio, isWarm ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22 ) ON CONFLICT (contractId, timePeriod, periodLength, valueDate, timestamp) DO UPDATE SET futuresPrice = EXCLUDED.futuresPrice, atrValue = EXCLUDED.atrValue, trueRange = EXCLUDED.trueRange, atr = EXCLUDED.atr, atrStrength = EXCLUDED.atrStrength, configurationId = EXCLUDED.configurationId, observationId = EXCLUDED.observationId, marketDataAsOf = EXCLUDED.marketDataAsOf, sourceSequence = EXCLUDED.sourceSequence, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, schemaVersion = EXCLUDED.schemaVersion, isValid = EXCLUDED.isValid, previousAtrValue = EXCLUDED.previousAtrValue, atrBaseline = EXCLUDED.atrBaseline, atrRatio = EXCLUDED.atrRatio, isWarm = EXCLUDED.isWarm;
        """;

    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    public const string InsertFuturesAdxSignal = """
        INSERT INTO market_data.futures_adx_signal ( contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, plusDI, minusDI, adxValue, adx, adxStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, isWarm ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20 ) ON CONFLICT (contractId, timePeriod, periodLength, valueDate, timestamp) DO UPDATE SET futuresPrice = EXCLUDED.futuresPrice, plusDI = EXCLUDED.plusDI, minusDI = EXCLUDED.minusDI, adxValue = EXCLUDED.adxValue, adx = EXCLUDED.adx, adxStrength = EXCLUDED.adxStrength, configurationId = EXCLUDED.configurationId, observationId = EXCLUDED.observationId, marketDataAsOf = EXCLUDED.marketDataAsOf, sourceSequence = EXCLUDED.sourceSequence, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, schemaVersion = EXCLUDED.schemaVersion, isValid = EXCLUDED.isValid, isWarm = EXCLUDED.isWarm;
        """;

    /// <summary>Native PostgreSQL idempotent observation write.</summary>
    public const string InsertFuturesMacdSignal = """
        INSERT INTO market_data.futures_macd_signal ( contractId, valueDate, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, timestamp, futuresPrice, fastEma, slowEma, macdLine, signalLine, histogram, macd, macdStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, isWarm, observationCount ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25 ) ON CONFLICT (contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, valueDate, timestamp) DO UPDATE SET futuresPrice = EXCLUDED.futuresPrice, fastEma = EXCLUDED.fastEma, slowEma = EXCLUDED.slowEma, macdLine = EXCLUDED.macdLine, signalLine = EXCLUDED.signalLine, histogram = EXCLUDED.histogram, macd = EXCLUDED.macd, macdStrength = EXCLUDED.macdStrength, configurationId = EXCLUDED.configurationId, observationId = EXCLUDED.observationId, marketDataAsOf = EXCLUDED.marketDataAsOf, sourceSequence = EXCLUDED.sourceSequence, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, schemaVersion = EXCLUDED.schemaVersion, isValid = EXCLUDED.isValid, isWarm = EXCLUDED.isWarm, observationCount = EXCLUDED.observationCount;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesAtrSignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod as "TimePeriod", PeriodLength as "PeriodLength", Timestamp AS "Timestamp", FuturesPrice as "FuturesPrice", AtrValue AS "AtrValue", TrueRange AS "TrueRange", ATR AS "ATR", ATRStrength AS "ATRStrength", PreviousAtrValue AS "PreviousAtrValue", AtrBaseline AS "AtrBaseline", AtrRatio AS "AtrRatio", IsWarm AS "IsWarm", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid" FROM market_data.futures_atr_signal WHERE ContractId = $1 AND TimePeriod = $2 AND PeriodLength = $3 AND ValueDate = $4 ORDER BY timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesDailyAtrSignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod as "TimePeriod", PeriodLength as "PeriodLength", Timestamp AS "Timestamp", FuturesPrice as "FuturesPrice", AtrValue AS "AtrValue", TrueRange AS "TrueRange", ATR AS "ATR", ATRStrength AS "ATRStrength", PreviousAtrValue AS "PreviousAtrValue", AtrBaseline AS "AtrBaseline", AtrRatio AS "AtrRatio", IsWarm AS "IsWarm", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid" FROM market_data.futures_atr_signal WHERE ContractId = $1 AND TimePeriod = $2 AND PeriodLength = $3 ORDER BY valueDate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesAdxSignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod as "TimePeriod", PeriodLength as "PeriodLength", Timestamp AS "Timestamp", FuturesPrice AS "FuturesPrice", PlusDI AS "PlusDI", MinusDI AS "MinusDI", AdxValue AS "AdxValue", ADX AS "ADX", ADXStrength AS "ADXStrength", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid", IsWarm AS "IsWarm" FROM market_data.futures_adx_signal WHERE ContractId = $1 AND TimePeriod = $2 AND PeriodLength = $3 AND ValueDate = $4 ORDER BY timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesAdxDailySignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod as "TimePeriod", PeriodLength as "PeriodLength", Timestamp AS "Timestamp", FuturesPrice AS "FuturesPrice", PlusDI AS "PlusDI", MinusDI AS "MinusDI", AdxValue AS "AdxValue", ADX AS "ADX", ADXStrength AS "ADXStrength", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid", IsWarm AS "IsWarm" FROM market_data.futures_adx_signal WHERE ContractId = $1 AND TimePeriod = $2 AND PeriodLength = $3 ORDER BY valueDate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesMacdSignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod AS "TimePeriod", SignalEmaPeriod AS "SignalEmaPeriod", FastEmaPeriod AS "FastEmaPeriod", SlowEmaPeriod AS "SlowEmaPeriod", Timestamp AS "Timestamp", FuturesPrice AS "FuturesPrice", FastEma AS "FastEma", SlowEma AS "SlowEma", MacdLine AS "MacdLine", SignalLine AS "SignalLine", Histogram AS "Histogram", MACD AS "MACD", MACDStrength AS "MACDStrength", IsWarm AS "IsWarm", ObservationCount AS "ObservationCount", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid" FROM market_data.futures_macd_signal WHERE ContractId = $1 AND TimePeriod = $2 AND SignalEmaPeriod = $3 AND FastEmaPeriod = $4 AND SlowEmaPeriod = $5 AND ValueDate = $6 ORDER BY timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string GetLastFuturesMacdDailySignal = """
        SELECT ContractId AS "ContractId", ValueDate AS "ValueDate", TimePeriod AS "TimePeriod", SignalEmaPeriod AS "SignalEmaPeriod", FastEmaPeriod AS "FastEmaPeriod", SlowEmaPeriod AS "SlowEmaPeriod", Timestamp AS "Timestamp", FuturesPrice AS "FuturesPrice", FastEma AS "FastEma", SlowEma AS "SlowEma", MacdLine AS "MacdLine", SignalLine AS "SignalLine", Histogram AS "Histogram", MACD AS "MACD", MACDStrength AS "MACDStrength", IsWarm AS "IsWarm", ObservationCount AS "ObservationCount", ConfigurationId AS "ConfigurationId", ObservationId AS "ObservationId", MarketDataAsOf AS "MarketDataAsOf", SourceSequence AS "SourceSequence", CalculationVersion AS "CalculationVersion", CalculationMethod AS "CalculationMethod", SchemaVersion AS "SchemaVersion", IsValid AS "IsValid" FROM market_data.futures_macd_signal WHERE ContractId = $1 AND TimePeriod = $2 AND SignalEmaPeriod = $3 AND FastEmaPeriod = $4 AND SlowEmaPeriod = $5 ORDER BY valueDate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string DeleteFuturesAtrSignal = """
        DELETE FROM market_data.futures_atr_signal WHERE contractId = $1 AND timePeriod = $2 AND periodLength = $3 AND valueDate = $4;
        """;

    /// <summary>Native PostgreSQL indicator query.</summary>
    public const string DeleteFuturesAdxSignal = """
        DELETE FROM market_data.futures_adx_signal WHERE contractId = $1 AND timePeriod = $2 AND periodLength = $3 AND valueDate = $4;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string InsertFuturesEmaSignal = """
        INSERT INTO market_data.futures_ema_signal ( seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId, contractId, valueDate, price, ema10, previousEma10, ema10Slope, ema20, previousEma20, ema20Slope, ema50, previousEma50, ema50Slope, ema200, previousEma200, ema200Slope, isWarm, sourceSequence, calculatedAt, schemaVersion, calculationVersion, calculationMethod, isValid ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28 ) ON CONFLICT (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId) DO UPDATE SET contractId = EXCLUDED.contractId, valueDate = EXCLUDED.valueDate, price = EXCLUDED.price, ema10 = EXCLUDED.ema10, previousEma10 = EXCLUDED.previousEma10, ema10Slope = EXCLUDED.ema10Slope, ema20 = EXCLUDED.ema20, previousEma20 = EXCLUDED.previousEma20, ema20Slope = EXCLUDED.ema20Slope, ema50 = EXCLUDED.ema50, previousEma50 = EXCLUDED.previousEma50, ema50Slope = EXCLUDED.ema50Slope, ema200 = EXCLUDED.ema200, previousEma200 = EXCLUDED.previousEma200, ema200Slope = EXCLUDED.ema200Slope, isWarm = EXCLUDED.isWarm, sourceSequence = EXCLUDED.sourceSequence, calculatedAt = EXCLUDED.calculatedAt, schemaVersion = EXCLUDED.schemaVersion, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, isValid = EXCLUDED.isValid;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string InsertFuturesBollingerBandSignal = """
        INSERT INTO market_data.futures_bollinger_band_signal ( seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId, contractId, valueDate, price, ema10Center, standardDeviation10, upper10, lower10, width10, position10, ema20Center, standardDeviation20, upper20, lower20, width20, position20, width20Baseline, width20Ratio, isWarm, sourceSequence, calculatedAt, schemaVersion, calculationVersion, calculationMethod, isValid ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30 ) ON CONFLICT (seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId) DO UPDATE SET contractId = EXCLUDED.contractId, valueDate = EXCLUDED.valueDate, price = EXCLUDED.price, ema10Center = EXCLUDED.ema10Center, standardDeviation10 = EXCLUDED.standardDeviation10, upper10 = EXCLUDED.upper10, lower10 = EXCLUDED.lower10, width10 = EXCLUDED.width10, position10 = EXCLUDED.position10, ema20Center = EXCLUDED.ema20Center, standardDeviation20 = EXCLUDED.standardDeviation20, upper20 = EXCLUDED.upper20, lower20 = EXCLUDED.lower20, width20 = EXCLUDED.width20, position20 = EXCLUDED.position20, width20Baseline = EXCLUDED.width20Baseline, width20Ratio = EXCLUDED.width20Ratio, isWarm = EXCLUDED.isWarm, sourceSequence = EXCLUDED.sourceSequence, calculatedAt = EXCLUDED.calculatedAt, schemaVersion = EXCLUDED.schemaVersion, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, isValid = EXCLUDED.isValid;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string InsertFuturesVwapSignal = """
        INSERT INTO market_data.futures_vwap_signal ( contractId, valueDate, configurationId, asOfUtc, lastTradeOrdinal, sessionStartUtc, sessionEndUtc, cumulativePriceVolume, cumulativeVolume, eligibleTradeCount, rejectedTradeCount, lastPrice, vwap, priceMinusVwap, priceToVwapPercent, lastTradeSourceSequence, streamEpochId, isWarm, isValid, invalidReason, isTickExact, calculationMethod, schemaVersion, calculationVersion ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24 ) ON CONFLICT (contractId, valueDate, configurationId, asOfUtc, lastTradeOrdinal) DO UPDATE SET sessionStartUtc = EXCLUDED.sessionStartUtc, sessionEndUtc = EXCLUDED.sessionEndUtc, cumulativePriceVolume = EXCLUDED.cumulativePriceVolume, cumulativeVolume = EXCLUDED.cumulativeVolume, eligibleTradeCount = EXCLUDED.eligibleTradeCount, rejectedTradeCount = EXCLUDED.rejectedTradeCount, lastPrice = EXCLUDED.lastPrice, vwap = EXCLUDED.vwap, priceMinusVwap = EXCLUDED.priceMinusVwap, priceToVwapPercent = EXCLUDED.priceToVwapPercent, lastTradeSourceSequence = EXCLUDED.lastTradeSourceSequence, streamEpochId = EXCLUDED.streamEpochId, isWarm = EXCLUDED.isWarm, isValid = EXCLUDED.isValid, invalidReason = EXCLUDED.invalidReason, isTickExact = EXCLUDED.isTickExact, calculationMethod = EXCLUDED.calculationMethod, schemaVersion = EXCLUDED.schemaVersion, calculationVersion = EXCLUDED.calculationVersion;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string GetLatestFuturesEmaSignal = """
        SELECT seriesKey AS "SeriesKey", timePeriod AS "TimePeriod", configurationId AS "ConfigurationId", marketDataAsOf AS "MarketDataAsOf", observationId AS "ObservationId", contractId AS "ContractId", valueDate AS "ValueDate", price AS "Price", ema10 AS "Ema10", previousEma10 AS "PreviousEma10", ema10Slope AS "Ema10Slope", ema20 AS "Ema20", previousEma20 AS "PreviousEma20", ema20Slope AS "Ema20Slope", ema50 AS "Ema50", previousEma50 AS "PreviousEma50", ema50Slope AS "Ema50Slope", ema200 AS "Ema200", previousEma200 AS "PreviousEma200", ema200Slope AS "Ema200Slope", isWarm AS "IsWarm", sourceSequence AS "SourceSequence", calculatedAt AS "CalculatedAt", schemaVersion AS "SchemaVersion", calculationVersion AS "CalculationVersion", calculationMethod AS "CalculationMethod", isValid AS "IsValid" FROM market_data.futures_ema_signal WHERE seriesKey = $1 AND timePeriod = $2 AND configurationId = $3 AND yearMonth = $4 AND marketDataAsOf <= $5 ORDER BY marketDataAsOf DESC, observationId ASC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string GetLatestFuturesBollingerBandSignal = """
        SELECT seriesKey AS "SeriesKey", timePeriod AS "TimePeriod", configurationId AS "ConfigurationId", marketDataAsOf AS "MarketDataAsOf", observationId AS "ObservationId", contractId AS "ContractId", valueDate AS "ValueDate", price AS "Price", ema10Center AS "Ema10Center", standardDeviation10 AS "StandardDeviation10", upper10 AS "Upper10", lower10 AS "Lower10", width10 AS "Width10", position10 AS "Position10", ema20Center AS "Ema20Center", standardDeviation20 AS "StandardDeviation20", upper20 AS "Upper20", lower20 AS "Lower20", width20 AS "Width20", position20 AS "Position20", width20Baseline AS "Width20Baseline", width20Ratio AS "Width20Ratio", isWarm AS "IsWarm", sourceSequence AS "SourceSequence", calculatedAt AS "CalculatedAt", schemaVersion AS "SchemaVersion", calculationVersion AS "CalculationVersion", calculationMethod AS "CalculationMethod", isValid AS "IsValid" FROM market_data.futures_bollinger_band_signal WHERE seriesKey = $1 AND timePeriod = $2 AND configurationId = $3 AND yearMonth = $4 AND marketDataAsOf <= $5 ORDER BY marketDataAsOf DESC, observationId ASC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string GetLatestFuturesVwapSignal = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", configurationId AS "ConfigurationId", sessionStartUtc AS "SessionStartUtc", sessionEndUtc AS "SessionEndUtc", asOfUtc AS "AsOfUtc", cumulativePriceVolume AS "CumulativePriceVolume", cumulativeVolume AS "CumulativeVolume", eligibleTradeCount AS "EligibleTradeCount", rejectedTradeCount AS "RejectedTradeCount", lastPrice AS "LastPrice", vwap AS "Vwap", priceMinusVwap AS "PriceMinusVwap", priceToVwapPercent AS "PriceToVwapPercent", lastTradeSourceSequence AS "LastTradeSourceSequence", streamEpochId AS "StreamEpochId", lastTradeOrdinal AS "LastTradeOrdinal", isWarm AS "IsWarm", isValid AS "IsValid", invalidReason AS "InvalidReason", isTickExact AS "IsTickExact", calculationMethod AS "CalculationMethod", schemaVersion AS "SchemaVersion", calculationVersion AS "CalculationVersion" FROM market_data.futures_vwap_signal WHERE contractId = $1 AND valueDate = $2 AND configurationId = $3 ORDER BY asOfUtc DESC, lastTradeOrdinal DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL indicator persistence with explicit keys and ordering.</summary>
    public const string GetFuturesVwapSignalHistory = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", configurationId AS "ConfigurationId", sessionStartUtc AS "SessionStartUtc", sessionEndUtc AS "SessionEndUtc", asOfUtc AS "AsOfUtc", cumulativePriceVolume AS "CumulativePriceVolume", cumulativeVolume AS "CumulativeVolume", eligibleTradeCount AS "EligibleTradeCount", rejectedTradeCount AS "RejectedTradeCount", lastPrice AS "LastPrice", vwap AS "Vwap", priceMinusVwap AS "PriceMinusVwap", priceToVwapPercent AS "PriceToVwapPercent", lastTradeSourceSequence AS "LastTradeSourceSequence", streamEpochId AS "StreamEpochId", lastTradeOrdinal AS "LastTradeOrdinal", isWarm AS "IsWarm", isValid AS "IsValid", invalidReason AS "InvalidReason", isTickExact AS "IsTickExact", calculationMethod AS "CalculationMethod", schemaVersion AS "SchemaVersion", calculationVersion AS "CalculationVersion" FROM market_data.futures_vwap_signal WHERE contractId = $1 AND valueDate = $2 AND configurationId = $3 ORDER BY asOfUtc DESC, lastTradeOrdinal DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteFuturesBarData = """
        DELETE FROM market_data.futures_bar_data WHERE contractId = $1 AND symbol = $2 AND valueDate = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesBarData = """
        SELECT contractId AS "ContractId", symbol AS "Symbol", valueDate AS "ValueDate", barDate AS "BarDate", barRateType AS "BarRateType", barValue AS "BarValue", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger" FROM market_data.futures_bar_data WHERE contractId = $1 AND symbol = $2 AND valueDate = $3 AND barDate >= $4 AND barDate <= $5 ORDER BY barDate DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesBarDataAll = """
        SELECT contractId AS "ContractId", symbol AS "Symbol", valueDate AS "ValueDate", barDate AS "BarDate", barRateType AS "BarRateType", barValue AS "BarValue", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger" FROM market_data.futures_bar_data ORDER BY contractId, symbol, valueDate, barDate DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesBarData = """
        SELECT contractId AS "ContractId", symbol AS "Symbol", valueDate AS "ValueDate", barDate AS "BarDate", barRateType AS "BarRateType", barValue AS "BarValue", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger" FROM market_data.futures_bar_data WHERE contractId = $1 AND symbol = $2 AND valueDate = $3 ORDER BY barDate DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesBarDataCount = """
        SELECT count(*) as "Value" FROM market_data.futures_bar_data WHERE contractId = $1 AND symbol = $2 AND valueDate = $3 GROUP BY contractId, symbol, valueDate;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesBarData = """
        INSERT INTO market_data.futures_bar_data ( contractId, symbol, valueDate, barDate, barRateType, barValue, upTrendTrigger, downTrendTrigger ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8 ) ON CONFLICT (contractId, symbol, valueDate, barDate) DO UPDATE SET barRateType = EXCLUDED.barRateType, barValue = EXCLUDED.barValue, upTrendTrigger = EXCLUDED.upTrendTrigger, downTrendTrigger = EXCLUDED.downTrendTrigger;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteFuturesClosingPrice = """
        DELETE FROM market_data.futures_closing_price WHERE contractId = $1 AND valueDate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesClosingPrice = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", closingPrice AS "ClosingPrice", createdOn AS "CreatedOn", createdBy AS "CreatedBy" FROM market_data.futures_closing_price WHERE contractId = $1 AND valueDate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetYesterdaysFuturesClosingPrice = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", closingPrice AS "ClosingPrice", createdOn AS "CreatedOn", createdBy AS "CreatedBy" FROM market_data.futures_closing_price WHERE contractId = $1 AND valueDate < $2 ORDER BY valueDate DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesClosingPrice = """
        INSERT INTO market_data.futures_closing_price ( contractId, valueDate, closingPrice, createdOn, createdBy ) VALUES ( $1, $2, $3, $4, $5 ) ON CONFLICT (contractId, valueDate) DO UPDATE SET closingPrice = EXCLUDED.closingPrice, createdOn = EXCLUDED.createdOn, createdBy = EXCLUDED.createdBy;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesEodClosingPrices = """
        SELECT symbol AS "Symbol", valueDate AS "ValueDate", closePrice AS "ClosingPrice" FROM market_data.futures_eod_data WHERE contractId = $1 AND valueDate >= $2 AND valueDate <= $3 ORDER BY valueDate DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesVxTermStructureSignal = """
        INSERT INTO market_data.futures_vx_term_structure_signal ( valueDate, configurationId, calculatedAt, frontSourceSequence, backSourceSequence, frontContractId, frontExpiry, frontPrice, backContractId, backExpiry, backPrice, frontBackSpread, frontBackRatio, termStructurePercent, termStructureState, priorFrontBackRatio, priorTermStructurePercent, frontSourceTimestamp, backSourceTimestamp, isWarm, isValid, schemaVersion, calculationVersion ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 ) ON CONFLICT (valueDate, configurationId, calculatedAt, frontSourceSequence, backSourceSequence) DO UPDATE SET frontContractId = EXCLUDED.frontContractId, frontExpiry = EXCLUDED.frontExpiry, frontPrice = EXCLUDED.frontPrice, backContractId = EXCLUDED.backContractId, backExpiry = EXCLUDED.backExpiry, backPrice = EXCLUDED.backPrice, frontBackSpread = EXCLUDED.frontBackSpread, frontBackRatio = EXCLUDED.frontBackRatio, termStructurePercent = EXCLUDED.termStructurePercent, termStructureState = EXCLUDED.termStructureState, priorFrontBackRatio = EXCLUDED.priorFrontBackRatio, priorTermStructurePercent = EXCLUDED.priorTermStructurePercent, frontSourceTimestamp = EXCLUDED.frontSourceTimestamp, backSourceTimestamp = EXCLUDED.backSourceTimestamp, isWarm = EXCLUDED.isWarm, isValid = EXCLUDED.isValid, schemaVersion = EXCLUDED.schemaVersion, calculationVersion = EXCLUDED.calculationVersion;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLatestFuturesVxTermStructureSignal = """
        SELECT valueDate AS "ValueDate", configurationId AS "ConfigurationId", frontContractId AS "FrontContractId", frontExpiry AS "FrontExpiry", frontPrice AS "FrontPrice", backContractId AS "BackContractId", backExpiry AS "BackExpiry", backPrice AS "BackPrice", frontBackSpread AS "FrontBackSpread", frontBackRatio AS "FrontBackRatio", termStructurePercent AS "TermStructurePercent", termStructureState AS "TermStructureState", priorFrontBackRatio AS "PriorFrontBackRatio", priorTermStructurePercent AS "PriorTermStructurePercent", frontSourceTimestamp AS "FrontSourceTimestamp", backSourceTimestamp AS "BackSourceTimestamp", frontSourceSequence AS "FrontSourceSequence", backSourceSequence AS "BackSourceSequence", calculatedAt AS "CalculatedAt", isWarm AS "IsWarm", isValid AS "IsValid", schemaVersion AS "SchemaVersion", calculationVersion AS "CalculationVersion" FROM market_data.futures_vx_term_structure_signal WHERE valueDate = $1 AND configurationId = $2 ORDER BY calculatedAt DESC, frontSourceSequence DESC, backSourceSequence DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteFuturesOptionTickPriceData = """
        DELETE FROM market_data.futures_option_tick_price_data WHERE contractId = $1 AND valueDate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesOptionTickPriceData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", tickId AS "TickId", tickTime AS "TickTime", optionPrice AS "OptionPrice", bidPrice AS "BidPrice", askPrice AS "AskPrice", bidSize AS "BidSize", askSize AS "AskSize", impliedVolatility AS "ImpliedVolatility", underlyingPrice AS "UnderlyingPrice", delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho" FROM market_data.futures_option_tick_price_data WHERE contractId = $1 AND valueDate = $2 AND tickId = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesOptionTickPriceData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", tickId AS "TickId", tickTime AS "TickTime", optionPrice AS "OptionPrice", bidPrice AS "BidPrice", askPrice AS "AskPrice", bidSize AS "BidSize", askSize AS "AskSize", impliedVolatility AS "ImpliedVolatility", underlyingPrice AS "UnderlyingPrice", delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho" FROM market_data.futures_option_tick_price_data WHERE contractId = $1 AND valueDate = $2 ORDER BY tickId DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesOptionTickPriceData = """
        INSERT INTO market_data.futures_option_tick_price_data ( contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16 ) ON CONFLICT (contractId, valueDate, tickId) DO UPDATE SET tickTime = EXCLUDED.tickTime, optionPrice = EXCLUDED.optionPrice, bidPrice = EXCLUDED.bidPrice, askPrice = EXCLUDED.askPrice, bidSize = EXCLUDED.bidSize, askSize = EXCLUDED.askSize, impliedVolatility = EXCLUDED.impliedVolatility, underlyingPrice = EXCLUDED.underlyingPrice, delta = EXCLUDED.delta, gamma = EXCLUDED.gamma, vega = EXCLUDED.vega, theta = EXCLUDED.theta, rho = EXCLUDED.rho;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesRsiSignal = """
        INSERT INTO market_data.futures_rsi_signal ( contractId, valueDate, timePeriod, periodLength, timestamp, price, priceChange, priceGain, priceLoss, averagePriceGain, averagePriceLoss, rs, rsi, rsiAverage, rsiSlope, sourceSequence, sourceEventTimestamp, configurationId, observationId, marketDataAsOf, calculationVersion, calculationMethod, schemaVersion, isValid, previousRsi, regimeSlope, isWarm ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27 ) ON CONFLICT (contractId, timePeriod, periodLength, valueDate, timestamp) DO UPDATE SET price = EXCLUDED.price, priceChange = EXCLUDED.priceChange, priceGain = EXCLUDED.priceGain, priceLoss = EXCLUDED.priceLoss, averagePriceGain = EXCLUDED.averagePriceGain, averagePriceLoss = EXCLUDED.averagePriceLoss, rs = EXCLUDED.rs, rsi = EXCLUDED.rsi, rsiAverage = EXCLUDED.rsiAverage, rsiSlope = EXCLUDED.rsiSlope, sourceSequence = EXCLUDED.sourceSequence, sourceEventTimestamp = EXCLUDED.sourceEventTimestamp, configurationId = EXCLUDED.configurationId, observationId = EXCLUDED.observationId, marketDataAsOf = EXCLUDED.marketDataAsOf, calculationVersion = EXCLUDED.calculationVersion, calculationMethod = EXCLUDED.calculationMethod, schemaVersion = EXCLUDED.schemaVersion, isValid = EXCLUDED.isValid, previousRsi = EXCLUDED.previousRsi, regimeSlope = EXCLUDED.regimeSlope, isWarm = EXCLUDED.isWarm;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesRsiSignal = """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timePeriod AS "TimePeriod", periodLength AS "PeriodLength", timestamp AS "Timestamp", price AS "FuturesPrice", pricechange AS "PriceChange", pricegain AS "PriceGain", priceloss AS "PriceLoss", averagepricegain AS "AveragePriceGain", averagepriceloss AS "AveragePriceLoss", rs AS "RS", rsi AS "RSI", rsiaverage AS "RSIAverage", rsislope AS "RSISlope", sourceSequence AS "SourceSequence", sourceEventTimestamp AS "SourceEventTimestamp", configurationId AS "ConfigurationId", observationId AS "ObservationId", marketDataAsOf AS "MarketDataAsOf", calculationVersion AS "CalculationVersion", calculationMethod AS "CalculationMethod", schemaVersion AS "SchemaVersion", isValid AS "IsValid", previousRsi AS "PreviousRsi", regimeSlope AS "RegimeSlope", isWarm AS "IsWarm" FROM market_data.futures_rsi_signal WHERE contractid = $1 AND timePeriod = $2 AND periodLength = $3 AND valuedate = $4 ORDER BY timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesRsiSignalsForTrend = """
        SELECT rsi AS "RSI" FROM market_data.futures_rsi_signal WHERE contractid = $1 AND timeperiod = $2 AND periodlength = $3 AND valuedate = $4 AND timestamp >= $5 AND timestamp <= $6 ORDER BY timestamp DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string UpsertMarketOutlookSnapshot = """
        INSERT INTO market_data.market_outlook_snapshot (contractId, valueDate, revision, updatedOn, eodData, futuresTradeSignal, missingInputs, snapshot) VALUES ($1, $2, $3, $4, $5, $6, $7, $8) ON CONFLICT (contractId, valueDate) DO UPDATE SET revision = EXCLUDED.revision, updatedOn = EXCLUDED.updatedOn, eodData = EXCLUDED.eodData, futuresTradeSignal = EXCLUDED.futuresTradeSignal, missingInputs = EXCLUDED.missingInputs, snapshot = EXCLUDED.snapshot;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetMarketOutlookSnapshot = """
        SELECT snapshot AS "Snapshot", contractId AS "ContractId", valueDate AS "ValueDate", revision AS "Revision", updatedOn AS "UpdatedOn", eodData AS "EodData", futuresTradeSignal AS "FuturesTradeSignal", missingInputs AS "MissingInputs" FROM market_data.market_outlook_snapshot WHERE contractId = $1 AND valueDate <= $2 ORDER BY valueDate DESC LIMIT 1;
        """;

    public const string GetOptionTradeEvidence = "SELECT source_digest,payload FROM market_data.option_trade_evidence WHERE contract_id=$1 AND value_date=$2 AND source_id=$3;";
    public const string InsertOptionTradeEvidence = "INSERT INTO market_data.option_trade_evidence(contract_id,value_date,source_id,source_digest,payload) VALUES($1,$2,$3,$4,$5) ON CONFLICT (contract_id,value_date,source_id) DO NOTHING;";
    public const string GetCompositionPreparation = "SELECT payload FROM market_data.composition_preparation WHERE workflow_id=$1 AND input_revision=$2;";
    public const string InsertCompositionPreparation = "INSERT INTO market_data.composition_preparation(workflow_id,input_revision,payload) VALUES($1,$2,$3) ON CONFLICT (workflow_id,input_revision) DO NOTHING;";

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetMarketHolidays = """
        SELECT currencyType AS "CurrencyType", holidayDate AS "HolidayDate", description AS "Description" FROM market_data.market_holiday WHERE currencyType = $1 ORDER BY holidayDate ASC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetMarketHolidaysByDateRange = """
        SELECT currencyType AS "CurrencyType", holidayDate AS "HolidayDate", description AS "Description" FROM market_data.market_holiday WHERE currencyType = $1 AND holidayDate >= $2 AND holidayDate <= $3 ORDER BY holidayDate ASC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertMarketHoliday = """
        INSERT INTO market_data.market_holiday (currencyType, holidayDate, description) VALUES ($1, $2, $3) ON CONFLICT (currencyType, holidayDate) DO UPDATE SET description = EXCLUDED.description;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteMarketHoliday = """
        DELETE FROM market_data.market_holiday WHERE currencyType = $1 AND holidayDate = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteMarketHolidays = """
        DELETE FROM market_data.market_holiday WHERE currencyType = $1;
        """;

    public const string GetNormalCurveData = "SELECT StdDevIndex,Percent FROM market_data.normal_curve_data ORDER BY StdDevIndex;";

    public const string TryInsertObservation = """
        WITH accepted AS (INSERT INTO market_data.futures_trade_session_bar ( seriesKey, timePeriod, yearMonth, marketDataAsOf, observationId, contractId, valueDate, intervalStart, intervalEnd, openPrice, highPrice, lowPrice, closePrice, volume, tradeCount, priceVolumeSum, firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent, calculatedAt, schemaVersion, calculationVersion, isComplete, isValid) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25) ON CONFLICT (seriesKey, timePeriod, yearMonth, marketDataAsOf, observationId) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM accepted);
        """;

    public const string TryInsertRawEod = """
        WITH accepted AS (INSERT INTO market_data.futures_eod_observation ( seriesKey, yearMonth, valueDate, contractId, sessionStart, sessionEnd, openPrice, highPrice, lowPrice, closePrice, volume, tradeCount, priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21) ON CONFLICT (seriesKey, yearMonth, valueDate, contractId) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM accepted);
        """;

    public const string GetRawEod = """
        SELECT seriesKey, contractId, valueDate, sessionStart, sessionEnd, openPrice, highPrice, lowPrice, closePrice, volume, tradeCount, priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid FROM market_data.futures_eod_observation WHERE seriesKey = $1 AND yearMonth = $2 AND valueDate = $3 ORDER BY valueDate,contractId;
        """;

    public const string GetRawEodRange = """
        SELECT seriesKey, contractId, valueDate, sessionStart, sessionEnd, openPrice, highPrice, lowPrice, closePrice, volume, tradeCount, priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid FROM market_data.futures_eod_observation WHERE seriesKey = $1 AND yearMonth = $2 AND valueDate >= $3 AND valueDate <= $4 ORDER BY valueDate,contractId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesRsiDailySignal = """
        SELECT contractid AS "ContractId", valuedate AS "ValueDate", timePeriod AS "TimePeriod", periodLength AS "PeriodLength", timestamp AS "Timestamp", price AS "FuturesPrice", pricechange AS "PriceChange", pricegain AS "PriceGain", priceloss AS "PriceLoss", averagepricegain AS "AveragePriceGain", averagepriceloss AS "AveragePriceLoss", rs AS "RS", rsi AS "RSI", rsiaverage AS "RSIAverage", rsislope AS "RSISlope", sourceSequence AS "SourceSequence", sourceEventTimestamp AS "SourceEventTimestamp", configurationId AS "ConfigurationId", observationId AS "ObservationId", marketDataAsOf AS "MarketDataAsOf", calculationVersion AS "CalculationVersion", calculationMethod AS "CalculationMethod", schemaVersion AS "SchemaVersion", isValid AS "IsValid", previousRsi AS "PreviousRsi", regimeSlope AS "RegimeSlope", isWarm AS "IsWarm" FROM market_data.futures_rsi_signal WHERE contractid = $1 AND timePeriod = $2 AND periodLength = $3 ORDER BY valueDate DESC, timestamp DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertTradeLiveFeed = """
        INSERT INTO market_data.trade_live_feed ( OrderId, TradeId, TradeLiveFeedState ) VALUES ( $1, $2, $3 ) ON CONFLICT (orderId, tradeId) DO UPDATE SET OrderId = EXCLUDED.OrderId, TradeId = EXCLUDED.TradeId, TradeLiveFeedState = EXCLUDED.TradeLiveFeedState;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetTradeLiveFeed = """
        SELECT OrderId, TradeId, TradeLiveFeedState FROM market_data.trade_live_feed WHERE orderId = $1 AND tradeId = $2;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteTradeLiveFeed = """
        DELETE FROM market_data.trade_live_feed WHERE orderId = $1 AND tradeId = $2;
        """;

    public const string DownloadLogInsert = """
        INSERT INTO market_data.market_data_download_log (dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19) ON CONFLICT (dataset,provider,scope,value_date,requested_at_utc,import_command_id) DO UPDATE SET log_command_id=EXCLUDED.log_command_id,source_terminal_event_id=EXCLUDED.source_terminal_event_id,schema_version=EXCLUDED.schema_version,status=EXCLUDED.status,started_at_utc=EXCLUDED.started_at_utc,finished_at_utc=EXCLUDED.finished_at_utc,elapsed_milliseconds=EXCLUDED.elapsed_milliseconds,downloaded_record_count=EXCLUDED.downloaded_record_count,persisted_record_count=EXCLUDED.persisted_record_count,error_code=EXCLUDED.error_code,error_message=EXCLUDED.error_message,payload_sha256=EXCLUDED.payload_sha256,projected_at_utc=EXCLUDED.projected_at_utc;
        """;

    public const string DownloadLogExact = """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc FROM market_data.market_data_download_log WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4 AND requested_at_utc=$5 AND import_command_id=$6 LIMIT 1;
        """;

    public const string DownloadLogHistory = """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc FROM market_data.market_data_download_log WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4 ORDER BY requested_at_utc DESC,import_command_id DESC LIMIT $5;
        """;

    public const string DownloadLogHistoryAfter = """
        SELECT dataset, provider, scope, value_date, requested_at_utc, import_command_id, log_command_id, source_terminal_event_id, schema_version, status, started_at_utc, finished_at_utc, elapsed_milliseconds, downloaded_record_count, persisted_record_count, error_code, error_message, payload_sha256, projected_at_utc FROM market_data.market_data_download_log WHERE dataset = $1 AND provider = $2 AND scope = $3 AND value_date = $4 AND (requested_at_utc,import_command_id)<($5,$6) ORDER BY requested_at_utc DESC,import_command_id DESC LIMIT $7;
        """;

    public const string UpsertFuturesOptionChainQuoteData = """
        INSERT INTO market_data.futures_option_chain_quote_data ( underlyingContractId, expiryDate, valueDate, contractId, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18 ) ON CONFLICT (underlyingContractId,expiryDate,valueDate,contractId) DO UPDATE SET tickId=EXCLUDED.tickId,tickTime=EXCLUDED.tickTime,optionPrice=EXCLUDED.optionPrice,bidPrice=EXCLUDED.bidPrice,askPrice=EXCLUDED.askPrice,bidSize=EXCLUDED.bidSize,askSize=EXCLUDED.askSize,impliedVolatility=EXCLUDED.impliedVolatility,underlyingPrice=EXCLUDED.underlyingPrice,delta=EXCLUDED.delta,gamma=EXCLUDED.gamma,vega=EXCLUDED.vega,theta=EXCLUDED.theta,rho=EXCLUDED.rho;
        """;

    public const string UpdateOptionChainGreeks = """
        UPDATE market_data.futures_option_chain_quote_data SET impliedVolatility = $1, underlyingPrice = $2, delta = $3, gamma = $4, vega = $5, theta = $6, rho = $7 WHERE underlyingContractId = $8 AND expiryDate = $9 AND valueDate = $10 AND contractId = $11 AND tickId = $12;
        """;

    public const string GetOptionChainStatistics = """
        WITH scope AS (
            SELECT contractId, valueDate, volume, volumeValueDate, openInterest, openInterestValueDate
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingContractId = $1 AND expiryDate = $2 AND valueDate <= $3
        ), volumes AS (
            SELECT DISTINCT ON (contractId) contractId, volume, volumeValueDate FROM scope
            WHERE volume IS NOT NULL AND volume >= 0 AND volumeValueDate IS NOT NULL AND volumeValueDate <= $3
            ORDER BY contractId, volumeValueDate DESC, valueDate DESC
        ), interest AS (
            SELECT DISTINCT ON (contractId) contractId, openInterest, openInterestValueDate FROM scope
            WHERE openInterest IS NOT NULL AND openInterest >= 0 AND openInterestValueDate IS NOT NULL AND openInterestValueDate <= $3
            ORDER BY contractId, openInterestValueDate DESC, valueDate DESC
        )
        SELECT COALESCE(v.contractId, i.contractId), v.volume, v.volumeValueDate, i.openInterest, i.openInterestValueDate
        FROM volumes v FULL OUTER JOIN interest i ON v.contractId = i.contractId;
        """;

    public const string GetFuturesOptionChainQuoteData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", tickId AS "TickId", tickTime AS "TickTime", optionPrice AS "OptionPrice", bidPrice AS "BidPrice", askPrice AS "AskPrice", bidSize AS "BidSize", askSize AS "AskSize", impliedVolatility AS "ImpliedVolatility", underlyingPrice AS "UnderlyingPrice", delta AS "Delta", gamma AS "Gamma", vega AS "Vega", theta AS "Theta", rho AS "Rho", volume AS "Volume", openInterest AS "OpenInterest", volumeValueDate AS "VolumeValueDate", openInterestValueDate AS "OpenInterestValueDate" FROM market_data.futures_option_chain_quote_data WHERE underlyingContractId = $1 AND expiryDate = $2 AND valueDate = $3 ORDER BY contractId;
        """;

    public const string InsertRateOfReturn = """
        INSERT INTO market_data.rate_of_return (symbol, valueDate, rateOfReturn) VALUES ($1, $2, $3) ON CONFLICT (symbol,valueDate) DO UPDATE SET rateOfReturn=EXCLUDED.rateOfReturn;
        """;

    public const string GetLastRateOfReturn = """
        SELECT symbol AS "Symbol", valueDate AS "ValueDate", rateOfReturn AS "RateOfReturn" FROM market_data.rate_of_return WHERE symbol = $1 ORDER BY valueDate DESC LIMIT 1;
        """;

    public const string DeleteRateOfReturn = """
        DELETE FROM market_data.rate_of_return WHERE symbol = $1 AND valueDate = $2;
        """;

    public const string InsertVixFuturesEodData = """
        INSERT INTO market_data.vix_futures_eod_data (contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume) VALUES ($1, $2, $3, $4, $5, $6, $7) ON CONFLICT(contractId,valueDate) DO UPDATE SET openPrice=EXCLUDED.openPrice,highPrice=EXCLUDED.highPrice,lowPrice=EXCLUDED.lowPrice,closePrice=EXCLUDED.closePrice,volume=EXCLUDED.volume;
        """;

    public const string UpdateVixFuturesEodData = """
        UPDATE market_data.vix_futures_eod_data SET openPrice = $1, highPrice = $2, lowPrice = $3, closePrice = $4, volume = $5 WHERE contractId = $6 AND valueDate = $7;
        """;

    public const string GetLastVixFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume" FROM market_data.vix_futures_eod_data WHERE contractId = $1 AND valueDate <= $2 ORDER BY valueDate DESC LIMIT 1;
        """;

    public const string GetVixFuturesEodData = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume" FROM market_data.vix_futures_eod_data WHERE contractId = $1 AND valueDate = $2 LIMIT 1;
        """;

    public const string DeleteVixFuturesEodData = """
        DELETE FROM market_data.vix_futures_eod_data WHERE contractId = $1 AND valueDate = $2;
        """;

    public const string GetVixFuturesEodDataByValueDate = """
        SELECT contractId,valueDate,openPrice,highPrice,lowPrice,closePrice,volume FROM market_data.vix_futures_eod_data WHERE valueDate=$1 ORDER BY contractId;
        """;

    public const string InsertYieldCurveRate = """
        INSERT INTO market_data.yield_curve_rates ( id, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14 ) ON CONFLICT(id,valueDate) DO UPDATE SET oneMonth=EXCLUDED.oneMonth,twoMonth=EXCLUDED.twoMonth,threeMonth=EXCLUDED.threeMonth,sixMonth=EXCLUDED.sixMonth,oneYear=EXCLUDED.oneYear,twoYear=EXCLUDED.twoYear,threeYear=EXCLUDED.threeYear,fiveYear=EXCLUDED.fiveYear,sevenYear=EXCLUDED.sevenYear,tenYear=EXCLUDED.tenYear,twentyYear=EXCLUDED.twentyYear,thirtyYear=EXCLUDED.thirtyYear;
        """;

    public const string InsertYieldCurveRateByDate = """
        INSERT INTO market_data.yield_curve_rate_by_date ( lookupId, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14 ) ON CONFLICT(lookupId,valueDate) DO UPDATE SET oneMonth=EXCLUDED.oneMonth,twoMonth=EXCLUDED.twoMonth,threeMonth=EXCLUDED.threeMonth,sixMonth=EXCLUDED.sixMonth,oneYear=EXCLUDED.oneYear,twoYear=EXCLUDED.twoYear,threeYear=EXCLUDED.threeYear,fiveYear=EXCLUDED.fiveYear,sevenYear=EXCLUDED.sevenYear,tenYear=EXCLUDED.tenYear,twentyYear=EXCLUDED.twentyYear,thirtyYear=EXCLUDED.thirtyYear;
        """;

    public const string InsertYieldCurveRateYear = """
        INSERT INTO market_data.yield_curve_rate_year (lookupId, rateYear) VALUES ($1, $2) ON CONFLICT(lookupId,rateYear) DO NOTHING;
        """;

    public const string DeleteYieldCurveRate = """
        DELETE FROM market_data.yield_curve_rates WHERE id = 1 AND valueDate = $1;
        """;

    public const string DeleteYieldCurveRateByDate = """
        DELETE FROM market_data.yield_curve_rate_by_date WHERE lookupId = 1 AND valueDate = $1;
        """;

    public const string GetYieldCurveRate = """
        SELECT valueDate AS "ValueDate", oneMonth AS "OneMonth", twoMonth AS "TwoMonth", threeMonth AS "ThreeMonth", sixMonth AS "SixMonth", oneYear AS "OneYear", twoYear AS "TwoYear", threeYear AS "ThreeYear", fiveYear AS "FiveYear", sevenYear AS "SevenYear", tenYear AS "TenYear", twentyYear AS "TwentyYear", thirtyYear AS "ThirtyYear" FROM market_data.yield_curve_rate_by_date WHERE lookupId = 1 AND valueDate = $1;
        """;

    public const string GetLastYieldCurveRate = """
        SELECT valueDate AS "ValueDate", oneMonth AS "OneMonth", twoMonth AS "TwoMonth", threeMonth AS "ThreeMonth", sixMonth AS "SixMonth", oneYear AS "OneYear", twoYear AS "TwoYear", threeYear AS "ThreeYear", fiveYear AS "FiveYear", sevenYear AS "SevenYear", tenYear AS "TenYear", twentyYear AS "TwentyYear", thirtyYear AS "ThirtyYear" FROM market_data.yield_curve_rate_by_date WHERE lookupId = 1 ORDER BY valueDate DESC LIMIT 1;
        """;

    public const string GetYieldCurveRateYears = """
        SELECT rateYear AS "RateYear" FROM market_data.yield_curve_rate_year WHERE lookupId = $1 ORDER BY rateYear DESC LIMIT 200;
        """;

    public const string GetYieldCurveRates = """
        SELECT valueDate AS "ValueDate", oneMonth AS "OneMonth", twoMonth AS "TwoMonth", threeMonth AS "ThreeMonth", sixMonth AS "SixMonth", oneYear AS "OneYear", twoYear AS "TwoYear", threeYear AS "ThreeYear", fiveYear AS "FiveYear", sevenYear AS "SevenYear", tenYear AS "TenYear", twentyYear AS "TwentyYear", thirtyYear AS "ThirtyYear" FROM market_data.yield_curve_rate_by_date WHERE lookupId = 1 AND valueDate >= $1 AND valueDate <= $2 ORDER BY valueDate DESC LIMIT 5000;
        """;

    public const string GetCurrentFuturesEodDataByDateRange = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", symbol AS "Symbol", openPrice AS "OpenPrice", highPrice AS "HighPrice", lowPrice AS "LowPrice", closePrice AS "ClosePrice", volume AS "Volume", dailyPercentChange AS "DailyPercentChange", dailyStdDev AS "DailyStdDev", dailyStdDevAmount AS "DailyStdDevAmount", upperBand AS "UpperBand", mean AS "Mean", lowerBand AS "LowerBand", marketDirection AS "MarketDirection", marketVolatility AS "MarketVolatility", priceDirection AS "PriceDirection", priceVolatility AS "PriceVolatility", marketDirectionIndicator AS "MarketDirectionIndicator", windowSize AS "WindowSize", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA" FROM market_data.futures_eod_data WHERE valueDate >= $1 AND valueDate <= $2 ORDER BY valueDate DESC,contractId,symbol;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesTradeSignal = """
        INSERT INTO market_data.futures_trade_signal ( contractId, valueDate, timePeriod, sequenceId, timestamp, mean, stdDev, futuresPrice, priceChangePercent, fundRiskPercent, rsi, rsiSlope, trendType, trendStrength, tradeSignal, tdi, tdiStrength, mdi, mdiTrend, mdiUpTrendLimit, mdiDownTrendLimit, upTrendingTrigger, downTrendingTrigger, entryTrigger, exitTrigger, trendDelta, trendExtreme, trendReversal, fiftyDMA, twoHundredDMA, tradeExecuteState ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28, $29, $30, $31 ) ON CONFLICT (contractId, valueDate, timePeriod, timestamp, sequenceId) DO UPDATE SET mean = EXCLUDED.mean, stdDev = EXCLUDED.stdDev, futuresPrice = EXCLUDED.futuresPrice, priceChangePercent = EXCLUDED.priceChangePercent, fundRiskPercent = EXCLUDED.fundRiskPercent, rsi = EXCLUDED.rsi, rsiSlope = EXCLUDED.rsiSlope, trendType = EXCLUDED.trendType, trendStrength = EXCLUDED.trendStrength, tradeSignal = EXCLUDED.tradeSignal, tdi = EXCLUDED.tdi, tdiStrength = EXCLUDED.tdiStrength, mdi = EXCLUDED.mdi, mdiTrend = EXCLUDED.mdiTrend, mdiUpTrendLimit = EXCLUDED.mdiUpTrendLimit, mdiDownTrendLimit = EXCLUDED.mdiDownTrendLimit, upTrendingTrigger = EXCLUDED.upTrendingTrigger, downTrendingTrigger = EXCLUDED.downTrendingTrigger, entryTrigger = EXCLUDED.entryTrigger, exitTrigger = EXCLUDED.exitTrigger, trendDelta = EXCLUDED.trendDelta, trendExtreme = EXCLUDED.trendExtreme, trendReversal = EXCLUDED.trendReversal, fiftyDMA = EXCLUDED.fiftyDMA, twoHundredDMA = EXCLUDED.twoHundredDMA, tradeExecuteState = EXCLUDED.tradeExecuteState;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesTradeSignalIndex = """
        INSERT INTO market_data.futures_trade_signal_lookup_by_scope ( scope, entryId, sequenceId, contractId, valueDate, timePeriod ) VALUES ( $1, $2, $3, $4, $5, $6 ) ON CONFLICT (scope, entryId) DO UPDATE SET sequenceId = EXCLUDED.sequenceId, contractId = EXCLUDED.contractId, valueDate = EXCLUDED.valueDate, timePeriod = EXCLUDED.timePeriod WHERE market_data.futures_trade_signal_lookup_by_scope.sequenceId <= EXCLUDED.sequenceId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesTradeSignalById = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stdDev AS "StdDev", futuresPrice AS "FuturesPrice", priceChangePercent AS "PriceChangePercent", fundRiskPercent AS "FundRiskPercent", rsi AS "RSI", rsiSlope AS "RSISlope", trendType AS "TrendType", trendStrength AS "TrendStrength", tradeSignal AS "TradeSignal", tdi AS "TDI", tdiStrength AS "TDIStrength", mdi AS "MDI", mdiTrend AS "MDITrend", mdiUpTrendLimit AS "MDIUpTrendLimit", mdiDownTrendLimit AS "MDIDownTrendLimit", upTrendingTrigger AS "UpTrendingTrigger", downTrendingTrigger AS "DownTrendingTrigger", entryTrigger AS "EntryTrigger", exitTrigger AS "ExitTrigger", trendDelta AS "TrendDelta", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA", tradeExecuteState AS "TradeExecuteState" FROM market_data.futures_trade_signal WHERE contractId = $1 AND valueDate = $2 AND timePeriod = $3 ORDER BY sequenceId DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesTradeSignal = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId" FROM market_data.futures_trade_signal_lookup_by_scope WHERE scope = $1 ORDER BY sequenceId DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesTradeSignalAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stdDev AS "StdDev", futuresPrice AS "FuturesPrice", priceChangePercent AS "PriceChangePercent", fundRiskPercent AS "FundRiskPercent", rsi AS "RSI", rsiSlope AS "RSISlope", trendType AS "TrendType", trendStrength AS "TrendStrength", tradeSignal AS "TradeSignal", tdi AS "TDI", tdiStrength AS "TDIStrength", mdi AS "MDI", mdiTrend AS "MDITrend", mdiUpTrendLimit AS "MDIUpTrendLimit", mdiDownTrendLimit AS "MDIDownTrendLimit", upTrendingTrigger AS "UpTrendingTrigger", downTrendingTrigger AS "DownTrendingTrigger", entryTrigger AS "EntryTrigger", exitTrigger AS "ExitTrigger", trendDelta AS "TrendDelta", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA", tradeExecuteState AS "TradeExecuteState" FROM market_data.futures_trade_signal ORDER BY valueDate DESC, sequenceId DESC;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesTradeSignalBySymbol = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", timestamp AS "Timestamp", mean AS "Mean", stdDev AS "StdDev", futuresPrice AS "FuturesPrice", priceChangePercent AS "PriceChangePercent", fundRiskPercent AS "FundRiskPercent", rsi AS "RSI", rsiSlope AS "RSISlope", trendType AS "TrendType", trendStrength AS "TrendStrength", tradeSignal AS "TradeSignal", tdi AS "TDI", tdiStrength AS "TDIStrength", mdi AS "MDI", mdiTrend AS "MDITrend", mdiUpTrendLimit AS "MDIUpTrendLimit", mdiDownTrendLimit AS "MDIDownTrendLimit", upTrendingTrigger AS "UpTrendingTrigger", downTrendingTrigger AS "DownTrendingTrigger", entryTrigger AS "EntryTrigger", exitTrigger AS "ExitTrigger", trendDelta AS "TrendDelta", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", fiftyDMA AS "FiftyDMA", twoHundredDMA AS "TwoHundredDMA", tradeExecuteState AS "TradeExecuteState" FROM market_data.futures_trade_signal WHERE contractId = ANY($1) AND valueDate = $2 ORDER BY sequenceId DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesTradeSignalIdByValueDate = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId" FROM market_data.futures_trade_signal_lookup_by_scope WHERE scope = $1 ORDER BY sequenceId DESC, contractId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesTradeSignalQuarantine = """
        INSERT INTO market_data.futures_trade_signal_quarantine ( fingerprint, sourcePayload, reason, quarantinedOn ) VALUES ( $1, $2, $3, $4 ) ON CONFLICT (fingerprint) DO UPDATE SET sourcePayload = EXCLUDED.sourcePayload, reason = EXCLUDED.reason, quarantinedOn = EXCLUDED.quarantinedOn;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertFuturesItiSignal = """
        INSERT INTO market_data.futures_iti_signal ( contractId, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandLevel, reversalLevel ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23 ) ON CONFLICT (contractId, valueDate, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId, sequenceId) DO UPDATE SET intrinsicTime = EXCLUDED.intrinsicTime, intrinsicTimeLength = EXCLUDED.intrinsicTimeLength, intrinsicPrice = EXCLUDED.intrinsicPrice, trendPrice = EXCLUDED.trendPrice, trendExtreme = EXCLUDED.trendExtreme, trendReversal = EXCLUDED.trendReversal, trendDelta = EXCLUDED.trendDelta, targetDelta = EXCLUDED.targetDelta, lambda = EXCLUDED.lambda, tradingDays = EXCLUDED.tradingDays, threshold = EXCLUDED.threshold, upTrendTrigger = EXCLUDED.upTrendTrigger, downTrendTrigger = EXCLUDED.downTrendTrigger, tradeState = EXCLUDED.tradeState, bandLevel = EXCLUDED.bandLevel, reversalLevel = EXCLUDED.reversalLevel;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string UpsertFuturesItiTimeFrameState = """
        INSERT INTO market_data.futures_iti_timeframe_state ( contractId, timePeriod, calendarBucketStart, timeFrameStartValueDate, valueDate, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandAnchorPrice, bandPercentage, bandSize, bandLevel, reversalLevel ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $23, $24, $25, $26, $27, $28 ) ON CONFLICT (contractId, timePeriod, calendarBucketStart) DO UPDATE SET timeFrameStartValueDate = EXCLUDED.timeFrameStartValueDate, valueDate = EXCLUDED.valueDate, sequenceId = EXCLUDED.sequenceId, intrinsicTime = EXCLUDED.intrinsicTime, intrinsicTimeGroupId = EXCLUDED.intrinsicTimeGroupId, intrinsicTimeLength = EXCLUDED.intrinsicTimeLength, intrinsicPrice = EXCLUDED.intrinsicPrice, intrinsicTimeTrend = EXCLUDED.intrinsicTimeTrend, intrinsicTimeMode = EXCLUDED.intrinsicTimeMode, trendPrice = EXCLUDED.trendPrice, trendExtreme = EXCLUDED.trendExtreme, trendReversal = EXCLUDED.trendReversal, trendDelta = EXCLUDED.trendDelta, targetDelta = EXCLUDED.targetDelta, lambda = EXCLUDED.lambda, tradingDays = EXCLUDED.tradingDays, threshold = EXCLUDED.threshold, upTrendTrigger = EXCLUDED.upTrendTrigger, downTrendTrigger = EXCLUDED.downTrendTrigger, tradeState = EXCLUDED.tradeState, bandAnchorPrice = EXCLUDED.bandAnchorPrice, bandPercentage = EXCLUDED.bandPercentage, bandSize = EXCLUDED.bandSize, bandLevel = EXCLUDED.bandLevel, reversalLevel = EXCLUDED.reversalLevel WHERE market_data.futures_iti_timeframe_state.sequenceId <= EXCLUDED.sequenceId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesItiTimeFrameState = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", timeFrameStartValueDate AS "TimeFrameStartValueDate", bandAnchorPrice AS "BandAnchorPrice", bandPercentage AS "BandPercentage", bandSize AS "BandSize", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_timeframe_state WHERE contractId = $1 AND timePeriod = $2 AND calendarBucketStart = $3;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesItiSignals = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate = $2 AND timePeriod = $3 ORDER BY valueDate DESC, sequenceId DESC, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesItiSignalsCanonicalByContract = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 ORDER BY valueDate DESC, sequenceId DESC, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesItiSignalsCanonicalByContractDay = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate = $2 ORDER BY valueDate DESC, sequenceId DESC, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetFuturesItiSignalContractIdsByDate = """
        SELECT DISTINCT contractId AS "ContractId" FROM market_data.futures_iti_signal WHERE valueDate = $1 ORDER BY contractId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteFuturesItiSignal = """
        DELETE FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate = $2 AND timePeriod = $3;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    public const string GetNativeItiRange = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = ANY($1) AND valueDate >= $2 AND valueDate <= $3 ORDER BY valueDate, sequenceId, contractId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    public const string GetNativeItiDayMode = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate = $2 AND intrinsicTimeMode = $3 AND sequenceId > $4 ORDER BY sequenceId DESC, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId;
        """;

    /// <summary>Indexed canonical ITI observation query.</summary>
    public const string GetNativeItiLatestTrendMode = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate <= $2 AND intrinsicTimeTrend = $3 AND intrinsicTimeMode = $4 ORDER BY valueDate DESC, sequenceId DESC, timePeriod, intrinsicTimeGroupId LIMIT 1;
        """;

    /// <summary>Deletes a timeframe state only when it represents the removed value date.</summary>
    public const string DeleteNativeItiTimeFrameState = """
        DELETE FROM market_data.futures_iti_timeframe_state WHERE contractId = $1 AND timePeriod = $2 AND calendarBucketStart = $3 AND valueDate = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetLastFuturesItiSignalByTimePeriod = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate", timePeriod AS "TimePeriod", sequenceId AS "SequenceId", intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId", intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice", intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode", trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme", trendReversal AS "TrendReversal", trendDelta AS "TrendDelta", targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays", threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger", downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState", bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel" FROM market_data.futures_iti_signal WHERE contractId = $1 AND valueDate = $2 AND timePeriod = $3 ORDER BY sequenceId DESC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetEconomicCalendarV2ById = """
        SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy" FROM market_data.economic_calendar WHERE countryCode = $1 AND monthBucket = $2 AND eventDate >= date_trunc('second', $3::timestamptz) AND eventDate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventName = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetEconomicCalendars = """
        SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy" FROM market_data.economic_calendar WHERE countryCode = $1 AND monthBucket = $2 AND eventDate >= $3 AND eventDate <= $4 ORDER BY eventDate DESC, eventName COLLATE "C" LIMIT 2501;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetEconomicCalendarCountryCodes = """
        SELECT countryCode AS "CountryCode" FROM market_data.economic_calendar_country_code WHERE lookupId = $1 ORDER BY countryCode COLLATE "C" LIMIT 512;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string DeleteEconomicCalendarV2 = """
        DELETE FROM market_data.economic_calendar WHERE countryCode = $1 AND monthBucket = $2 AND eventDate >= date_trunc('second', $3::timestamptz) AND eventDate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventName = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertEconomicCalendarV2 = """
        INSERT INTO market_data.economic_calendar (countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, impact, unit, change, changePercentage, createdOn, createdBy, commandId) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14) ON CONFLICT (countryCode, monthBucket, eventDate, eventName) DO UPDATE SET actual = EXCLUDED.actual, forecast = EXCLUDED.forecast, prior = EXCLUDED.prior, impact = EXCLUDED.impact, unit = EXCLUDED.unit, change = EXCLUDED.change, changePercentage = EXCLUDED.changePercentage, createdOn = EXCLUDED.createdOn, createdBy = EXCLUDED.createdBy, commandId = EXCLUDED.commandId;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string GetEconomicCalendarV2CommandId = """
        SELECT commandId AS "CommandId" FROM market_data.economic_calendar WHERE countryCode = $1 AND monthBucket = $2 AND eventDate >= date_trunc('second', $3::timestamptz) AND eventDate < date_trunc('second', $3::timestamptz) + INTERVAL '1 second' AND eventName = $4;
        """;

    /// <summary>Native PostgreSQL market observation persistence.</summary>
    public const string InsertEconomicCalendarCountryCode = """
        INSERT INTO market_data.economic_calendar_country_code (lookupId, countryCode) VALUES ($1, $2) ON CONFLICT (lookupId, countryCode) DO NOTHING;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string InsertObservationById = """
        INSERT INTO market_data.option_iv_observation_by_id(environment,observation_id,payload) VALUES($1,$2,$3) ON CONFLICT (environment,observation_id) DO UPDATE SET payload=EXCLUDED.payload WHERE market_data.option_iv_observation_by_id.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string InsertObservationHistory = """
        INSERT INTO market_data.option_iv_observation_history(environment,series_id,methodology_version,calendar_bucket, value_date,sampling_slot,revision,observation_id,available_at_utc,payload) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) ON CONFLICT (environment,series_id,methodology_version,calendar_bucket,value_date,sampling_slot,revision,observation_id) DO UPDATE SET payload=EXCLUDED.payload WHERE market_data.option_iv_observation_history.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string InsertSnapshot = """
        INSERT INTO market_data.option_iv_snapshot_by_id(environment,snapshot_id,snapshot_digest,publication_sequence,payload) VALUES($1,$2,$3,$4,$5) ON CONFLICT (environment,snapshot_id) DO UPDATE SET payload=EXCLUDED.payload WHERE market_data.option_iv_snapshot_by_id.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string InsertMetricHistory = """
        INSERT INTO market_data.option_iv_metric_history(environment,series_id,methodology_version,metric_policy_version, calendar_bucket,value_date,sampling_slot,revision,snapshot_id,available_at_utc,publication_sequence,payload) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT (environment,series_id,methodology_version,metric_policy_version,calendar_bucket,value_date,sampling_slot,revision,snapshot_id) DO UPDATE SET payload=EXCLUDED.payload WHERE market_data.option_iv_metric_history.payload=EXCLUDED.payload RETURNING 1;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string InsertLatest = """
        INSERT INTO market_data.option_iv_latest(environment,series_id,methodology_version,metric_policy_version, snapshot_id,snapshot_digest,publication_sequence,available_at_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8) ON CONFLICT (environment,series_id,methodology_version,metric_policy_version) DO UPDATE SET snapshot_id=EXCLUDED.snapshot_id,snapshot_digest=EXCLUDED.snapshot_digest,publication_sequence=EXCLUDED.publication_sequence,available_at_utc=EXCLUDED.available_at_utc WHERE market_data.option_iv_latest.publication_sequence < EXCLUDED.publication_sequence;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string SelectObservationById = """
        SELECT payload FROM market_data.option_iv_observation_by_id WHERE environment=$1 AND observation_id=$2;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string SelectSnapshot = """
        SELECT snapshot_digest,publication_sequence,payload FROM market_data.option_iv_snapshot_by_id WHERE environment=$1 AND snapshot_id=$2;
        """;

    /// <summary>Native immutable volatility observation/publication persistence.</summary>
    public const string SelectLatest = """
        SELECT snapshot_id,snapshot_digest,publication_sequence,available_at_utc FROM market_data.option_iv_latest WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND metric_policy_version=$4;
        """;
    /// <summary>Checks canonical PostgreSQL query indexes; raw ticks belong to TradeTickQuoteDb.</summary>
    public const string GetCanonicalQueryReadiness = """
        WITH required(group_name,index_name,table_name,columns) AS (VALUES
          ('eod','futures_eod_data_ordered','futures_eod_data',ARRAY['contractid','valuedate','symbol']),
          ('eod','futures_eod_value_date','futures_eod_data',ARRAY['valuedate','contractid','symbol']),
          ('vix','vix_futures_eod_data_ordered','vix_futures_eod_data',ARRAY['contractid','valuedate']),
          ('vix','vix_futures_eod_value_date','vix_futures_eod_data',ARRAY['valuedate','contractid']),
          ('iti','futures_iti_signal_ordered','futures_iti_signal',ARRAY['contractid','valuedate','timeperiod','intrinsictimemode','intrinsictimetrend','intrinsictimegroupid','sequenceid'])),
        checks AS (SELECT r.group_name,COALESCE(i.indisvalid AND i.indisready
          AND i.indrelid=to_regclass('market_data.'||r.table_name)
          AND i.indpred IS NULL AND i.indexprs IS NULL
          AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ORDINALITY k(attnum,ordinality)
              JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum
              WHERE k.ordinality<=i.indnkeyatts ORDER BY k.ordinality)=r.columns,false) AS ready
          FROM required r LEFT JOIN pg_index i ON i.indexrelid=to_regclass('market_data.'||r.index_name))
        SELECT bool_and(ready) FILTER(WHERE group_name='eod'),
               bool_and(ready) FILTER(WHERE group_name='vix'),
               bool_and(ready) FILTER(WHERE group_name='iti') FROM checks;
        """;

    /// <summary>Reconciles canonical query sources in one database snapshot without duplicate read tables.</summary>
    public const string GetCanonicalQueryInventory = """
        SELECT 'eod' AS kind,count(*) AS rows,md5(COALESCE(string_agg(md5(to_jsonb(t)::text),'' ORDER BY to_jsonb(t)::text),'')) AS fingerprint
          FROM market_data.futures_eod_data t
        UNION ALL SELECT 'vix_rows',count(*),'' FROM market_data.vix_futures_eod_data
        UNION ALL SELECT 'vix',count(*),md5(COALESCE(string_agg(contractid,'' ORDER BY contractid),'')) FROM
          (SELECT DISTINCT contractid FROM market_data.vix_futures_eod_data) t
        UNION ALL SELECT 'iti',count(*),md5(COALESCE(string_agg(md5(to_jsonb(t)::text),'' ORDER BY to_jsonb(t)::text),''))
          FROM market_data.futures_iti_signal t;
        """;

    /// <summary>Creates or advances a checkpoint and stores its immutable terminal manifest.</summary>
    public const string SaveHistoricalDataLoad = """
    WITH checkpoint AS (
        INSERT INTO historical_data_load_checkpoint (
            data_load_attempt_id, request_sha256, status, stage, provider_job_id,
            provider_file_id, batch_ordinal, source_position, error_message, updated_at_utc)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
        ON CONFLICT (data_load_attempt_id) DO UPDATE SET
            request_sha256 = EXCLUDED.request_sha256,
            status = EXCLUDED.status,
            stage = EXCLUDED.stage,
            provider_job_id = EXCLUDED.provider_job_id,
            provider_file_id = EXCLUDED.provider_file_id,
            batch_ordinal = EXCLUDED.batch_ordinal,
            source_position = EXCLUDED.source_position,
            error_message = EXCLUDED.error_message,
            updated_at_utc = EXCLUDED.updated_at_utc
        RETURNING data_load_attempt_id
    )
    INSERT INTO historical_data_load_manifest (
        data_load_attempt_id, manifest_json, audit_json, created_at_utc)
    SELECT $1, $11, $12, $10
    FROM checkpoint
    WHERE $11 <> ''
    ON CONFLICT (data_load_attempt_id) DO NOTHING;
    """;

    /// <summary>Reads one exact attempt.</summary>
    public const string GetHistoricalDataLoadByAttempt = """
    SELECT c.data_load_attempt_id, c.request_sha256, c.status, c.stage,
           c.provider_job_id, c.provider_file_id, c.batch_ordinal,
           c.source_position, c.error_message, c.updated_at_utc,
           m.manifest_json, m.audit_json
    FROM historical_data_load_checkpoint c
    LEFT JOIN historical_data_load_manifest m
      ON m.data_load_attempt_id = c.data_load_attempt_id
    WHERE c.data_load_attempt_id = $1;
    """;

    /// <summary>Reads the immutable completed owner of one request hash.</summary>
    public const string GetCompletedHistoricalDataLoadByRequestHash = """
    SELECT c.data_load_attempt_id, c.request_sha256, c.status, c.stage,
           c.provider_job_id, c.provider_file_id, c.batch_ordinal,
           c.source_position, c.error_message, c.updated_at_utc,
           m.manifest_json, m.audit_json
    FROM historical_data_load_checkpoint c
    INNER JOIN historical_data_load_manifest m
      ON m.data_load_attempt_id = c.data_load_attempt_id
    WHERE c.request_sha256 = $1 AND c.status = 2
    LIMIT 1;
    """;
    /// <summary>GetCanonicalYieldCurvesForRebuild database statement.</summary>
    public const string GetCanonicalYieldCurvesForRebuild = "SELECT valueDate,oneMonth,twoMonth,threeMonth,sixMonth,oneYear,twoYear,threeYear,fiveYear,sevenYear,tenYear,twentyYear,thirtyYear FROM market_data.yield_curve_rates WHERE id=1 ORDER BY valueDate;";

    /// <summary>GetIndexedYieldCurvesForRebuild database statement.</summary>
    public const string GetIndexedYieldCurvesForRebuild = "SELECT valueDate,oneMonth,twoMonth,threeMonth,sixMonth,oneYear,twoYear,threeYear,fiveYear,sevenYear,tenYear,twentyYear,thirtyYear FROM market_data.yield_curve_rate_by_date WHERE lookupId=1 ORDER BY valueDate;";

    /// <summary>RebuildYieldCurveDateIndex database statement.</summary>
    public const string RebuildYieldCurveDateIndex = "INSERT INTO market_data.yield_curve_rate_by_date(lookupId,valueDate,oneMonth,twoMonth,threeMonth,sixMonth,oneYear,twoYear,threeYear,fiveYear,sevenYear,tenYear,twentyYear,thirtyYear) SELECT 1,valueDate,oneMonth,twoMonth,threeMonth,sixMonth,oneYear,twoYear,threeYear,fiveYear,sevenYear,tenYear,twentyYear,thirtyYear FROM market_data.yield_curve_rates WHERE id=1;";

    /// <summary>AcquireMarketDataIdentityLock database statement.</summary>
    public const string AcquireMarketDataIdentityLock = "SELECT pg_advisory_xact_lock(hashtextextended($1,0));";

    /// <summary>LockYieldCurveIndexes database statement.</summary>
    public const string LockYieldCurveIndexes = "LOCK TABLE market_data.yield_curve_rates,market_data.yield_curve_rate_by_date,market_data.yield_curve_rate_year IN SHARE ROW EXCLUSIVE MODE;";

    /// <summary>ClearYieldCurveDateIndex database statement.</summary>
    public const string ClearYieldCurveDateIndex = "DELETE FROM market_data.yield_curve_rate_by_date WHERE lookupId=1;";

    /// <summary>ClearYieldCurveYearIndex database statement.</summary>
    public const string ClearYieldCurveYearIndex = "DELETE FROM market_data.yield_curve_rate_year WHERE lookupId=1;";

    /// <summary>RebuildYieldCurveYearIndex database statement.</summary>
    public const string RebuildYieldCurveYearIndex = "INSERT INTO market_data.yield_curve_rate_year(lookupId,rateYear) SELECT DISTINCT 1,EXTRACT(YEAR FROM valueDate)::integer FROM market_data.yield_curve_rates WHERE id=1;";

    /// <summary>GetYieldCurveIndexYears database statement.</summary>
    public const string GetYieldCurveIndexYears = "SELECT rateYear FROM market_data.yield_curve_rate_year WHERE lookupId=1 ORDER BY rateYear;";

    /// <summary>ReadMarketDataImportOwnership database statement.</summary>
    public const string ReadMarketDataImportOwnership = "SELECT commandId,mayWrite FROM market_data.market_data_import_ownership WHERE dataset=$1 AND logicalKey=$2;";

    /// <summary>YieldCurveExists database statement.</summary>
    public const string YieldCurveExists = "SELECT EXISTS(SELECT 1 FROM market_data.yield_curve_rates WHERE id=1 AND valueDate=$1);";

    /// <summary>InsertMarketDataImportOwnership database statement.</summary>
    public const string InsertMarketDataImportOwnership = "INSERT INTO market_data.market_data_import_ownership(dataset,logicalKey,commandId,mayWrite,createdOn) VALUES($1,$2,$3,$4,$5);";

    /// <summary>InsertOptionChainParameterVersion database statement.</summary>
    public const string InsertOptionChainParameterVersion = "INSERT INTO market_data.strategy_option_chain_policy_version(set_id,version,parameter_hash) VALUES($1,$2,$3) ON CONFLICT(set_id,version) DO NOTHING;";

    /// <summary>GetOptionChainParameterVersion database statement.</summary>
    public const string GetOptionChainParameterVersion = "SELECT parameter_hash FROM market_data.strategy_option_chain_policy_version WHERE set_id=$1 AND version=$2;";

    /// <summary>UpsertOptionChainParameterCurrent database statement.</summary>
    public const string UpsertOptionChainParameterCurrent = "INSERT INTO market_data.strategy_option_chain_policy_current(environment,set_id,version,revision,published,parameter_hash) VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(environment,set_id,version) DO UPDATE SET revision=EXCLUDED.revision,published=EXCLUDED.published,parameter_hash=EXCLUDED.parameter_hash WHERE strategy_option_chain_policy_current.revision < EXCLUDED.revision;";

    /// <summary>LockTradeSignalIndexes database statement.</summary>
    public const string LockTradeSignalIndexes = "LOCK TABLE market_data.futures_trade_signal,market_data.futures_trade_signal_lookup_by_scope,market_data.futures_trade_signal_quarantine IN SHARE ROW EXCLUSIVE MODE;";

    /// <summary>ReadTradeSignalRepairRows database statement.</summary>
    public const string ReadTradeSignalRepairRows = "SELECT row_to_json(t)::text FROM market_data.futures_trade_signal t ORDER BY sequenceId,contractId,valueDate,timePeriod;";

    /// <summary>ClearTradeSignalLookup database statement.</summary>
    public const string ClearTradeSignalLookup = "DELETE FROM market_data.futures_trade_signal_lookup_by_scope;";

    /// <summary>GetOptionIvObservationHistory database statement.</summary>
    public const string GetOptionIvObservationHistory = """
            WITH ranked AS (
                SELECT value_date,sampling_slot,payload,
                    row_number() OVER (PARTITION BY value_date,sampling_slot ORDER BY revision DESC,observation_id) AS ordinal
                FROM market_data.option_iv_observation_history
                WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND calendar_bucket=$4 AND value_date>=$5 AND value_date<=$6
                    AND ($7::text IS NULL OR sampling_slot=$7)
                    AND ($8::timestamptz IS NULL OR available_at_utc<=$8)
            )
            SELECT value_date,sampling_slot,payload FROM ranked
            WHERE ordinal=1 AND ($9::date IS NULL OR (value_date,sampling_slot COLLATE "C")>($9,$10::text COLLATE "C"))
            ORDER BY value_date,sampling_slot COLLATE "C" LIMIT $11;
            """;

    /// <summary>GetOptionIvMetricHistory database statement.</summary>
    public const string GetOptionIvMetricHistory = """
            WITH ranked AS (
                SELECT value_date,sampling_slot,payload,
                    row_number() OVER (PARTITION BY value_date,sampling_slot ORDER BY revision DESC,publication_sequence DESC,snapshot_id) AS ordinal
                FROM market_data.option_iv_metric_history
                WHERE environment=$1 AND series_id=$2 AND methodology_version=$3 AND metric_policy_version=$4 AND calendar_bucket=$5 AND value_date>=$6 AND value_date<=$7
                    AND ($8::text IS NULL OR sampling_slot=$8)
                    AND ($9::timestamptz IS NULL OR available_at_utc<=$9)
            )
            SELECT value_date,sampling_slot,payload FROM ranked
            WHERE ordinal=1 AND ($10::date IS NULL OR (value_date,sampling_slot COLLATE "C")>($10,$11::text COLLATE "C"))
            ORDER BY value_date,sampling_slot COLLATE "C" LIMIT $12;
            """;
}
