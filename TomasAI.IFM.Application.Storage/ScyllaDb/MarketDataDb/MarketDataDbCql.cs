namespace TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

internal static class MarketDataDbCql
{

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

    public const string CreateFuturesItiSignalTable = """
        CREATE TABLE IF NOT EXISTS futures_iti_signal (
        contractId text,
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
        lambda double,
        targetDelta double,
        predictedDelta double,
        trendDelta double,
        upTrendTrigger double,
        downTrendTrigger double,
        futuresPercentChange double,
        futuresMean double,
        futuresStdDev double,
        futuresMDI double,
        futuresMDITrend text,
        futuresMDIUpTrendLimit double,
        futuresMDIDownTrendLimit double,
        futuresRSI double,
        futuresRSISlope double,
        futuresFiftyDMA decimal,
        futuresTwoHundredDMA decimal,
        tradeState text,
        upTrendCoastLineCounter int,
        downTrendCoastLineCounter int,
        PRIMARY KEY (contractId, valueDate, intrinsicTimeGroupId, sequenceId)
    ) WITH CLUSTERING ORDER BY (valueDate desc, intrinsicTimeGroupId desc, sequenceId desc);
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

    public const string DeleteFuturesBarData = """
        DELETE FROM futures_bar_data
        WHERE contractId = :contractId AND symbol = :symbol AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesEodData = """
        DELETE FROM futures_eod_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesIntraDayData = """
        DELETE FROM futures_intra_day_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesTickData = """
        DELETE FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteVixFuturesEodData = """
        DELETE FROM vix_futures_eod_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesOptionTickData = """
        DELETE FROM futures_option_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesOptionTickPriceData = """
        DELETE FROM futures_option_tick_price_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;


    public const string DeleteFuturesClosingPrice = """
        DELETE FROM futures_closing_price
        WHERE contractId = :contractId 
        AND valueDate = :valueDate;
    """;

    public const string GetFuturesBarData = """
        SELECT
            contractId AS "ContractId",
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            barDate AS "BarDate",
            barRateType AS "BarRateType",
            barValue AS "BarValue",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger"
        FROM futures_bar_data
        WHERE contractId = :contractId 
        AND symbol = :symbol 
        AND valueDate = :valueDate
        AND barDate >= :startDate
        AND barDate <= :endDate;
    """;

    public const string GetFuturesBarDataAll = """
        SELECT
            contractId AS "ContractId",
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            barDate AS "BarDate",
            barRateType AS "BarRateType",
            barValue AS "BarValue",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger"
        FROM futures_bar_data;
    """;

    public const string GetLastFuturesBarDataAll = """
        SELECT
            contractId AS "ContractId",
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            barDate AS "BarDate",
            barRateType AS "BarRateType",
            barValue AS "BarValue",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger"
        FROM futures_bar_data
        WHERE contractId = :contractId 
        AND symbol = :symbol 
        AND valueDate = :valueDate;
    """;

    public const string GetLastFuturesBarData = """
        SELECT
            contractId AS "ContractId",
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            barDate AS "BarDate",
            barRateType AS "BarRateType",
            barValue AS "BarValue",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger"
        FROM futures_bar_data
        WHERE contractId = :contractId 
        AND symbol = :symbol 
        AND valueDate = :valueDate
        ORDER BY barDate DESC
        LIMIT 1;
    """;

    public const string GetFuturesBarDataCount = """
        SELECT count(*) as "Value"
        FROM futures_bar_data 
        WHERE contractId = :contractId
        AND symbol = :symbol
        AND valueDate = :valueDate
        GROUP BY contractId, symbol, valueDate;
    """;

    public const string GetFuturesClosingPrice = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            closingPrice AS "ClosingPrice", 
            createdOn AS "CreatedOn", 
            createdBy AS "CreatedBy"
        FROM 
            futures_closing_price
        WHERE 
            contractId = :contractId 
            AND valueDate = :valueDate;
    """;

    public const string GetFuturesOpenPrice = """
        SELECT ClosingPrice as "Value"
        FROM futures_closing_price 
        WHERE ContractId = :contractId 
        AND ValueDate < :valueDate 
        ORDER BY ValueDate DESC 
        LIMIT 1;
    """;

    public const string GetFuturesOptionTickData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId AS "TickId", 
            tickTime AS "TickTime", 
            optionPrice AS "OptionPrice", 
            bidPrice AS "BidPrice", 
            askPrice AS "AskPrice", 
            bidSize AS "BidSize", 
            askSize AS "AskSize", 
            impliedVolatility AS "ImpliedVolatility", 
            underlyingPrice AS "UnderlyingPrice", 
            delta AS "Delta", 
            gamma AS "Gamma", 
            vega AS "Vega", 
            theta AS "Theta", 
            rho AS "Rho"
        FROM futures_option_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate AND tickId = :tickId;
    """;

    public const string GetFuturesOptionTickPriceData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId AS "TickId", 
            tickTime AS "TickTime", 
            optionPrice AS "OptionPrice", 
            bidPrice AS "BidPrice", 
            askPrice AS "AskPrice", 
            bidSize AS "BidSize", 
            askSize AS "AskSize", 
            impliedVolatility AS "ImpliedVolatility", 
            underlyingPrice AS "UnderlyingPrice", 
            delta AS "Delta", 
            gamma AS "Gamma", 
            vega AS "Vega", 
            theta AS "Theta", 
            rho AS "Rho"
        FROM futures_option_tick_price_data
        WHERE contractId = :contractId AND valueDate = :valueDate AND tickId = :tickId;
    """;

    public const string GetFuturesTickData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId as "TickId",
            tickTime AS "TickTime", 
            price AS "Price", 
            size AS "Size"
        FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate AND tickId = :tickId;
    """;

    public const string GetLastFuturesTickData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId as "TickId",
            tickTime AS "TickTime", 
            price AS "Price", 
            size AS "Size"
        FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate
        ORDER BY valueDate DESC, tickId DESC
        LIMIT 1;
    """;

    public const string GetLastFuturesTickDataByTickTime = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId as "TickId",
            tickTime AS "TickTime", 
            price AS "Price", 
            size AS "Size"
        FROM futures_tick_data
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND tickTime = :tickTime
        ORDER BY valueDate DESC, tickId DESC
        LIMIT 1
        ALLOW FILTERING;
    """;

    public const string GetFuturesHighPrice = """
        SELECT max(price) as "Value" 
        FROM futures_tick_data 
        WHERE contractId = :contractId 
        AND valueDate = :valueDate;
    """;

    public const string GetFuturesLowPrice = """
        SELECT min(price) as "Value" 
        FROM futures_tick_data 
        WHERE contractId = :contractId 
        AND valueDate = :valueDate;
    """;

    public const string GetFuturesVolume = """
        SELECT sum(size) as "Value" 
        FROM futures_tick_data 
        WHERE contractId = :contractId 
        AND valueDate = :valueDate;
    """;

    public const string GetLastFuturesOptionTickDataId = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            MAX(tickId) AS "TickId"
        FROM futures_option_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate
        GROUP BY contractId, valueDate;
    """;

    public const string GetLastFuturesTickDataId = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            MAX(tickId) AS "TickId"
        FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate
        GROUP BY contractId, valueDate;
    """;

