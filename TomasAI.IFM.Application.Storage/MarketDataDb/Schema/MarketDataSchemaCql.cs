namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

internal static class MarketDataSchemaCql
{
    public const string CreateMarketDataImportOwnershipTable = """
    CREATE TABLE IF NOT EXISTS market_data_import_ownership (
    dataset text,
    logicalKey text,
    commandId uuid,
    mayWrite boolean,
    createdOn timestamp,
    PRIMARY KEY ((dataset, logicalKey))
    );
    """;

    public const string CreateEconomicCalendarV2Table = """
    CREATE TABLE IF NOT EXISTS economic_calendar_v2 (
    countryCode text,
    monthBucket int,
    eventDate timestamp,
    eventName text,
    actual text,
    forecast text,
    prior text,
    impact text,
    unit text,
    change text,
    changePercentage text,
    createdOn timestamp,
    createdBy text,
    commandId uuid,
    PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)
    )
    WITH CLUSTERING ORDER BY (eventDate DESC, eventName ASC);
    """;

    public const string CreateEconomicCalendarCountryCodeTable = """
    CREATE TABLE IF NOT EXISTS economic_calendar_country_code (
    lookupId int,
    countryCode text,
    PRIMARY KEY ((lookupId), countryCode)
    )
    WITH CLUSTERING ORDER BY (countryCode ASC);
    """;

    public const string CreateEconomicCalendarCutoverV2Table = """
    CREATE TABLE IF NOT EXISTS economic_calendar_cutover_v2 (
    cutoverId int PRIMARY KEY,
    sourceRows bigint,
    targetRows bigint,
    sourceFingerprint text,
    targetFingerprint text,
    verified boolean,
    updatedOn timestamp
    );
    """;

    public const string CreateTickQuoteItemType = """
        CREATE TYPE IF NOT EXISTS tick_quote_item (
            source_sequence bigint,
            source_event_timestamp_ns bigint,
            source_receive_timestamp_ns bigint,
            header_flags smallint,
            bid_price_raw bigint,
            bid_price decimal,
            bid_size bigint,
            bid_count bigint,
            ask_price_raw bigint,
            ask_price decimal,
            ask_size bigint,
            ask_count bigint
        );
        """;

    public const string CreateTickTradeDataTable = """
        CREATE TABLE IF NOT EXISTS tick_trade_data (
            asset_type_id tinyint, contract_id text, value_date date,
            sequence_id bigint, aggregation_timestamp_utc timestamp,
            aggregation_timestamp_utc_ticks bigint, aggregation_time time,
            schema_version smallint, dataset text, definition_date date,
            publisher_id int, instrument_id bigint, actor_event_id uuid,
            actor_event_log_id bigint, command_id uuid, aggregate_id text,
            event_source text, received_on timestamp, source_sequence bigint,
            source_event_timestamp_ns bigint, source_receive_timestamp_ns bigint,
            header_flags smallint, price_raw bigint, price decimal, size bigint,
            action smallint, side smallint, dbn_flags smallint,
            PRIMARY KEY ((asset_type_id, contract_id), value_date, aggregation_time, sequence_id)
        ) WITH CLUSTERING ORDER BY (value_date ASC, aggregation_time ASC, sequence_id ASC);
        """;

