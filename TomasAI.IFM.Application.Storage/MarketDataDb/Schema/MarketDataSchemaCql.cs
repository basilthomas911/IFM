namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

internal static class MarketDataSchemaCql
{
    public const string CreateFuturesItiTrendDeltaModelTable = """
        CREATE TABLE IF NOT EXISTS futures_iti_trend_delta_model (
            symbol TEXT,
            valueDate DATE,
            count INT,
            endDate DATE,
            lossFunction DOUBLE,
            maximum DOUBLE,
            mean DOUBLE,
            meanAbsoluteError DOUBLE,
            meanSquaredError DOUBLE,
            median DOUBLE,
            minimum DOUBLE,
            modelData BLOB,
            rootMeanSquaredError DOUBLE,
            rSquared DOUBLE,
            skewness DOUBLE,
            startDate DATE,
            stdDev DOUBLE,
            variance DOUBLE,
            PRIMARY KEY (symbol, valueDate)
        ) WITH CLUSTERING ORDER BY (valueDate ASC);
        """;

    public const string CreateFuturesOptionQuoteTable = """
        CREATE TABLE IF NOT EXISTS futures_option_quote (
            quoteId INT,
            contractId TEXT,
            requestId INT,
            createdBy TEXT,
            createdOn TIMESTAMP,
            PRIMARY KEY (quoteId, contractId, requestId)
        ) WITH CLUSTERING ORDER BY (contractId ASC, requestId ASC);
        """;

    public const string CreateFuturesOptionQuoteDataTable = """
        CREATE TABLE IF NOT EXISTS futures_option_quote_data (
            quoteId INT,
            contractId TEXT,
            requestId INT,
            sequenceId BIGINT,
            askPrice DECIMAL,
            askSize INT,
            bidPrice DECIMAL,
            bidSize INT,
            PRIMARY KEY (quoteId, contractId, requestId, sequenceId)
        ) WITH CLUSTERING ORDER BY (contractId ASC, requestId ASC, sequenceId ASC);
        """;