    public const string GetYesterdaysFuturesClosingPrice = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            closingPrice AS "ClosingPrice", 
            createdOn AS "CreatedOn", 
            createdBy AS "CreatedBy"
        FROM 
            futures_closing_price
        WHERE 
            contractId = :contractId 
            AND valueDate < :valueDate;
    """;

    public const string GetYesterdaysFuturesClosingPriceValue = """
        SELECT ClosingPrice as "Value"
        FROM futures_closing_price 
        WHERE ContractId = :contractId 
        AND ValueDate <= :valueDate 
        ORDER BY ValueDate DESC 
        LIMIT 1;
    """;

    public const string InsertFuturesBarData = """
        INSERT INTO futures_bar_data (
            contractId,
            symbol,
            valueDate,
            barDate,
            barRateType,
            barValue,
            upTrendTrigger,
            downTrendTrigger
        ) VALUES (
            :contractId,
            :symbol,
            :valueDate,
            :barDate,
            :barRateType,
            :barValue,
            :upTrendTrigger,
            :downTrendTrigger
        );
    """;

    public const string InsertFuturesClosingPrice = """
        INSERT INTO futures_closing_price (
            contractId, 
            valueDate, 
            closingPrice, 
            createdOn, 
            createdBy
        ) VALUES (
            :contractId, 
            :valueDate, 
            :closingPrice, 
            :createdOn, 
            :createdBy
        );
    """;

    public const string InsertFuturesEodData = """
        INSERT INTO futures_eod_data (
            contractId, 
            valueDate,
            symbol,
            openPrice, 
            highPrice, 
            lowPrice, 
            closePrice, 
            volume, 
            dailyPercentChange, 
            dailyStdDev, 
            dailyStdDevAmount, 
            upperBand, 
            mean, 
            lowerBand, 
            marketDirection, 
            marketVolatility, 
            priceDirection, 
            priceVolatility, 
            marketDirectionIndicator, 
            windowSize
        ) VALUES (
            :contractId, 
            :valueDate,
            :symbol,
            :openPrice, 
            :highPrice, 
            :lowPrice, 
            :closePrice, 
            :volume, 
            :dailyPercentChange, 
            :dailyStdDev, 
            :dailyStdDevAmount, 
            :upperBand, 
            :mean, 
            :lowerBand, 
            :marketDirection, 
            :marketVolatility, 
            :priceDirection, 
            :priceVolatility, 
            :marketDirectionIndicator, 
            :windowSize
        );
    """;

    public const string InsertFuturesIntraDayData = """
        INSERT INTO futures_intra_day_data (
            contractId, 
            valueDate,
            sequenceId,
            symbol,
            openPrice, 
            highPrice, 
            lowPrice, 
            closePrice, 
            volume, 
            dailyPercentChange, 
            dailyStdDev, 
            dailyStdDevAmount, 
            upperBand, 
            mean, 
            lowerBand, 
            marketDirection, 
            marketVolatility, 
            priceDirection, 
            priceVolatility, 
            marketDirectionIndicator, 
            windowSize
        ) VALUES (
            :contractId, 
            :valueDate,
            :sequenceId,
            :symbol,
            :openPrice, 
            :highPrice, 
            :lowPrice, 
            :closePrice, 
            :volume, 
            :dailyPercentChange, 
            :dailyStdDev, 
            :dailyStdDevAmount, 
            :upperBand, 
            :mean, 
            :lowerBand, 
            :marketDirection, 
            :marketVolatility, 
            :priceDirection, 
            :priceVolatility, 
            :marketDirectionIndicator, 
            :windowSize
        );
    """;


    public const string InsertFuturesOptionTickData = """
        INSERT INTO futures_option_tick_data (
            contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho
        ) VALUES (
            :contractId, :valueDate, :tickId, :tickTime, :optionPrice, :bidPrice, :askPrice, :bidSize, :askSize, :impliedVolatility, :underlyingPrice, :delta, :gamma, :vega, :theta, :rho
        );
    """;

    public const string InsertFuturesOptionTickPriceData = """
        INSERT INTO futures_option_tick_price_data (
            contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho
        ) VALUES (
            :contractId, :valueDate, :tickId, :tickTime, :optionPrice, :bidPrice, :askPrice, :bidSize, :askSize, :impliedVolatility, :underlyingPrice, :delta, :gamma, :vega, :theta, :rho
        );
    """;

    public const string InsertFuturesTickData = """
        INSERT INTO futures_tick_data (contractId, valueDate, tickId, tickTime, price, size)
        VALUES (:contractId, :valueDate, :tickId, :tickTime, :price, :size);
    """;

    public const string CreateFuturesTickIdCounterTable = """
        CREATE TABLE IF NOT EXISTS futures_tick_id_counter(
            contractId text,
            valueDate date,
            nextTickId counter,
            PRIMARY KEY ((contractId, valueDate))
        );
    """;

    public const string UpdateNextFuturesTickId = """
        UPDATE futures_tick_id_counter
        SET nextTickId = nextTickId + 1
        WHERE contractId = :contractId
        AND valueDate = :valueDate;
    """;

    public const string GetNextFuturesTickId = """
        SELECT nextTickId as "Value" 
        FROM futures_tick_id_counter 
        WHERE contractId = :contractId
        AND valueDate = :valueDate;
    """;

    public const string GetCurrentFuturesEodData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
        FROM futures_eod_data
        WHERE valueDate <= :valueDate
        ORDER BY valueDate DESC
        LIMIT 1
        ALLOW FILTERING;
    """;

    public const string GetCurrentFuturesEodDataByDateRange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
        WHERE valueDate >= :startDate AND valueDate <= :endDate
        ALLOW FILTERING;
    """;

    public const string GetFuturesEodClosingPrices = """
        SELECT
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            closePrice AS "ClosingPrice"
        FROM futures_eod_data
        WHERE contractId = :contractId
        and valueDate >= :startDate
        AND valueDate <= :endDate
        AND symbol = :symbol
        ORDER BY valueDate DESC
        LIMIT :maxDays
        ALLOW FILTERING;
    """;

    public const string GetFuturesEodData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
        WHERE contractId = :contractId 
        AND valueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetFuturesIntraDayData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_intra_day_data
        WHERE contractId = :contractId 
        AND valueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetLastFuturesEodData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
        WHERE contractId = :contractId 
        AND valueDate < :valueDate 
        LIMIT 1;
    """;

    public const string GetFuturesEodDataAll = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
    """;


    public const string GetFuturesEodDataByDateRange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
        WHERE contractId = :contractId
        AND valueDate >= :startDate AND valueDate <= :endDate
        ORDER BY valueDate DESC;
    """;

    public const string GetFuturesEodMovingAverages = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            AVG(closePrice) AS "MovingAverage"
        FROM futures_eod_data
        WHERE 
            valueDate >= :startDate
            AND valueDate <= :endDate
            AND symbol = :symbol
        GROUP BY contractId, valueDate
        ALLOW FILTERING;
    """;

    public const string GetFuturesDataId = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate"
        FROM futures_eod_data
        WHERE 
            contractId = :contractId
            AND valueDate = :valueDate;
    """;

    public const string UpdateFuturesEodData = """
        UPDATE futures_eod_data
        SET
            openPrice = :openPrice,
            highPrice = :highPrice,
            lowPrice = :lowPrice,
            closePrice = :closePrice,
            volume = :volume,
            dailyPercentChange = :dailyPercentChange,
            dailyStdDev = :dailyStdDev,
            dailyStdDevAmount = :dailyStdDevAmount,
            upperBand = :upperBand,
            mean = :mean,
            lowerBand = :lowerBand,
            marketDirection = :marketDirection,
            marketVolatility = :marketVolatility,
            priceDirection = :priceDirection,
            priceVolatility = :priceVolatility,
            marketDirectionIndicator = :marketDirectionIndicator,
            windowSize = :windowSize
        WHERE
            contractId = :contractId
            AND valueDate = :valueDate
            AND symbol = :symbol;
    """;