    public const string CreateTickQuoteDataTable = """
        CREATE TABLE IF NOT EXISTS tick_quote_data (
            asset_type_id tinyint, contract_id text, value_date date,
            sequence_id bigint, aggregation_timestamp_utc timestamp,
            aggregation_timestamp_utc_ticks bigint, aggregation_time time,
            schema_version smallint, dataset text, definition_date date,
            publisher_id int, instrument_id bigint, actor_event_id uuid,
            actor_event_log_id bigint, command_id uuid, aggregate_id text,
            event_source text, received_on timestamp, emission_reason smallint,
            quote_count smallint, quote_data frozen<list<frozen<tick_quote_item>>>,
            PRIMARY KEY ((asset_type_id, contract_id), value_date, aggregation_time, sequence_id)
        ) WITH CLUSTERING ORDER BY (value_date ASC, aggregation_time ASC, sequence_id ASC);
        """;
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
            sourceSequence BIGINT,
            sourceEventTimestamp TIMESTAMP,
            signalType TEXT,
            timePeriod TEXT,
            windowSize INT,
            PRIMARY KEY ((contractId, timePeriod, periodLength), valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string AddFuturesRsiSignalSourceSequenceColumn = """
        ALTER TABLE futures_rsi_signal
        ADD sourceSequence BIGINT;
        """;

    public const string AddFuturesRsiSignalSourceEventTimestampColumn = """
        ALTER TABLE futures_rsi_signal
        ADD sourceEventTimestamp TIMESTAMP;
        """;

    public const string AddFuturesEodDataFiftyDmaColumn = """
        ALTER TABLE futures_eod_data
        ADD fiftyDMA DECIMAL;
        """;

    public const string AddFuturesEodDataTwoHundredDmaColumn = """
        ALTER TABLE futures_eod_data
        ADD twoHundredDMA DECIMAL;
        """;

    public const string AddFuturesEodDataByMonthFiftyDmaColumn = """
        ALTER TABLE futures_eod_data_by_month
        ADD fiftyDMA DECIMAL;
        """;

    public const string AddFuturesEodDataByMonthTwoHundredDmaColumn = """
        ALTER TABLE futures_eod_data_by_month
        ADD twoHundredDMA DECIMAL;
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

    public const string CreateFuturesTradersDynamicIndexSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_traders_dynamic_index_signal (
            contractId TEXT,
            timePeriod TEXT,
            configurationId TEXT,
            valueDate DATE,
            timestamp TIME,
            schemaVersion INT,
            rsiPeriod INT,
            priceLinePeriod INT,
            signalLinePeriod INT,
            marketBasePeriod INT,
            volatilityBandPeriod INT,
            volatilityBandDeviation DOUBLE,
            price DECIMAL,
            rsi DOUBLE,
            priceLine DOUBLE,
            signalLine DOUBLE,
            marketBaseLine DOUBLE,
            upperVolatilityBand DOUBLE,
            lowerVolatilityBand DOUBLE,
            bandWidth DOUBLE,
            priceSignalDivergence DOUBLE,
            crossType TEXT,
            marketState TEXT,
            trendDirection TEXT,
            trendStrength TEXT,
            sourceSequence BIGINT,
            sourceEventTimestamp TIMESTAMP,
            PRIMARY KEY ((contractId, timePeriod, configurationId), valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateFuturesMacdSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_macd_signal (
            contractId TEXT,
            valueDate DATE,
            timePeriod TEXT,
            periodLength INT,
            timestamp TIME,
            futuresPrice DECIMAL,
            macdLine DOUBLE,
            signalLine DOUBLE,
            histogram DOUBLE,
            macd TEXT,
            macdStrength TEXT,
            PRIMARY KEY ((contractId, timePeriod, periodLength), valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateFuturesMacdSignalV2Table = """
        CREATE TABLE IF NOT EXISTS futures_macd_signal_v2 (
            contractId TEXT,
            valueDate DATE,
            timePeriod TEXT,
            signalEmaPeriod INT,
            fastEmaPeriod INT,
            slowEmaPeriod INT,
            timestamp TIME,
            futuresPrice DECIMAL,
            fastEma DOUBLE,
            slowEma DOUBLE,
            macdLine DOUBLE,
            signalLine DOUBLE,
            histogram DOUBLE,
            macd TEXT,
            macdStrength TEXT,
            PRIMARY KEY ((contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod), valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateFuturesAdxSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_adx_signal (
            contractId TEXT,
            valueDate DATE,
            timePeriod TEXT,
            periodLength INT,
            timestamp TIME,
            futuresPrice DECIMAL,
            plusDI DOUBLE,
            minusDI DOUBLE,
            adxValue DOUBLE,
            adx TEXT,
            adxStrength TEXT,
            PRIMARY KEY ((contractId, timePeriod, periodLength), valueDate, timestamp)
        ) WITH CLUSTERING ORDER BY (valueDate DESC, timestamp DESC);
        """;

    public const string CreateFuturesAtrSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_atr_signal (
            contractId TEXT,
            valueDate DATE,
            timePeriod TEXT,
            periodLength INT,
            timestamp TIME,
            futuresPrice DECIMAL,
            atrValue DOUBLE,
            trueRange DOUBLE,
            atr TEXT,
            atrStrength TEXT,
            PRIMARY KEY ((contractId, timePeriod, periodLength), valueDate, timestamp)
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

    public const string CreateFuturesItiSignalByContractDayV2Table = """
    CREATE TABLE IF NOT EXISTS futures_iti_signal_by_contract_day_v2 (
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
    PRIMARY KEY ((contractId, valueDate), intrinsicTimeMode, sequenceId, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId)
    ) WITH CLUSTERING ORDER BY (intrinsicTimeMode ASC, sequenceId DESC, timePeriod ASC, intrinsicTimeTrend ASC, intrinsicTimeGroupId ASC);
    """;

    public const string CreateFuturesItiSignalByContractMonthV2Table = """
    CREATE TABLE IF NOT EXISTS futures_iti_signal_by_contract_month_v2 (
    contractId text,
    yearMonth int,
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
    PRIMARY KEY ((contractId, yearMonth), valueDate, sequenceId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, sequenceId DESC, timePeriod ASC, intrinsicTimeMode ASC, intrinsicTimeTrend ASC, intrinsicTimeGroupId ASC);
    """;

    public const string CreateFuturesItiSignalByTrendModeMonthV2Table = """
    CREATE TABLE IF NOT EXISTS futures_iti_signal_by_trend_mode_month_v2 (
    contractId text,
    yearMonth int,
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
    PRIMARY KEY ((contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth), valueDate, sequenceId, timePeriod, intrinsicTimeGroupId)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, sequenceId DESC, timePeriod ASC, intrinsicTimeGroupId ASC);
    """;

    public const string CreateFuturesItiTimeFrameStateTable = """
      CREATE TABLE IF NOT EXISTS futures_iti_timeframe_state (
      contractId text,
      timePeriod text,
      calendarBucketStart date,
      timeFrameStartValueDate date,
      valueDate date,
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
      bandAnchorPrice double,
      bandPercentage double,
      bandSize double,
      PRIMARY KEY ((contractId, timePeriod, calendarBucketStart))
      );
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

    public const string CreateFuturesTickDataByTimeTable = """
    CREATE TABLE IF NOT EXISTS futures_tick_data_by_time (
    contractId text,
    valueDate date,
    tickTime time,
    tickId bigint,
    price decimal,
    size int,
    PRIMARY KEY ((contractId, valueDate), tickTime, tickId)
    ) WITH CLUSTERING ORDER BY (tickTime DESC, tickId DESC);
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
    volume bigint,
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
    fiftyDMA decimal,
    twoHundredDMA decimal,
    PRIMARY KEY (contractId, valueDate, symbol)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, symbol ASC);
    """;

    public const string CreateFuturesEodDataByMonthTable = """
    CREATE TABLE IF NOT EXISTS futures_eod_data_by_month (
    yearMonth int,
    contractId text,
    valueDate date,
    symbol text,
    openPrice decimal,
    highPrice decimal,
    lowPrice decimal,
    closePrice decimal,
    volume bigint,
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
    fiftyDMA decimal,
    twoHundredDMA decimal,
    PRIMARY KEY ((yearMonth), valueDate, contractId, symbol)
    ) WITH CLUSTERING ORDER BY (valueDate DESC, contractId ASC, symbol ASC);
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
    volume bigint,
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
    volume bigint,
    PRIMARY KEY (contractId, valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;

    public const string CreateVixFuturesContractIndexTable = """
    CREATE TABLE IF NOT EXISTS vix_futures_contract_index (
    bucket int,
    contractId text,
    PRIMARY KEY ((bucket), contractId)
    ) WITH CLUSTERING ORDER BY (contractId ASC);
    """;

    public const string CreateMarketDataProjectionMonthTable = """
    CREATE TABLE IF NOT EXISTS market_data_projection_month (
    projectionName text,
    yearMonth int,
    PRIMARY KEY (projectionName, yearMonth)
    ) WITH CLUSTERING ORDER BY (yearMonth DESC);
    """;

    public const string CreateMarketDataProjectionStateV2Table = """
    CREATE TABLE IF NOT EXISTS market_data_projection_state_v2 (
    projectionName text PRIMARY KEY,
    generation uuid,
    isReady boolean,
    blocked boolean,
    activeOperations set<uuid>,
    sourceRowCount bigint,
    projectedRowCount bigint,
    sourceFingerprint text,
    projectedFingerprint text,
    completedOn timestamp
    );
    """;

    public const string CreateMarketDataProjectionMutationTable = """
    CREATE TABLE IF NOT EXISTS market_data_projection_mutation (
    projectionName text,
    mutationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((projectionName), mutationId)
    );
    """;

    // Ordinary dual writes are coordinated at the query-partition scope. Keeping the
    // scope in the partition key prevents live tick traffic for unrelated contracts
    // from contending on the projection-wide migration state row.
    public const string CreateMarketDataProjectionScopeStateV3Table = """
    CREATE TABLE IF NOT EXISTS market_data_projection_scope_state_v3 (
    projectionName text,
    scopeKey text,
    generation uuid,
    isReady boolean,
    blocked boolean,
    activeOperations set<uuid>,
    completedOn timestamp,
    PRIMARY KEY ((projectionName, scopeKey))
    );
    """;

    // Mutation rows are partitioned by the same query scope. They are durable recovery
    // evidence and intentionally have no TTL; age alone must never release a writer.
    public const string CreateMarketDataProjectionScopeMutationV3Table = """
    CREATE TABLE IF NOT EXISTS market_data_projection_scope_mutation_v3 (
    projectionName text,
    scopeKey text,
    mutationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((projectionName, scopeKey), mutationId)
    );
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

    public const string CreateFuturesTradeSignalLookupTable = """
    CREATE TABLE IF NOT EXISTS futures_trade_signal_lookup_by_scope (
    scope text,
    entryId text,
    sequenceId bigint,
    contractId text,
    valueDate date,
    timePeriod text,
    PRIMARY KEY (scope, entryId)
    );
    """;

    public const string CreateFuturesTradeSignalQuarantineTable = """
    CREATE TABLE IF NOT EXISTS futures_trade_signal_quarantine (
    fingerprint text,
    sourcePayload text,
    reason text,
    quarantinedOn timestamp,
    PRIMARY KEY (fingerprint)
    );
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
    id int,
    valueDate date,
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
    thirtyYear double,
    PRIMARY KEY ((id), valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;

    public const string CreateYieldCurveRateYearTable = """
    CREATE TABLE IF NOT EXISTS yield_curve_rate_year (
    lookupId int,
    rateYear int,
    PRIMARY KEY ((lookupId), rateYear)
    ) WITH CLUSTERING ORDER BY (rateYear DESC);
    """;

    public const string CreateYieldCurveRateByDateTable = """
    CREATE TABLE IF NOT EXISTS yield_curve_rate_by_date (
    lookupId int,
    valueDate date,
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
    thirtyYear double,
    PRIMARY KEY ((lookupId), valueDate)
    ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;
}