    public const string CreateFuturesRsiSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_rsi_signal (
            contractId TEXT,
            valueDate DATE,
            timestamp TIME,
            averagePriceGain DECIMAL,
            averagePriceLoss DECIMAL,
            periodLength INT,
            price DECIMAL,
            priceChange DECIMAL,
            priceGain DECIMAL,
            priceLoss DECIMAL,
            rs DOUBLE,
            rsi DOUBLE,
            rsiAverage DOUBLE,
            rsiSlope DOUBLE,
            signalType TEXT,
            timePeriod TEXT,
            windowSize INT,
            PRIMARY KEY (contractId, valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateFuturesTdiSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_tdi_signal (
            contractId TEXT,
            valueDate DATE,
            timestamp TIME,
            downTrendCount INT,
            tdi TEXT,
            tdiStrength TEXT,
            timePeriod TEXT,
            upTrendCount INT,
            PRIMARY KEY (contractId, valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateMarketHolidayTable = """
        CREATE TABLE IF NOT EXISTS market_holiday (
            currencyType TEXT,
            holidayDate DATE,
            description TEXT,
            PRIMARY KEY (currencyType, holidayDate)
        ) WITH CLUSTERING ORDER BY (holidayDate ASC);
        """;

    public const string CreateNormalCurveDataTable = """
        CREATE TABLE IF NOT EXISTS normal_curve_data (
            stdDevIndex DOUBLE PRIMARY KEY,
            percent DOUBLE
        );
        """;

    public const string CreateTradeLiveFeedTable = """
    CREATE TABLE IF NOT EXISTS trade_live_feed (
    orderId int,
    tradeId int,
    tradeLiveFeedState text,
    PRIMARY KEY (orderId, tradeId)
    );
    """;

    public const string CreateFuturesitiSignalTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_signal (
    contractId text,
    valueDate date,
    timePeriod text,
    sequenceId bigint,
    intrinsicTime timestamp,
    intrinsicTimeGroupId int,
    intrinsicTimeLength double,
    intrinsicPrice double,
    intrinsicTimeTrend text,
    intrinsicTimeMode text,
    trendPrice double,
    trendExtreme double,
    trendReversal double,
    trendDelta double,
    targetDelta double,
    lambda double,
    tradingDays int,
    threshold double,
    upTrendTrigger double,
    downTrendTrigger double,
    tradeState text,
    PRIMARY KEY (contractId, valueDate, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate desc, timePeriod desc, intrinsicTimeMode desc, intrinsicTimeTrend desc,intrinsicTimeGroupId desc, sequenceId desc);
    """;

    public const string CreateFuturesTickDataTable = """
    CREATE TABLE IF NOT EXISTS futures_tick_data (
    contractId text,
    valueDate date,
    tickId bigint,
    tickTime time,
    price decimal,
    size int,
    PRIMARY KEY (contractId, valueDate, tickId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, tickId ASC);
    """;

    public const string CreateFuturesOptionTickDataTable = """
    CREATE TABLE IF NOT EXISTS futures_option_tick_data (
    contractId text,
    valueDate date,
    tickId bigint,
    tickTime time,
    optionPrice decimal,
    bidPrice decimal,
    askPrice decimal,
    bidSize int,
    askSize int,
    impliedVolatility double,
    underlyingPrice decimal,
    delta double,
    gamma double,
    vega double,
    theta double,
    rho double,
    PRIMARY KEY (contractId, valueDate, tickId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, tickId ASC);
    """;

    public const string CreateFuturesOptionTickPriceDataTable = """
    CREATE TABLE IF NOT EXISTS futures_option_tick_price_data (
    contractId text,
    valueDate date,
    tickId bigint,
    tickTime time,
    optionPrice decimal,
    bidPrice decimal,
    askPrice decimal,
    bidSize int,
    askSize int,
    impliedVolatility double,
    underlyingPrice decimal,
    delta double,
    gamma double,
    vega double,
    theta double,
    rho double,
    PRIMARY KEY (contractId, valueDate, tickId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, tickId ASC);
    """;

    public const string CreateFuturesBaraDataTable = """
    CREATE TABLE IF NOT EXISTS futures_bar_data (
    contractId text,
    symbol text,
    valueDate date,
    barDate timestamp,
    barRateType text,
    barValue decimal,
    upTrendTrigger double,
    downTrendTrigger double,
    PRIMARY KEY (contractId, symbol, valueDate, barDate)
    ) WITH CLUSTERING ORDER BY (symbol ASC, valueDate DESC,barDate ASC);
    """;

    public const string CreateFuturesClosingPriceTable = """
    CREATE TABLE IF NOT EXISTS futures_closing_price (
    contractId text,
    valueDate date,
    closingPrice decimal,
    createdOn timestamp,
    createdBy text,
    PRIMARY KEY (contractId, valueDate)
    );
    """;

    public const string CreateFuturesEodDataTable = """
    CREATE TABLE IF NOT EXISTS futures_eod_data (
    contractId text,
    valueDate date,
    symbol text,
    openPrice decimal,
    highPrice decimal,
    lowPrice decimal,
    closePrice decimal,
    volume int,
    dailyPercentChange double,
    dailyStdDev double,
    dailyStdDevAmount double,
    upperBand double,
    mean double,
    lowerBand double,
    marketDirection text,
    marketVolatility text,
    priceDirection text,
    priceVolatility text,
    marketDirectionIndicator double,
    windowSize int,
    PRIMARY KEY (contractId, valueDate, symbol)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, symbol ASC);
    """;

    public const string CreateFuturesIntraDayDataTable = """
    CREATE TABLE IF NOT EXISTS futures_intra_day_data (
    contractId text,
    valueDate date,
    sequenceId bigint,
    symbol text,
    openPrice decimal,
    highPrice decimal,
    lowPrice decimal,
    closePrice decimal,
    volume int,
    dailyPercentChange double,
    dailyStdDev double,
    dailyStdDevAmount double,
    upperBand double,
    mean double,
    lowerBand double,
    marketDirection text,
    marketVolatility text,
    priceDirection text,
    priceVolatility text,
    marketDirectionIndicator double,
    windowSize int,
    PRIMARY KEY (contractId, valueDate, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, sequenceId DESC);
    """;

    public const string CreateVixFuturesEodDataTable = """
    CREATE TABLE IF NOT EXISTS vix_futures_eod_data (
    contractId text,
    valueDate date,
    openPrice decimal,
    highPrice decimal,
    lowPrice decimal,
    closePrice decimal,
    volume int,
    PRIMARY KEY (contractId, valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;

    public const string CreateFuturesTradeSignalTable = """
    CREATE TABLE IF NOT EXISTS futures_trade_signal (
    contractId text,
    valueDate date,
    timePeriod text,
    timestamp time,
    sequenceId bigint,
    mean double,
    stdDev double,
    futuresPrice double,
    priceChangePercent double,
    fundRiskPercent double,
    rsi double,
    rsiSlope double,
    trendType text,
    trendStrength text,
    tradeSignal text,
    tdi text,
    tdiStrength text,
    mdi double,
    mdiTrend text,
    mdiUpTrendLimit double,
    mdiDownTrendLimit double,
    upTrendingTrigger double,
    downTrendingTrigger double,
    entryTrigger double,
    exitTrigger double,
    trendDelta double,
    trendExtreme double,
    trendReversal double,
    fiftyDMA decimal,
    twoHundredDMA decimal,
    tradeExecuteState text,
    PRIMARY KEY (contractId, valueDate, timePeriod, timestamp, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, timePeriod DESC, timestamp DESC, sequenceId DESC);
    """;

    public const string CreateFuturesEodDataIndexTable = """
    CREATE TABLE IF NOT EXISTS futures_eod_data_index(
    valueDate date,
    contractId text,
    PRIMARY KEY (valueDate, contractId)
    );
    """;

    public const string CreateFuturesItiSignalIndexTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_signal_index(
    valueDate date,
    contractId text,
    PRIMARY KEY (valueDate, contractId)
    );
    """;

    public const string CreateFuturesItiTrendClassDataTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_class_data (
    symbol TEXT,
    valueDate DATE,
    timestamp TIMESTAMP,
    sequenceId BIGINT,
    trendClass FLOAT,
    trendDirection FLOAT,
    trendDirectionMode FLOAT,
    trendDelta FLOAT,
    futuresRSI FLOAT,
    PRIMARY KEY (symbol, valueDate, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, sequenceId ASC);
    """;

    public const string CreateFuturesItiTrendDeltaDataTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_delta_data (
    symbol text,
    valueDate date,
    timestamp timestamp,
    sequenceId bigint,
    trendDelta float,
    trendDirection float,
    trendDirectionMode float,
    futuresPrice float,
    trendExtreme float,
    futuresRSI float,
    PRIMARY KEY (symbol, valueDate, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, sequenceId ASC);
    """;

    public const string CreateFuturesItiTrendClassModelTable = """
    CREATE TABLE IF NOT EXISTS futures_iti_trend_class_model (
    symbol text,
    valueDate date,
    startDate date,
    endDate date,
    count int,
    maximum double,
    mean double,
    median double,
    minimum double,
    skewness double,
    stdDev double,
    variance double,
    accuracy double,
    areaUnderPrecisionRecallCurve double,
    areaUnderRocCurve double,
    entropy double,
    f1Score double,
    modelData blob,
    PRIMARY KEY (symbol, valueDate)
    );
    """;

    public const string CreateFuturesRsiSignal_SignalTypeIndex = """
    CREATE INDEX IF NOT EXISTS futures_rsi_signal_signaltype ON futures_rsi_signal(signalType);
    """;

    public const string CreateRateOfReturn = """
    CREATE TABLE IF NOT EXISTS rate_of_return (
    symbol TEXT,
    valueDate DATE,
    rateOfReturn DOUBLE,
    PRIMARY KEY (symbol, valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;

    public const string CreateYieldCurveRateTable = """
    CREATE TABLE IF NOT EXISTS yield_curve_rates (
    valueDate date PRIMARY KEY,
    oneMonth double,
    twoMonth double,
    threeMonth double,
    sixMonth double,
    oneYear double,
    twoYear double,
    threeYear double,
    fiveYear double,
    sevenYear double,
    tenYear double,
    twentyYear double,
    thirtyYear double
    );
    """;
}