    public const string GetYesterdaysFuturesEodData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            symbol AS "Symbol",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume",
            dailyPercentChange AS "DailyPercentChange",
            dailyStdDev AS "DailyStdDev",
            dailyStdDevAmount AS "DailyStdDevAmount",
            upperBand AS "UpperBand",
            mean AS "Mean",
            lowerBand AS "LowerBand",
            marketDirection AS "MarketDirection",
            marketVolatility AS "MarketVolatility",
            priceDirection AS "PriceDirection",
            priceVolatility AS "PriceVolatility",
            marketDirectionIndicator AS "MarketDirectionIndicator",
            windowSize AS "WindowSize" 
        FROM futures_eod_data
        WHERE valueDate < :valueDate 
        LIMIT 1
        ALLOW FILTERING;
    """;

    public const string GetFuturesTickHLVData = """
        SELECT 
            ContractId as "ContractId", 
            ValueDate as "ValueDate", 
            max(price) as "HighPrice",
            min(price) as "LowPrice",
            sum(size) as "Volume"
        FROM futures_tick_data
        WHERE ContractId = :contractId
        AND ValueDate = :valueDate
        GROUP BY ContractId, ValueDate;
    """;

    public const string CreateFuturesEodDataIndexTable = """
        CREATE TABLE IF NOT EXISTS futures_eod_data_index(
            valueDate date,
            contractId text,
            PRIMARY KEY (valueDate, contractId)
        );
    """;

    public const string InsertFuturesEodDataIndex = """
        INSERT INTO futures_eod_data_index (valueDate, contractId)
        VALUES (:valueDate, :contractId);
    """;

    public const string GetCurrentFuturesEodDataIndex = """
        SELECT 
            valueDate as "ValueDate",
            contractId as "ContractId"
        FROM futures_eod_data_index
        WHERE token(valueDate) <= token(:valueDate);
    """;

    public const string InsertFuturesItiSignal = """
        INSERT INTO futures_iti_signal (
            contractId, 
            valueDate, 
            timePeriod, 
            sequenceId, 
            intrinsicTime, 
            intrinsicTimeGroupId,
            intrinsicTimeLength,
            intrinsicPrice, 
            intrinsicTimeTrend, 
            intrinsicTimeMode, 
            trendPrice,
            trendExtreme,
            trendReversal, 
            trendDelta,
            targetDelta,
            lambda, 
            tradingDays,
            threshold,
            upTrendTrigger, 
            downTrendTrigger, 
            tradeState 
        ) VALUES (
            :contractId, 
            :valueDate, 
            :timePeriod, 
            :sequenceId, 
            :intrinsicTime, 
            :intrinsicTimeGroupId, 
            :intrinsicTimeLength, 
            :intrinsicPrice, 
            :intrinsicTimeTrend, 
            :intrinsicTimeMode, 
            :trendPrice, 
            :trendExtreme, 
            :trendReversal, 
            :trendDelta,
            :targetDelta, 
            :lambda, 
            :tradingDays, 
            :threshold,
            :upTrendTrigger,
            :downTrendTrigger, 
            :tradeState
        );
    """;

    public const string GetFuturesItiSignals = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta",
            lambda AS "Lambda",
            tradingDays AS "TradingDays",
            threshold AS "Threshold",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            tradeState AS "TradeState"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND timePeriod = :timePeriod;
    """;

    public const string GetFuturesItiSignalsByDateRange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta",
            lambda AS "Lambda",
            tradingDays AS "TradingDays",
            threshold AS "Threshold",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            tradeState AS "TradeState"
        FROM futures_iti_signal
        WHERE contractId IN :contractIds 
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalsByDateRangeIndex = """
        SELECT 
            valueDate AS "ValueDate",
            contractId AS "ContractId"
        FROM futures_iti_signal_index
        WHERE token(valueDate) >= token(:startDate) AND token(valueDate) <= token(:endDate);
    """;

    public const string CreateFuturesItiSignalIndexTable = """
        CREATE TABLE IF NOT EXISTS futures_iti_signal_index(
            valueDate date,
            contractId text,
            PRIMARY KEY (valueDate, contractId)
        );
    """;

    public const string InsertFuturesItiSignalIndex = """
        INSERT INTO futures_iti_signal_index (valueDate, contractId)
        VALUES (:valueDate, :contractId);
    """;

    public const string GetFuturesItiSignalMaxTrendValueDate = """
        SELECT max(ValueDate) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate <=  :valueDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiAvgTrendMDI = """
        SELECT avg(FuturesMDI) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate = :maxUpTrendValueDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalMaxTrendSequenceId = """
        SELECT max(SequenceId) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate <= :maxTrendValueDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        AND IntrinsicTimeMode = :intrinsicTimeMode
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalMaxValueDate = """
        SELECT max(ValueDate) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate < :valueDate;
    """;

    public const string GetFuturesItiSignalMDI = """
        SELECT 
            ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            IntrinsicTime AS "IntrinsicTime",
            IntrinsicTimeTrend AS "TrendType",
            FuturesMDI AS "MDI"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate = :maxValueDate
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND FuturesRSI > 0
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalMaxValueDateByTrend = """
        SELECT max(ValueDate) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate <= :valueDate
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalMaxTimeGroupId = """
        SELECT max(IntrinsicTimeGroupId) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate = :maxValueDate
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        Allow Filtering;
    """;

    public const string GetFuturesItiSignalMDIByTrend = """
        SELECT 
            ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            IntrinsicTime AS "IntrinsicTime",
            IntrinsicTimeTrend AS "TrendType",
            FuturesMDI AS "MDI"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate = :maxValueDate
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalTrendDeltaData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta",
            lambda AS "Lambda",
            tradingDays AS "TradingDays",
            threshold AS "Threshold",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            tradeState AS "TradeState"
        FROM futures_iti_signal
        WHERE contractId IN :contractIds 
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        AND intrinsicTimeMode IN :intrinsicTimeModes
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalTrendClassData = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta",
            lambda AS "Lambda",
            tradingDays AS "TradingDays",
            threshold AS "Threshold",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            tradeState AS "TradeState"
        FROM futures_iti_signal
        WHERE contractId IN :contractIds 
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        AND intrinsicTimeMode IN :intrinsicTimeModes
        ALLOW FILTERING;
    """;

    public const string CreateFuturesItiTrendClassDataTable = """
        CREATE TABLE IF NOT EXISTS futures_iti_trend_class_data (
            symbol TEXT,
            valueDate DATE,
            timestamp TIMESTAMP,
            sequenceId BIGINT,
            trendClass BOOLEAN,
            trendDirection FLOAT,
            trendDirectionMode FLOAT,
            trendDelta FLOAT,
            futuresRSI FLOAT,
            PRIMARY KEY (symbol, valueDate, sequenceId)
        ) WITH CLUSTERING ORDER BY (valueDate ASC, sequenceId ASC);
    """;

    public const string InsertFuturesItiTrendClassData = """
        INSERT INTO futures_iti_trend_class_data (
            symbol,
            valueDate,
            timestamp,
            sequenceId,
            trendClass,
            trendDirection,
            trendDirectionMode,
            trendDelta,
            futuresRSI
        ) VALUES (
            :symbol,
            :valueDate,
            :timestamp,
            :sequenceId,
            :trendClass,
            :trendDirection,
            :trendDirectionMode,
            :trendDelta,
            :futuresRSI
        );
    """;

    public const string GetFuturesItiTrendClassData = """
        SELECT symbol AS "Symbol",
            valueDate AS "ValueDate",
            timestamp AS "Timestamp",
            sequenceId AS "SequenceId",
            trendClass AS "TrendClass",
            trendDirection AS "TrendDirection",
            trendDirectionMode AS "TrendDirectionMode",
            trendDelta AS "TrendDelta",
            futuresRSI AS "FuturesRSI"
        FROM futures_iti_trend_class_data
        WHERE symbol = :symbol
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        ORDER BY valueDate ASC, sequenceId ASC;
    """;

    public const string DeleteFuturesItiTrendClassData = """
        DELETE FROM futures_iti_trend_class_data
        WHERE symbol = :symbol
        AND valueDate >= :startDate
        AND valueDate <= :endDate;
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

    public const string InsertFuturesItiTrendDeltaData = """
        INSERT INTO futures_iti_trend_delta_data (
            symbol, valueDate, timestamp, sequenceId, trendDelta, trendDirection, trendDirectionMode, futuresPrice, trendExtreme, futuresRSI
        ) VALUES (
            :symbol, :valueDate, :timestamp, :sequenceId, :trendDelta, :trendDirection, :trendDirectionMode, :futuresPrice, :trendExtreme, :futuresRSI
        );
    """;

    public const string GetFuturesItiTrendDeltaData = """
        SELECT symbol AS "Symbol",
            valueDate AS "ValueDate",
            timestamp AS "Timestamp",
            sequenceId AS "SequenceId",
            trenddelta AS "TrendDelta",
            trenddirection AS "TrendDirection",
            trenddirectionmode AS "TrendDirectionMode",
            futuresprice AS "FuturesPrice",
            trendextreme AS "TrendExtreme",
            futuresrsi AS "FuturesRSI"
        FROM futures_iti_trend_delta_data
        WHERE symbol = :symbol
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        ORDER BY valueDate ASC, sequenceId ASC;
    """;

    public const string DeleteFuturesItiTrendDeltaData = """
        DELETE FROM futures_iti_trend_delta_data
        WHERE symbol = :symbol
        AND valueDate >= :startDate
        AND valueDate <= :endDate;
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

    public const string InsertFuturesItiTrendClassModel = """
        INSERT INTO futures_iti_trend_class_model (
            symbol,
            valueDate,
            startDate,
            endDate,
            count,
            maximum,
            mean,
            median,
            minimum,
            skewness,
            stdDev,
            variance,
            accuracy,
            areaUnderPrecisionRecallCurve,
            areaUnderRocCurve,
            entropy,
            f1Score,
            modelData
        ) VALUES (
            :symbol,
            :valueDate,
            :startDate,
            :endDate,
            :count,
            :maximum,
            :mean,
            :median,
            :minimum,
            :skewness,
            :stdDev,
            :variance,
            :accuracy,
            :areaUnderPrecisionRecallCurve,
            :areaUnderRocCurve,
            :entropy,
            :f1Score,
            :modelData
        );
    """;

    public const string GetFuturesItiTrendDeltaModel = """
        SELECT 
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            startDate AS "StartDate",
            endDate AS "EndDate",
            count AS "Count",
            maximum AS "Maximum",
            mean AS "Mean",
            median AS "Median",
            minimum AS "Minimum",
            skewness AS "Skewness",
            stdDev AS "StdDev",
            variance AS "Variance",
            meanAbsoluteError AS "MeanAbsoluteError",
            meanSquaredError AS "MeanSquaredError",
            rootMeanSquaredError AS "RootMeanSquaredError",
            lossFunction AS "LossFunction",
            rSquared AS "RSquared",
            modelData AS "ModelData"
        FROM futures_iti_trend_delta_model
        WHERE symbol = :symbol AND valueDate = :valueDate;
    """;

    public const string GetFuturesItiTrendClassModel = """
        SELECT symbol AS "Symbol",
            valueDate AS "ValueDate",
            startdate AS "StartDate",
            enddate AS "EndDate",
            count AS "Count",
            maximum AS "Maximum",
            mean AS "Mean",
            median AS "Median",
            minimum AS "Minimum",
            skewness AS "Skewness",
            stddev AS "StdDev",
            variance AS "Variance",
            accuracy AS "Accuracy",
            areaunderprecisionrecallcurve AS "AreaUnderPrecisionRecallCurve",
            areaunderroccurve AS "AreaUnderRocCurve",
            entropy AS "Entropy",
            f1score AS "F1Score",
            modeldata AS "ModelData"
        FROM futures_iti_trend_class_model
        WHERE symbol = :symbol
        AND valueDate = :valueDate;
    """;

    public const string GetFuturesItiTrendClassModelMaxValueDate = """
        SELECT valueDate AS "Value"
        FROM futures_iti_trend_class_model
        WHERE symbol = :symbol
        AND valueDate <= :valueDate
        ORDER BY valueDate DESC
        LIMIT 1;
    """;

    public const string GetFuturesItiTrendDeltaModelMaxValueDate = """
        SELECT valueDate AS "Value"
        FROM futures_iti_trend_delta_model
        WHERE symbol = :symbol
        AND valueDate <= :valueDate
        ORDER BY valueDate DESC
        LIMIT 1;
    """;

    public const string InsertFuturesItiTrendDeltaModel = """
        INSERT INTO futures_iti_trend_delta_model (
            symbol,
            valueDate,
            startDate,
            endDate,
            count,
            maximum,
            mean,
            median,
            minimum,
            skewness,
            stdDev,
            variance,
            meanAbsoluteError,
            meanSquaredError,
            rootMeanSquaredError,
            lossFunction,
            rSquared,
            modelData
        ) VALUES (
            :symbol,
            :valueDate,
            :startDate,
            :endDate,
            :count,
            :maximum,
            :mean,
            :median,
            :minimum,
            :skewness,
            :stdDev,
            :variance,
            :meanAbsoluteError,
            :meanSquaredError,
            :rootMeanSquaredError,
            :lossFunction,
            :rSquared,
            :modelData
        );
    """;

    public const string GetFuturesItiTrendDirectionChangedSignals = """
        SELECT contractid AS "ContractId",
            valuedate AS "ValueDate",
            intrinsictime AS "IntrinsicTime",
            intrinsictimegroupid AS "IntrinsicTimeGroupId",
            sequenceid AS "SequenceId",
            intrinsictimelength AS "IntrinsicTimeLength",
            intrinsicprice AS "IntrinsicPrice",
            intrinsictimetrend AS "IntrinsicTimeTrend",
            intrinsictimemode AS "IntrinsicTimeMode",
            trendprice AS "TrendPrice",
            trendextreme AS "TrendExtreme",
            trendreversal AS "TrendReversal",
            lambda AS "Lambda",
            targetdelta AS "TargetDelta",
            predicteddelta AS "PredictedDelta",
            trenddelta AS "TrendDelta",
            uptrendtrigger AS "UpTrendTrigger",
            downtrendtrigger AS "DownTrendTrigger",
            futurespercentchange AS "FuturesPercentChange",
            futuresmean AS "FuturesMean",
            futuresstddev AS "FuturesStdDev",
            futuresmdi AS "FuturesMDI",
            futuresmditrend AS "FuturesMDITrend",
            futuresmdiuptrendlimit AS "FuturesMDIUpTrendLimit",
            futuresmdidowntrendlimit AS "FuturesMDIDownTrendLimit",
            futuresrsi AS "FuturesRSI",
            futuresrsislope AS "FuturesRSISlope",
            futuresfiftydma AS "FuturesFiftyDMA",
            futurestwohundreddma AS "FuturesTwoHundredDMA",
            tradestate AS "TradeState",
            uptrendcoastlinecounter AS "UpTrendCoastLineCounter",
            downtrendcoastlinecounter AS "DownTrendCoastLineCounter"
        FROM futures_iti_signal
        WHERE contractid = :contractid
        AND valuedate = :valuedate
        AND intrinsictimemode = 'TrendDirectionChanged'
        Allow Filtering;
    """;

    public const string InsertFuturesRsiSignal = """
        INSERT INTO futures_rsi_signal (
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            timestamp,
            price,
            priceChange,
            priceGain,
            priceLoss,
            averagePriceGain,
            averagePriceLoss,
            rs,
            rsi,
            rsiAverage,
            rsiSlope
        ) VALUES (
            :contractId,
            :valueDate,
            :timePeriod,
            :periodLength,
            :timestamp,
            :price,
            :priceChange,
            :priceGain,
            :priceLoss,
            :averagePriceGain,
            :averagePriceLoss,
            :rs,
            :rsi,
            :rsiAverage,
            :rsiSlope
        );
    """;

    public const string DeleteFuturesRsiSignal = """
        DELETE FROM futures_rsi_signal
        WHERE contractId = :contractId
        AND valueDate = :valueDate;
    """;

    public const string GetFuturesRsiSignalUpTrendCount = """
        SELECT count(*) AS "Value"
        FROM futures_rsi_signal
        WHERE contractid = :contractid
        AND valuedate = :valuedate
        AND timestamp >= :startTime
        AND timestamp <= :endTime
        AND rsi >= 50;
    """;

    public const string GetFuturesRsiSignalDownTrendCount = """
        SELECT count(*) AS "Value"
        FROM futures_rsi_signal
        WHERE contractid = :contractid
        AND valuedate = :valuedate
        AND timestamp >= :startTime
        AND timestamp <= :endTime
        AND rsi < 50;
    """;

    public const string GetLastFuturesItiSignal = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta",
            lambda AS "Lambda",
            tradingDays AS "TradingDays",
            threshold AS "Threshold",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            tradeState AS "TradeState"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetLastFuturesItiSignalTrendDirectionChange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            lambda AS "Lambda",
            targetDelta AS "TargetDelta",
            predictedDelta AS "PredictedDelta",
            trendDelta AS "TrendDelta",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            futuresPercentChange AS "FuturesPercentChange",
            futuresMean AS "FuturesMean",
            futuresStdDev AS "FuturesStdDev",
            futuresMDI AS "FuturesMDI",
            futuresMDITrend AS "FuturesMDITrend",
            futuresMDIUpTrendLimit AS "FuturesMDIUpTrendLimit",
            futuresMDIDownTrendLimit AS "FuturesMDIDownTrendLimit",
            futuresRSI AS "FuturesRSI",
            futuresRSISlope AS "FuturesRSISlope",
            futuresFiftyDMA AS "FuturesFiftyDMA",
            futuresTwoHundredDMA AS "FuturesTwoHundredDMA",
            tradeState AS "TradeState",
            upTrendCoastLineCounter AS "UpTrendCoastLineCounter",
            downTrendCoastLineCounter AS "DownTrendCoastLineCounter"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND intrinsicTimeMode = 'TrendDirectionChanged'
        LIMIT 1
        Allow Filtering;
    """;

    public const string CreateFuturesItiSignal_IntrinsicTimeModeIndex = """
        CREATE INDEX IF NOT EXISTS futures_iti_signal_intrinsictimemode ON futures_iti_signal(intrinsicTimeMode);
    """;

    public const string GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged = """
        SELECT sequenceid AS "Value"
        FROM futures_iti_signal
        WHERE contractid = :contractid
        AND valuedate = :valuedate
        AND intrinsictimemode = 'TrendDirectionChanged'
        LIMIT 1
        Allow Filtering;
    """;

    public const string GetLastFuturesItiSignalTrendExtremeChange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            lambda AS "Lambda",
            targetDelta AS "TargetDelta",
            predictedDelta AS "PredictedDelta",
            trendDelta AS "TrendDelta",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            futuresPercentChange AS "FuturesPercentChange",
            futuresMean AS "FuturesMean",
            futuresStdDev AS "FuturesStdDev",
            futuresMDI AS "FuturesMDI",
            futuresMDITrend AS "FuturesMDITrend",
            futuresMDIUpTrendLimit AS "FuturesMDIUpTrendLimit",
            futuresMDIDownTrendLimit AS "FuturesMDIDownTrendLimit",
            futuresRSI AS "FuturesRSI",
            futuresRSISlope AS "FuturesRSISlope",
            futuresFiftyDMA AS "FuturesFiftyDMA",
            futuresTwoHundredDMA AS "FuturesTwoHundredDMA",
            tradeState AS "TradeState",
            upTrendCoastLineCounter AS "UpTrendCoastLineCounter",
            downTrendCoastLineCounter AS "DownTrendCoastLineCounter"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND SequenceId > :lastTrendDirectionChangedSequenceId
        AND intrinsicTimeMode = 'TrendExtremeChanged'
        LIMIT 1
        Allow Filtering;
    """;

    public const string GetLastFuturesItiSignalTrendReversalChange = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime",
            intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength",
            intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend",
            intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            lambda AS "Lambda",
            targetDelta AS "TargetDelta",
            predictedDelta AS "PredictedDelta",
            trendDelta AS "TrendDelta",
            upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger",
            futuresPercentChange AS "FuturesPercentChange",
            futuresMean AS "FuturesMean",
            futuresStdDev AS "FuturesStdDev",
            futuresMDI AS "FuturesMDI",
            futuresMDITrend AS "FuturesMDITrend",
            futuresMDIUpTrendLimit AS "FuturesMDIUpTrendLimit",
            futuresMDIDownTrendLimit AS "FuturesMDIDownTrendLimit",
            futuresRSI AS "FuturesRSI",
            futuresRSISlope AS "FuturesRSISlope",
            futuresFiftyDMA AS "FuturesFiftyDMA",
            futuresTwoHundredDMA AS "FuturesTwoHundredDMA",
            tradeState AS "TradeState",
            upTrendCoastLineCounter AS "UpTrendCoastLineCounter",
            downTrendCoastLineCounter AS "DownTrendCoastLineCounter"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND SequenceId > :lastTrendDirectionChangedSequenceId
        AND intrinsicTimeMode = 'TrendReversalChanged'
        Limit 1
        Allow Filtering;
    """;

    public const string GetLastFuturesOptionTickData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId AS "TickId", 
            tickTime AS "TickTime", 
            optionPrice AS "OptionPrice", 
            bidPrice AS "BidPrice", 
            askPrice AS "AskPrice", 
            bidSize AS "BidSize", 
            askSize AS "AskSize", 
            impliedVolatility AS "ImpliedVolatility", 
            underlyingPrice AS "UnderlyingPrice", 
            delta AS "Delta", 
            gamma AS "Gamma", 
            vega AS "Vega", 
            theta AS "Theta", 
            rho AS "Rho"
        FROM futures_option_tick_data
        WHERE contractId = :contractId 
        AND valueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetLastFuturesOptionTickPriceData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            tickId AS "TickId", 
            tickTime AS "TickTime", 
            optionPrice AS "OptionPrice", 
            bidPrice AS "BidPrice", 
            askPrice AS "AskPrice", 
            bidSize AS "BidSize", 
            askSize AS "AskSize", 
            impliedVolatility AS "ImpliedVolatility", 
            underlyingPrice AS "UnderlyingPrice", 
            delta AS "Delta", 
            gamma AS "Gamma", 
            vega AS "Vega", 
            theta AS "Theta", 
            rho AS "Rho"
        FROM futures_option_tick_price_data
        WHERE contractId = :contractId 
        AND valueDate = :valueDate 
        LIMIT 1;
    """;


    public const string CreateFuturesRsiSignal_SignalTypeIndex = """
        CREATE INDEX IF NOT EXISTS futures_rsi_signal_signaltype ON futures_rsi_signal(signalType);
    """;

    public const string GetLastFuturesRsiSignal = """
        SELECT contractid AS "ContractId",
            valuedate AS "ValueDate",
            timePeriod AS "TimePeriod",
            periodLength AS "PeriodLength",
            timestamp AS "Timestamp",
            futuresPrice AS "FuturesPrice",
            pricechange AS "PriceChange",
            pricegain AS "PriceGain",
            priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain",
            averagepriceloss AS "AveragePriceLoss",
            rs AS "RS",
            rsi AS "RSI",
            rsiaverage AS "RSIAverage",
            rsislope AS "RSISlope",
            windowsize AS "WindowSize"
        FROM futures_rsi_signal
        WHERE contractid = :contractId
        AND timePeriod = :timePeriod
        AND periodLength = :periodLength
        AND valuedate = :valueDate
        LIMIT 1;
    """;

    public const string GetLastFuturesRsiDailySignal = """
        SELECT contractid AS "ContractId",
            valuedate AS "ValueDate",
            timePeriod AS "TimePeriod",
            periodLength AS "PeriodLength",
            timestamp AS "Timestamp",
            futuresPrice AS "FuturesPrice",
            pricechange AS "PriceChange",
            pricegain AS "PriceGain",
            priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain",
            averagepriceloss AS "AveragePriceLoss",
            rs AS "RS",
            rsi AS "RSI",
            rsiaverage AS "RSIAverage",
            rsislope AS "RSISlope",
            windowsize AS "WindowSize"
        FROM futures_rsi_signal
           WHERE contractid = :contractId
        AND timePeriod = :timePeriod
        AND periodLength = :periodLength
        LIMIT 1;
    """;

    public const string InsertFuturesTdiSignal = """
        INSERT INTO futures_tdi_signal (
            contractId,
            valueDate,  
            timePeriod,
            timestamp,
            upTrendCount,
            downTrendCount,
            tdi,
            tdiStrength
        ) VALUES (
            :contractId,
            :valueDate,
            :timePeriod,
            :timestamp,
            :upTrendCount,
            :downTrendCount,
            :tdi,
            :tdiStrength
        );
    """;

    public const string DeleteFuturesTdiSignal = """
        DELETE FROM futures_tdi_signal
        WHERE contractId = :contractId
        AND valueDate = :valueDate
        AND timestamp = :timestamp IF EXISTS;
    """;

    public const string GetLastFuturesTdiSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            Timestamp AS "Timestamp",
            UpTrendCount AS "UpTrendCount",
            DownTrendCount AS "DownTrendCount",
            TDI AS "TDI",
            TDIStrength AS "TDIStrength"
        FROM futures_tdi_signal
        WHERE ContractId = :contractId AND ValueDate = :valueDate LIMIT 1;
    """;

    public const string InsertFuturesTradeSignal = """
        INSERT INTO futures_trade_signal (
            contractId,
            valueDate,
            sequenceId,
            timestamp,
            mean,
            stdDev,
            futuresPrice,
            priceChangePercent,
            fundRiskPercent,
            rsi,
            rsiSlope,
            trendType,
            trendStrength,
            tradeSignal,
            tdi,
            tdiStrength,
            mdi,
            mdiTrend,
            mdiUpTrendLimit,
            mdiDownTrendLimit,
            upTrendingTrigger,
            downTrendingTrigger,
            entryTrigger,
            exitTrigger,
            trendDelta,
            trendExtreme,
            trendReversal,
            fiftyDMA,
            twoHundredDMA,
            tradeExecuteState
        ) VALUES (
            :contractId,
            :valueDate,
            :sequenceId,
            :timestamp,
            :mean,
            :stdDev,
            :futuresPrice,
            :priceChangePercent,
            :fundRiskPercent,
            :rsi,
            :rsiSlope,
            :trendType,
            :trendStrength,
            :tradeSignal,
            :tdi,
            :tdiStrength,
            :mdi,
            :mdiTrend,
            :mdiUpTrendLimit,
            :mdiDownTrendLimit,
            :upTrendingTrigger,
            :downTrendingTrigger,
            :entryTrigger,
            :exitTrigger,
            :trendDelta,
            :trendExtreme,
            :trendReversal,
            :fiftyDMA,
            :twoHundredDMA,
            :tradeExecuteState
        );
    """;

    public const string GetLastFuturesTradeSignalById = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            timestamp AS "Timestamp",
            mean AS "Mean",
            stdDev AS "StdDev",
            futuresPrice AS "FuturesPrice",
            priceChangePercent AS "PriceChangePercent",
            fundRiskPercent AS "FundRiskPercent",
            rsi AS "RSI",
            rsiSlope AS "RSISlope",
            trendType AS "TrendType",
            trendStrength AS "TrendStrength",
            tradeSignal AS "TradeSignal",
            tdi AS "TDI",
            tdiStrength AS "TDIStrength",
            mdi AS "MDI",
            mdiTrend AS "MDITrend",
            mdiUpTrendLimit AS "MDIUpTrendLimit",
            mdiDownTrendLimit AS "MDIDownTrendLimit",
            upTrendingTrigger AS "UpTrendingTrigger",
            downTrendingTrigger AS "DownTrendingTrigger",
            entryTrigger AS "EntryTrigger",
            exitTrigger AS "ExitTrigger",
            trendDelta AS "TrendDelta",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA",
            tradeExecuteState AS "TradeExecuteState"
        FROM futures_trade_signal
        WHERE contractId = :contractId AND valueDate = :valueDate
        LIMIT 1;
    """;

    public const string GetLastFuturesTradeSignal = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            timestamp AS "Timestamp",
            mean AS "Mean",
            stdDev AS "StdDev",
            futuresPrice AS "FuturesPrice",
            priceChangePercent AS "PriceChangePercent",
            fundRiskPercent AS "FundRiskPercent",
            rsi AS "RSI",
            rsiSlope AS "RSISlope",
            trendType AS "TrendType",
            trendStrength AS "TrendStrength",
            tradeSignal AS "TradeSignal",
            tdi AS "TDI",
            tdiStrength AS "TDIStrength",
            mdi AS "MDI",
            mdiTrend AS "MDITrend",
            mdiUpTrendLimit AS "MDIUpTrendLimit",
            mdiDownTrendLimit AS "MDIDownTrendLimit",
            upTrendingTrigger AS "UpTrendingTrigger",
            downTrendingTrigger AS "DownTrendingTrigger",
            entryTrigger AS "EntryTrigger",
            exitTrigger AS "ExitTrigger",
            trendDelta AS "TrendDelta",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA",
            tradeExecuteState AS "TradeExecuteState"
        FROM futures_trade_signal
        LIMIT 1;
    """;

    public const string GetFuturesTradeSignalAll = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            timestamp AS "Timestamp",
            mean AS "Mean",
            stdDev AS "StdDev",
            futuresPrice AS "FuturesPrice",
            priceChangePercent AS "PriceChangePercent",
            fundRiskPercent AS "FundRiskPercent",
            rsi AS "RSI",
            rsiSlope AS "RSISlope",
            trendType AS "TrendType",
            trendStrength AS "TrendStrength",
            tradeSignal AS "TradeSignal",
            tdi AS "TDI",
            tdiStrength AS "TDIStrength",
            mdi AS "MDI",
            mdiTrend AS "MDITrend",
            mdiUpTrendLimit AS "MDIUpTrendLimit",
            mdiDownTrendLimit AS "MDIDownTrendLimit",
            upTrendingTrigger AS "UpTrendingTrigger",
            downTrendingTrigger AS "DownTrendingTrigger",
            entryTrigger AS "EntryTrigger",
            exitTrigger AS "ExitTrigger",
            trendDelta AS "TrendDelta",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA",
            tradeExecuteState AS "TradeExecuteState"
        FROM futures_trade_signal;
    """;

    public const string GetLastFuturesTradeSignalBySymbol = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId",
            timestamp AS "Timestamp",
            mean AS "Mean",
            stdDev AS "StdDev",
            futuresPrice AS "FuturesPrice",
            priceChangePercent AS "PriceChangePercent",
            fundRiskPercent AS "FundRiskPercent",
            rsi AS "RSI",
            rsiSlope AS "RSISlope",
            trendType AS "TrendType",
            trendStrength AS "TrendStrength",
            tradeSignal AS "TradeSignal",
            tdi AS "TDI",
            tdiStrength AS "TDIStrength",
            mdi AS "MDI",
            mdiTrend AS "MDITrend",
            mdiUpTrendLimit AS "MDIUpTrendLimit",
            mdiDownTrendLimit AS "MDIDownTrendLimit",
            upTrendingTrigger AS "UpTrendingTrigger",
            downTrendingTrigger AS "DownTrendingTrigger",
            entryTrigger AS "EntryTrigger",
            exitTrigger AS "ExitTrigger",
            trendDelta AS "TrendDelta",
            trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA",
            tradeExecuteState AS "TradeExecuteState"
        FROM futures_trade_signal
        WHERE contractId IN :contractIds 
        AND valueDate = :valueDate
        LIMIT 1;
    """;

    public const string CreateRateOfReturn = """
        CREATE TABLE IF NOT EXISTS rate_of_return (
            symbol TEXT,
            valueDate DATE,
            rateOfReturn DOUBLE,
            PRIMARY KEY (symbol, valueDate)
        ) WITH CLUSTERING ORDER BY (valueDate DESC);
    """;

    public const string GetLastRateOfReturn = """
        SELECT 
            symbol AS "Symbol", 
            valueDate AS "ValueDate", 
            rateOfReturn AS "RateOfReturn"
        FROM rate_of_return
        WHERE symbol = :symbol
        LIMIT 1;
    """;

    public const string InsertRateOfReturn = """
        INSERT INTO rate_of_return (symbol, valueDate, rateOfReturn)
        VALUES (:symbol, :valueDate, :rateOfReturn);
    """;

    public const string InsertVixFuturesEodData = """
        INSERT INTO vix_futures_eod_data (contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume)
        VALUES (:contractId, :valueDate, :price, :price, :price, :price, :size);
    """;

    public const string GetLastVixFuturesEodData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            openPrice AS "OpenPrice", 
            highPrice AS "HighPrice", 
            lowPrice AS "LowPrice", 
            closePrice AS "ClosePrice", 
            volume AS "Volume"
        FROM 
            vix_futures_eod_data
        WHERE 
            contractId = :contractId 
            AND valueDate <= :valueDate
        LIMIT 1;
    """;

    public const string GetVixFuturesEodData = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            openPrice AS "OpenPrice", 
            highPrice AS "HighPrice", 
            lowPrice AS "LowPrice", 
            closePrice AS "ClosePrice", 
            volume AS "Volume"
        FROM 
            vix_futures_eod_data
        WHERE 
            contractId = :contractId 
            AND valueDate = :valueDate
        LIMIT 1;
    """;

    public const string GetMinFuturesTickDataTickId = """
        SELECT MIN(tickId) AS "Value" 
        FROM futures_tick_data 
        WHERE ContractId = :contractId AND ValueDate = :valueDate;
    """;

    public const string GetFuturesTickDataPriceByTickId = """
        SELECT Price as "Value"
        FROM futures_tick_data 
        WHERE ContractId = :contractId 
        AND ValueDate = :valueDate 
        AND TickId = :tickId LIMIT 1;
    """;

    public const string UpdateVixFuturesEodData = """
        UPDATE vix_futures_eod_data
        SET
            openPrice = :openPrice,
            highPrice = :highPrice,
            lowPrice = :lowPrice,
            closePrice = :closePrice,
            volume = :volume
        WHERE
            contractId = :contractId
            AND valueDate = :valueDate;
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

    public const string InsertYieldCurveRate = """
        INSERT INTO yield_curve_rates (
            id,
            valueDate,
            oneMonth,
            twoMonth,
            threeMonth,
            sixMonth,
            oneYear,
            twoYear,
            threeYear,
            fiveYear,
            sevenYear,
            tenYear,
            twentyYear,
            thirtyYear
        ) VALUES (
            :id,
            :valueDate,
            :oneMonth,
            :twoMonth,
            :threeMonth,
            :sixMonth,
            :oneYear,
            :twoYear,
            :threeYear,
            :fiveYear,
            :sevenYear,
            :tenYear,
            :twentyYear,
            :thirtyYear
        );
    """;

    public const string DeleteYieldCurveRate = """
        DELETE FROM yield_curve_rates
        WHERE id = 1
        AND valueDate = :valueDate;
    """;

    public const string GetYieldCurveRate = """
        SELECT 
            valueDate AS "ValueDate",
            oneMonth AS "OneMonth",
            twoMonth AS "TwoMonth",
            threeMonth AS "ThreeMonth",
            sixMonth AS "SixMonth",
            oneYear AS "OneYear",
            twoYear AS "TwoYear",
            threeYear AS "ThreeYear",
            fiveYear AS "FiveYear",
            sevenYear AS "SevenYear",
            tenYear AS "TenYear",
            twentyYear AS "TwentyYear",
            thirtyYear AS "ThirtyYear"
        FROM yield_curve_rates 
        WHERE id = 1 AND valueDate = :valueDate;
    """;

    public const string GetAllYieldCurveRates = """
        SELECT 
            valueDate AS "ValueDate",
            oneMonth AS "OneMonth",
            twoMonth AS "TwoMonth",
            threeMonth AS "ThreeMonth",
            sixMonth AS "SixMonth",
            oneYear AS "OneYear",
            twoYear AS "TwoYear",
            threeYear AS "ThreeYear",
            fiveYear AS "FiveYear",
            sevenYear AS "SevenYear",
            tenYear AS "TenYear",
            twentyYear AS "TwentyYear",
            thirtyYear AS "ThirtyYear"
        FROM yield_curve_rates WHERE id = 1;
    """;

    public const string CreateYieldCurveRates_ValueDateIndex = """
        CREATE INDEX ON yield_curve_rates (valueDate);
    """;

    public const string GetYieldCurveRates = """
        SELECT 
            valueDate AS "ValueDate",
            oneMonth AS "OneMonth",
            twoMonth AS "TwoMonth",
            threeMonth AS "ThreeMonth",
            sixMonth AS "SixMonth",
            oneYear AS "OneYear",
            twoYear AS "TwoYear",
            threeYear AS "ThreeYear",
            fiveYear AS "FiveYear",
            sevenYear AS "SevenYear",
            tenYear AS "TenYear",
            twentyYear AS "TwentyYear",
            thirtyYear AS "ThirtyYear"
        FROM yield_curve_rates
        WHERE id = 1 AND valueDate >= :startDate
        AND valueDate <= :endDate;
    """;

    public const string GetMarketHolidays = """
        SELECT 
            currencyType AS "CurrencyType",
            holidayDate AS "HolidayDate",
            description AS "Description"
        FROM market_holiday
        WHERE currencyType = :currencyType;
    """;

    public const string DeleteMarketHoliday = """
        DELETE FROM market_holiday 
        WHERE currencyType = :currencyType AND holidayDate = :holidayDate 
        IF EXISTS;
    """;

    public const string GetMarketHolidaysByDateRange = """
        SELECT 
            currencyType AS "CurrencyType",
            holidayDate AS "HolidayDate",
            description AS "Description"
        FROM market_holiday
        WHERE currencyType = :currencyType AND holidayDate >= :startDate AND holidayDate <= :endDate;
    """;

    public const string InsertMarketHoliday = """
        INSERT INTO market_holiday (currencyType, holidayDate, description) 
        VALUES (:currencyType, :holidayDate, :description);
    """;

    public const string DeleteMarketHolidays = """
        DELETE FROM market_holiday 
        WHERE currencyType = :currencyType;
    """;

    public const string DeleteRateOfReturn = """
        DELETE FROM rate_of_return 
        WHERE symbol = :symbol AND valueDate = :valueDate
    """;

    public const string DeleteFuturesItiSignal = """
        DELETE FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND timePeriod = :timePeriod
    """;

    public const string GetMaxFuturesItiSignalValueDate = """
        SELECT max(ValueDate) AS "Value" 
        FROM futures_iti_signal 
        WHERE ContractId = :contractId
        AND ValueDate <= :valueDate 
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        ALLOW FILTERING;
    """;

    public const string GetMaxFuturesItiSignalSequenceId = """
        SELECT max(SequenceId) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate <= :valueDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        AND IntrinsicTimeMode = :intrinsicTimeMode
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalAverageInfo = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            avg(PredictedDelta) AS "PredictedDelta",
            avg(FuturesRSI) AS "FuturesRSI"
        FROM futures_iti_signal
        WHERE ContractId = :contractId
        AND ValueDate = :valueDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND SequenceId > :maxSequenceId
        AND FuturesRSI > -1
        GROUP BY contractId, valueDate
        ALLOW FILTERING;
    """;

    public const string GetFuturesItiSignalAvgPredictedDelta = """
        SELECT avg(PredictedDelta) AS "Value"
        FROM futures_iti_signal
        WHERE ContractId IN :contractIds
        AND ValueDate >= :startDate
        AND ValueDate <= :endDate
        AND IntrinsicTimeTrend = :intrinsicTimeTrend
        AND IntrinsicTimeMode IN :intrinsicTimeModes
        AND FuturesRSI > -1
        ALLOW FILTERING;
    """;

    public const string GetNormalCurveData = """
        SELECT 
            StdDevIndex AS "StdDevIndex",
            Percent AS "Percent"
        FROM normal_curve_data;
    """;

    public const string InsertFuturesOptionQuote = """
        INSERT INTO futures_option_quote (
            QuoteId,
            ContractId,
            RequestId,
            CreatedBy,
            CreatedOn
        ) VALUES (
            :quoteId,
            :contractId,
            :requestId,
            :createdBy,
            :createdOn
        );
    """;

    public const string InsertFuturesOptionQuoteData = """
        INSERT INTO futures_option_quote_data (
            QuoteId,
            ContractId,
            RequestId,
            BidPrice,
            BidSize,
            AskPrice,
            AskSize
        ) VALUES (
            :quoteId,
            :contractId,
            :requestId,
            :bidPrice,
            :bidSize,
            :askPrice,
            :askSize
        );
    """;

    public const string DeleteFuturesOptionQuotes = """
        DELETE FROM futures_option_quote
        WHERE QuoteId = :quoteId;
    """;

    public const string InsertTradeLiveFeed = """
        INSERT INTO trade_live_feed (
            OrderId,
            TradeId,
            TradeLiveFeedState
        ) VALUES (
            :orderId,
            :tradeId,
            :tradeLiveFeedState
        );
    """;

    public const string GetTradeLiveFeed = """
        SELECT OrderId, TradeId, TradeLiveFeedState
        FROM trade_live_feed
        WHERE orderId = :orderId 
        AND tradeId = :tradeId;
    """;

    public const string DeleteTradeLiveFeed = """
        DELETE FROM trade_live_feed
        WHERE orderId = :orderId 
        AND tradeId = :tradeId;
    """;

    public const string DeleteFuturesOptionQuoteData = """
        DELETE FROM futures_option_quote_data
        WHERE quoteId = :quoteId;
    """;

    public const string GetFuturesTradeSignalIdByValueDate = """
        SELECT ContractId, MAX(ValueDate) AS ValueDate
        FROM futures_trade_signal
        WHERE ValueDate = :valueDate
        AND TimePeriod = :timePeriod
        GROUP BY ContractId;
    """;

    public const string GetVixFuturesEodDataByValueDate = """
        SELECT 
            ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            OpenPrice AS "OpenPrice",
            HighPrice AS "HighPrice",
            LowPrice AS "LowPrice",
            ClosePrice AS "ClosePrice",
            Volume AS "Volume"
        FROM vix_futures_eod_data
        WHERE ValueDate <= :valueDate
        ORDER BY ValueDate DESC ALLOW FILTERING;
    """;

    public const string InsertFuturesMacdSignal = """
        INSERT INTO futures_macd_signal (
            contractId,
            valueDate,
            timestamp,
            macdLine,
            signalLine,
            histogram,
            macd,
            macdStrength
        ) VALUES (
            :contractId,
            :valueDate,
            :timestamp,
            :macdLine,
            :signalLine,
            :histogram,
            :macd,
            :macdStrength
        );
    """;

    public const string GetLastFuturesMacdSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            Timestamp AS "Timestamp",
            MacdLine AS "MacdLine",
            SignalLine AS "SignalLine",
            Histogram AS "Histogram",
            MACD AS "MACD",
            MACDStrength AS "MACDStrength"
        FROM futures_macd_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        AND ValueDate = :valueDate LIMIT 1;
    """;

    public const string GetLastFuturesMacdDailySignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            FuturesPrice as "FuturesPrice",
            MacdLine AS "MacdLine",
            SignalLine AS "SignalLine",
            Histogram AS "Histogram",
            MACD AS "MACD",
            MACDStrength AS "MACDStrength"
        FROM futures_macd_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        LIMIT 1;
    """;

    public const string InsertFuturesAtrSignal = """
        INSERT INTO futures_atr_signal (
            contractId,
            valueDate,
            timestamp,
            atrValue,
            trueRange,
            atr,
            atrStrength
        ) VALUES (
            :contractId,
            :valueDate,
            :timestamp,
            :atrValue,
            :trueRange,
            :atr,
            :atrStrength
        );
    """;

    public const string GetLastFuturesAtrSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            FuturesPrice as "FuturesPrice",
            AtrValue AS "AtrValue",
            TrueRange AS "TrueRange",
            ATR AS "ATR",
            ATRStrength AS "ATRStrength"
        FROM futures_atr_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        AND ValueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetLastFuturesDailyAtrSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            FuturesPrice as "FuturesPrice",
            AtrValue AS "AtrValue",
            TrueRange AS "TrueRange",
            ATR AS "ATR",
            ATRStrength AS "ATRStrength"
        FROM futures_atr_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        Limit 1;    
    """;

    public const string DeleteFuturesAtrSignal = """
        DELETE FROM futures_atr_signal
        WHERE contractId = :contractId AND valueDate = :valueDate
    """;

    public const string InsertFuturesAdxSignal = """
        INSERT INTO futures_adx_signal (
            contractId,
            valueDate,
            timestamp,
            plusDI,
            minusDI,
            adxValue,
            adx,
            adxStrength
        ) VALUES (
            :contractId,
            :valueDate,
            :timestamp,
            :plusDI,
            :minusDI,
            :adxValue,
            :adx,
            :adxStrength
        );
    """;

    public const string GetLastFuturesAdxSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            PlusDI AS "PlusDI",
            MinusDI AS "MinusDI",
            AdxValue AS "AdxValue",
            ADX AS "ADX",
            ADXStrength AS "ADXStrength"
        FROM futures_adx_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        AND ValueDate = :valueDate 
        LIMIT 1;
    """;

    public const string GetLastFuturesAdxDailySignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            PlusDI AS "PlusDI",
            MinusDI AS "MinusDI",
            AdxValue AS "AdxValue",
            ADX AS "ADX",
            ADXStrength AS "ADXStrength"
        FROM futures_adx_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        LIMIT 1;
    """;

    public const string DeleteFuturesAdxSignal = """
        DELETE FROM futures_adx_signal
        WHERE contractId = :contractId AND valueDate = :valueDate
    """;

}
