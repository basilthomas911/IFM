namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal static class MarketDataDbCql
{
    public const string InsertFuturesEmaSignal = """
        INSERT INTO futures_ema_signal (
            seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId,
            contractId, valueDate, price, ema10, previousEma10, ema10Slope,
            ema20, previousEma20, ema20Slope, ema50, previousEma50, ema50Slope,
            ema200, previousEma200, ema200Slope, isWarm, sourceSequence, calculatedAt,
            schemaVersion, calculationVersion, calculationMethod, isValid
        ) VALUES (
            :seriesKey, :timePeriod, :configurationId, :yearMonth, :marketDataAsOf, :observationId,
            :contractId, :valueDate, :price, :ema10, :previousEma10, :ema10Slope,
            :ema20, :previousEma20, :ema20Slope, :ema50, :previousEma50, :ema50Slope,
            :ema200, :previousEma200, :ema200Slope, :isWarm, :sourceSequence, :calculatedAt,
            :schemaVersion, :calculationVersion, :calculationMethod, :isValid
        );
        """;

    public const string InsertFuturesBollingerBandSignal = """
        INSERT INTO futures_bollinger_band_signal (
            seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId,
            contractId, valueDate, price, ema10Center, standardDeviation10, upper10, lower10,
            width10, position10, ema20Center, standardDeviation20, upper20, lower20, width20,
            position20, width20Baseline, width20Ratio, isWarm, sourceSequence, calculatedAt,
            schemaVersion, calculationVersion, calculationMethod, isValid
        ) VALUES (
            :seriesKey, :timePeriod, :configurationId, :yearMonth, :marketDataAsOf, :observationId,
            :contractId, :valueDate, :price, :ema10Center, :standardDeviation10, :upper10, :lower10,
            :width10, :position10, :ema20Center, :standardDeviation20, :upper20, :lower20, :width20,
            :position20, :width20Baseline, :width20Ratio, :isWarm, :sourceSequence, :calculatedAt,
            :schemaVersion, :calculationVersion, :calculationMethod, :isValid
        );
        """;

    public const string ClaimMarketDataImportOwnership = """
    INSERT INTO market_data_import_ownership (dataset, logicalKey, commandId, mayWrite, createdOn)
    VALUES (:dataset, :logicalKey, :commandId, :mayWrite, :createdOn) IF NOT EXISTS;
    """;
    public const string GetMarketDataImportOwnership = """
    SELECT commandId AS "CommandId", mayWrite AS "MayWrite" FROM market_data_import_ownership
    WHERE dataset = :dataset AND logicalKey = :logicalKey;
    """;

    public const string DeleteEconomicCalendarV2 = """
    DELETE FROM economic_calendar_v2
    WHERE countryCode = :countryCode AND monthBucket = :monthBucket
    AND eventDate = :eventDate AND eventName = :eventName;
    """;
    public const string GetEconomicCalendarV2ById = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar_v2 WHERE countryCode = :countryCode AND monthBucket = :monthBucket
    AND eventDate = :eventDate AND eventName = :eventName;
    """;
    public const string GetEconomicCalendarCountryCodes = """
    SELECT countryCode AS "CountryCode" FROM economic_calendar_country_code
    WHERE lookupId = :lookupId LIMIT 512;
    """;
    public const string GetEconomicCalendars = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar_v2
    WHERE countryCode = :countryCode AND monthBucket = :monthBucket AND eventDate >= :startDate AND eventDate <= :endDate
    LIMIT 2501;
    """;
    public const string InsertEconomicCalendarV2 = """
    INSERT INTO economic_calendar_v2 (countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, impact, unit, change, changePercentage, createdOn, createdBy, commandId)
    VALUES (:countryCode, :monthBucket, :eventDate, :eventName, :actual, :forecast, :prior, :impact, :unit, :change, :changePercentage, :createdOn, :createdBy, :commandId);
    """;
    public const string InsertEconomicCalendarV2IfNotExists = """
    INSERT INTO economic_calendar_v2 (countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, impact, unit, change, changePercentage, createdOn, createdBy, commandId)
    VALUES (:countryCode, :monthBucket, :eventDate, :eventName, :actual, :forecast, :prior, :impact, :unit, :change, :changePercentage, :createdOn, :createdBy, :commandId)
    IF NOT EXISTS;
    """;
    public const string GetEconomicCalendarV2CommandId = """
    SELECT commandId AS "CommandId" FROM economic_calendar_v2
    WHERE countryCode = :countryCode AND monthBucket = :monthBucket
    AND eventDate = :eventDate AND eventName = :eventName;
    """;
    public const string InsertEconomicCalendarCountryCode = """
    INSERT INTO economic_calendar_country_code (lookupId, countryCode)
    VALUES (:lookupId, :countryCode);
    """;
    public const string UpsertEconomicCalendarCutoverV2 = """
    INSERT INTO economic_calendar_cutover_v2 (cutoverId, sourceRows, targetRows, sourceFingerprint, targetFingerprint, verified, updatedOn)
    VALUES (:cutoverId, :sourceRows, :targetRows, :sourceFingerprint, :targetFingerprint, :verified, :updatedOn);
    """;
    public const string GetEconomicCalendarLegacySource = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar;
    """;
    public const string GetEconomicCalendarV2All = """
    SELECT eventDate AS "EventDate", countryCode AS "CountryCode", eventName AS "EventName", actual AS "Actual", forecast AS "Forecast", prior AS "Prior", impact AS "Impact", unit AS "Unit", change AS "Change", changePercentage AS "ChangePercentage", createdOn AS "CreatedOn", createdBy AS "CreatedBy"
    FROM economic_calendar_v2;
    """;
    public const string GetEconomicCalendarCountryCodeAll = """
    SELECT countryCode AS "CountryCode" FROM economic_calendar_country_code;
    """;
    public const string TruncateEconomicCalendarV2 =
        "TRUNCATE economic_calendar_v2;";
    public const string TruncateEconomicCalendarCountryCode =
        "TRUNCATE economic_calendar_country_code;";

    public const string InsertTickTradeData = """
        INSERT INTO tick_trade_data (
            asset_type_id, contract_id, value_date, aggregation_time, sequence_id,
            aggregation_timestamp_utc, aggregation_timestamp_utc_ticks,
            schema_version, dataset, definition_date, publisher_id, instrument_id,
            actor_event_id, actor_event_log_id, command_id, aggregate_id,
            event_source, received_on, source_sequence, source_event_timestamp_ns,
            source_receive_timestamp_ns, header_flags, price_raw, price, size,
            action, side, dbn_flags)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
        """;

    public const string InsertTickQuoteData = """
        INSERT INTO tick_quote_data (
            asset_type_id, contract_id, value_date, aggregation_time, sequence_id,
            aggregation_timestamp_utc, aggregation_timestamp_utc_ticks,
            schema_version, dataset, definition_date, publisher_id, instrument_id,
            actor_event_id, actor_event_log_id, command_id, aggregate_id,
            event_source, received_on, emission_reason, quote_count, quote_data)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
        """;

    public const string GetTickTradeDataRange = """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND value_date >= ? AND value_date <= ?;
        """;

    public const string GetTickQuoteDataRange = """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND value_date >= ? AND value_date <= ?;
        """;

    public const string GetTickTradeDataIntradayRange = """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ? AND value_date = ?
          AND aggregation_time >= ? AND aggregation_time <= ?;
        """;

    public const string GetTickQuoteDataIntradayRange = """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ? AND value_date = ?
          AND aggregation_time >= ? AND aggregation_time <= ?;
        """;

    public const string GetTickTradeDataExactRange = """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND (value_date, aggregation_time) >= (?, ?)
          AND (value_date, aggregation_time) <= (?, ?);
        """;

    public const string GetTickQuoteDataExactRange = """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND (value_date, aggregation_time) >= (?, ?)
          AND (value_date, aggregation_time) <= (?, ?);
        """;
    public const string TruncateFuturesTickDataByTime = "TRUNCATE futures_tick_data_by_time;";
    public const string TruncateFuturesEodDataByMonth = "TRUNCATE futures_eod_data_by_month;";
    public const string TruncateVixFuturesContractIndex = "TRUNCATE vix_futures_contract_index;";
    public const string TruncateMarketDataProjectionMonth = "TRUNCATE market_data_projection_month;";

    public const string GetMarketDataProjectionState = """
        SELECT projectionName AS "ProjectionName",
            generation AS "Generation",
            isReady AS "IsReady"
        FROM market_data_projection_state_v2
        WHERE projectionName = :projectionName;
    """;

    public const string BeginMarketDataProjectionOperation = """
        UPDATE market_data_projection_state_v2
        SET generation = :generation,
            isReady = false,
            activeOperations = activeOperations + :activeOperations
        WHERE projectionName = :projectionName;
    """;

    public const string EndMarketDataProjectionOperation = """
        UPDATE market_data_projection_state_v2
        SET generation = :generation,
            isReady = false,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName;
    """;

    public const string RemoveMarketDataProjectionOperations = """
        UPDATE market_data_projection_state_v2
        SET activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName;
    """;

    public const string CompleteMarketDataProjectionState = """
        UPDATE market_data_projection_state_v2
        SET isReady = true,
            activeOperations = activeOperations - :activeOperations,
            sourceRowCount = :sourceRowCount,
            projectedRowCount = :projectedRowCount,
            sourceFingerprint = :sourceFingerprint,
            projectedFingerprint = :projectedFingerprint,
            completedOn = :completedOn
        WHERE projectionName = :projectionName
        IF generation = :generation
        AND activeOperations = :expectedActiveOperations;
    """;

    public const string RestoreMarketDataProjectionState = """
        UPDATE market_data_projection_state_v2
        SET isReady = true,
            activeOperations = activeOperations - :activeOperations,
            completedOn = :completedOn
        WHERE projectionName = :projectionName
        IF generation = :generation
        AND activeOperations = :expectedActiveOperations;
    """;

    public const string InsertMarketDataProjectionMutation = """
        INSERT INTO market_data_projection_mutation (projectionName, mutationId, startedOn)
        VALUES (:projectionName, :mutationId, :startedOn);
    """;

    public const string DeleteMarketDataProjectionMutation = """
        DELETE FROM market_data_projection_mutation
        WHERE projectionName = :projectionName AND mutationId = :mutationId;
    """;

    public const string FailMarketDataProjectionMutation = """
        UPDATE market_data_projection_mutation
        SET startedOn = :startedOn
        WHERE projectionName = :projectionName AND mutationId = :mutationId;
    """;

    public const string GetMarketDataProjectionMutation = """
        SELECT mutationId AS "MutationId",
            startedOn AS "StartedOn"
        FROM market_data_projection_mutation
        WHERE projectionName = :projectionName
        LIMIT 1;
    """;

    public const string GetMarketDataProjectionMutations = """
        SELECT mutationId AS "MutationId",
            startedOn AS "StartedOn"
        FROM market_data_projection_mutation
        WHERE projectionName = :projectionName;
    """;

    public const string GetMarketDataProjectionScopeStatesV3 = """
        SELECT projectionName AS "ProjectionName",
            scopeKey AS "ScopeKey",
            generation AS "Generation",
            isReady AS "IsReady",
            blocked AS "Blocked",
            activeOperations AS "ActiveOperations"
        FROM market_data_projection_scope_state_v3
        WHERE projectionName = :projectionName
        AND scopeKey IN :scopeKeys;
    """;

    public const string GetMarketDataProjectionScopeStatesV3All = """
        SELECT projectionName AS "ProjectionName",
            scopeKey AS "ScopeKey",
            generation AS "Generation",
            isReady AS "IsReady",
            blocked AS "Blocked",
            activeOperations AS "ActiveOperations"
        FROM market_data_projection_scope_state_v3;
    """;

    public const string BeginMarketDataProjectionScopeOperationV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET generation = :generation,
            isReady = false,
            blocked = true,
            activeOperations = activeOperations + :activeOperations
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey;
    """;

    public const string EndMarketDataProjectionScopeOperationV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET generation = :generation,
            isReady = false,
            blocked = true,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey;
    """;

    public const string CompleteMarketDataProjectionScopeOperationV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET isReady = true,
            blocked = false,
            activeOperations = activeOperations - :activeOperations,
            completedOn = :completedOn
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey
        IF generation = :generation
        AND activeOperations = :expectedActiveOperations;
    """;

    public const string MarkMarketDataProjectionScopeAtomicWriteV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET generation = :generation,
            isReady = true
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey;
    """;

    public const string RegisterMarketDataProjectionGuardOperationV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET activeOperations = activeOperations + :activeOperations
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey;
    """;

    public const string CompleteMarketDataProjectionGuardOperationV3 = """
        UPDATE market_data_projection_scope_state_v3
        SET generation = :generation,
            isReady = true,
            blocked = false,
            activeOperations = activeOperations - :activeOperations,
            completedOn = :completedOn
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey
        IF blocked = false
        AND activeOperations = :expectedActiveOperations;
    """;

    public const string RemoveMarketDataProjectionScopeOperationV3 = """
        DELETE activeOperations[:operationId]
        FROM market_data_projection_scope_state_v3
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey;
    """;

    public const string InsertMarketDataProjectionScopeMutationV3 = """
        INSERT INTO market_data_projection_scope_mutation_v3 (
            projectionName, scopeKey, mutationId, startedOn)
        VALUES (:projectionName, :scopeKey, :mutationId, :startedOn);
    """;

    public const string FailMarketDataProjectionScopeMutationV3 = """
        UPDATE market_data_projection_scope_mutation_v3
        SET startedOn = :startedOn
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey
        AND mutationId = :mutationId
        IF EXISTS;
    """;

    public const string DeleteMarketDataProjectionScopeMutationV3 = """
        DELETE FROM market_data_projection_scope_mutation_v3
        WHERE projectionName = :projectionName
        AND scopeKey = :scopeKey
        AND mutationId = :mutationId;
    """;

    public const string GetMarketDataProjectionScopeMutationsV3All = """
        SELECT projectionName AS "ProjectionName",
            scopeKey AS "ScopeKey",
            mutationId AS "MutationId",
            startedOn AS "StartedOn"
        FROM market_data_projection_scope_mutation_v3;
    """;

    public const string GetFuturesTickProjectionScopesSource = """
        SELECT contractId, valueDate
        FROM futures_tick_data;
    """;

    public const string GetFuturesTickProjectionScopesTarget = """
        SELECT contractId, valueDate
        FROM futures_tick_data_by_time;
    """;

    public const string GetFuturesEodProjectionScopesSource = """
        SELECT valueDate
        FROM futures_eod_data;
    """;

    public const string GetFuturesEodProjectionScopesTarget = """
        SELECT yearMonth
        FROM futures_eod_data_by_month;
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

    public const string DeleteFuturesTickDataByTime = """
        DELETE FROM futures_tick_data_by_time
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteVixFuturesEodData = """
        DELETE FROM vix_futures_eod_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteVixFuturesContractIndex = """
        DELETE FROM vix_futures_contract_index
        WHERE bucket = :bucket AND contractId = :contractId;
    """;

    public const string DeleteFuturesEodDataByMonth = """
        DELETE FROM futures_eod_data_by_month
        WHERE yearMonth = :yearMonth AND valueDate = :valueDate AND contractId = :contractId;
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

    public const string GetFuturesTickDataByDate = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            tickId AS "TickId",
            tickTime AS "TickTime",
            price AS "Price",
            size AS "Size"
        FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string GetFuturesTickDataAll = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            tickId AS "TickId",
            tickTime AS "TickTime",
            price AS "Price",
            size AS "Size"
        FROM futures_tick_data;
    """;

    public const string GetFuturesTickDataByTimeAll = """
        SELECT contractId AS "ContractId",
            valueDate AS "ValueDate",
            tickId AS "TickId",
            tickTime AS "TickTime",
            price AS "Price",
            size AS "Size"
        FROM futures_tick_data_by_time;
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
        FROM futures_tick_data_by_time
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND tickTime = :tickTime
        ORDER BY tickTime DESC, tickId DESC
        LIMIT 1;
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
            windowSize,
            fiftyDMA,
            twoHundredDMA
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
            :windowSize,
            :fiftyDMA,
            :twoHundredDMA
        );
    """;

    public const string InsertFuturesEodDataByMonth = """
        INSERT INTO futures_eod_data_by_month (
            yearMonth,
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
            windowSize,
            fiftyDMA,
            twoHundredDMA
        ) VALUES (
            :yearMonth,
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
            :windowSize,
            :fiftyDMA,
            :twoHundredDMA
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

    public const string InsertFuturesTickDataByTime = """
        INSERT INTO futures_tick_data_by_time (contractId, valueDate, tickTime, tickId, price, size)
        VALUES (:contractId, :valueDate, :tickTime, :tickId, :price, :size);
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
        FROM futures_eod_data_by_month
        WHERE yearMonth = :yearMonth
        AND valueDate >= :startDate
        AND valueDate <= :endDate;
    """;

    public const string GetCurrentFuturesEodDataByMonth = """
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
        FROM futures_eod_data_by_month
        WHERE yearMonth = :yearMonth
        AND valueDate <= :valueDate
        LIMIT 1;
    """;

    public const string GetFuturesEodClosingPrices = """
        SELECT
            symbol AS "Symbol",
            valueDate AS "ValueDate",
            closePrice AS "ClosingPrice"
        FROM futures_eod_data
        WHERE contractId = :contractId
        AND valueDate >= :startDate
        AND valueDate <= :endDate
        ORDER BY valueDate DESC;
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
        FROM futures_eod_data
    """;

    public const string GetFuturesEodDataByMonthAll = """
        SELECT contractId AS "ContractId",
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
        FROM futures_eod_data_by_month;
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
        FROM futures_eod_data
        WHERE contractId = :contractId
        AND valueDate >= :startDate AND valueDate <= :endDate
        ORDER BY valueDate DESC;
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
            windowSize = :windowSize,
            fiftyDMA = :fiftyDMA,
            twoHundredDMA = :twoHundredDMA
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
            windowSize AS "WindowSize",
            fiftyDMA AS "FiftyDMA",
            twoHundredDMA AS "TwoHundredDMA"
        FROM futures_eod_data
        WHERE contractId = :contractId
        AND valueDate < :valueDate
        ORDER BY valueDate DESC
        LIMIT 1;
    """;

    public const string GetFuturesTickHLVData = """
        SELECT 
            ContractId as "ContractId", 
            ValueDate as "ValueDate", 
            max(price) as "HighPrice",
            min(price) as "LowPrice",
            sum(cast(size as bigint)) as "Volume"
        FROM futures_tick_data
        WHERE ContractId = :contractId
        AND ValueDate = :valueDate
        GROUP BY ContractId, ValueDate;
    """;

    public const string InsertFuturesEodDataIndex = """
        INSERT INTO futures_eod_data_index (valueDate, contractId)
        VALUES (:valueDate, :contractId);
    """;

    public const string UpdateFuturesEodSessionStatistics = """
        UPDATE futures_eod_data
        SET
            openPrice = :openPrice,
            highPrice = :highPrice,
            lowPrice = :lowPrice,
            volume = :volume,
            dailyPercentChange = :dailyPercentChange,
            priceDirection = :priceDirection
        WHERE
            contractId = :contractId
            AND valueDate = :valueDate
            AND symbol = :symbol;
    """;

    public const string InsertMarketDataProjectionMonth = """
        INSERT INTO market_data_projection_month (projectionName, yearMonth)
        VALUES (:projectionName, :yearMonth);
    """;

    public const string GetMarketDataProjectionMonths = """
        SELECT yearMonth AS "YearMonth"
        FROM market_data_projection_month
        WHERE projectionName = :projectionName
        AND yearMonth <= :yearMonth;
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
            tradeState,
            bandLevel,
            reversalLevel
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
            :tradeState,
            :bandLevel,
            :reversalLevel
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND timePeriod = :timePeriod;
    """;

    public const string GetFuturesItiSignalsByDateRangeIndex = """
        SELECT 
            valueDate AS "ValueDate",
            contractId AS "ContractId"
        FROM futures_iti_signal_index
        WHERE token(valueDate) >= token(:startDate) AND token(valueDate) <= token(:endDate);
    """;

    public const string InsertFuturesItiSignalIndex = """
        INSERT INTO futures_iti_signal_index (valueDate, contractId)
        VALUES (:valueDate, :contractId);
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
            rsiSlope,
            sourceSequence,
            sourceEventTimestamp,
            configurationId,
            observationId,
            marketDataAsOf,
            calculationVersion,
            calculationMethod,
            schemaVersion,
            isValid
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
            :rsiSlope,
            :sourceSequence,
            :sourceEventTimestamp,
            :configurationId,
            :observationId,
            :marketDataAsOf,
            :calculationVersion,
            :calculationMethod,
            :schemaVersion,
            :isValid
        );
    """;

    public const string UpsertFuturesItiTimeFrameState = """
        INSERT INTO futures_iti_timeframe_state (
            contractId, timePeriod, calendarBucketStart,
            timeFrameStartValueDate, valueDate, sequenceId, intrinsicTime,
            intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice,
            intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme,
            trendReversal, trendDelta, targetDelta, lambda, tradingDays,
            threshold, upTrendTrigger, downTrendTrigger, tradeState,
            bandAnchorPrice, bandPercentage, bandSize, bandLevel, reversalLevel
        ) VALUES (
            :contractId, :timePeriod, :calendarBucketStart,
            :timeFrameStartValueDate, :valueDate, :sequenceId, :intrinsicTime,
            :intrinsicTimeGroupId, :intrinsicTimeLength, :intrinsicPrice,
            :intrinsicTimeTrend, :intrinsicTimeMode, :trendPrice, :trendExtreme,
            :trendReversal, :trendDelta, :targetDelta, :lambda, :tradingDays,
            :threshold, :upTrendTrigger, :downTrendTrigger, :tradeState,
            :bandAnchorPrice, :bandPercentage, :bandSize, :bandLevel, :reversalLevel
        );
    """;

    public const string GetFuturesItiTimeFrameState = """
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
            tradeState AS "TradeState",
            timeFrameStartValueDate AS "TimeFrameStartValueDate",
            bandAnchorPrice AS "BandAnchorPrice",
            bandPercentage AS "BandPercentage",
            bandSize AS "BandSize",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_timeframe_state
        WHERE contractId = :contractId
        AND timePeriod = :timePeriod
        AND calendarBucketStart = :calendarBucketStart;
    """;

    public const string InsertFuturesItiSignalByContractDay = """
        INSERT INTO futures_iti_signal_by_contract_day (
            contractId, valueDate, timePeriod, sequenceId, intrinsicTime,
            intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice,
            intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme,
            trendReversal, trendDelta, targetDelta, lambda, tradingDays,
            threshold, upTrendTrigger, downTrendTrigger, tradeState,
            bandLevel, reversalLevel)
        VALUES (
            :contractId, :valueDate, :timePeriod, :sequenceId, :intrinsicTime,
            :intrinsicTimeGroupId, :intrinsicTimeLength, :intrinsicPrice,
            :intrinsicTimeTrend, :intrinsicTimeMode, :trendPrice, :trendExtreme,
            :trendReversal, :trendDelta, :targetDelta, :lambda, :tradingDays,
            :threshold, :upTrendTrigger, :downTrendTrigger, :tradeState,
            :bandLevel, :reversalLevel);
    """;

    public const string InsertFuturesItiSignalByContractMonth = """
        INSERT INTO futures_iti_signal_by_contract_month (
            contractId, yearMonth, valueDate, timePeriod, sequenceId, intrinsicTime,
            intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice,
            intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme,
            trendReversal, trendDelta, targetDelta, lambda, tradingDays,
            threshold, upTrendTrigger, downTrendTrigger, tradeState,
            bandLevel, reversalLevel)
        VALUES (
            :contractId, :yearMonth, :valueDate, :timePeriod, :sequenceId, :intrinsicTime,
            :intrinsicTimeGroupId, :intrinsicTimeLength, :intrinsicPrice,
            :intrinsicTimeTrend, :intrinsicTimeMode, :trendPrice, :trendExtreme,
            :trendReversal, :trendDelta, :targetDelta, :lambda, :tradingDays,
            :threshold, :upTrendTrigger, :downTrendTrigger, :tradeState,
            :bandLevel, :reversalLevel);
    """;

    public const string InsertFuturesItiSignalByTrendModeMonth = """
        INSERT INTO futures_iti_signal_by_trend_mode_month (
            contractId, yearMonth, valueDate, timePeriod, sequenceId, intrinsicTime,
            intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice,
            intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme,
            trendReversal, trendDelta, targetDelta, lambda, tradingDays,
            threshold, upTrendTrigger, downTrendTrigger, tradeState,
            bandLevel, reversalLevel)
        VALUES (
            :contractId, :yearMonth, :valueDate, :timePeriod, :sequenceId, :intrinsicTime,
            :intrinsicTimeGroupId, :intrinsicTimeLength, :intrinsicPrice,
            :intrinsicTimeTrend, :intrinsicTimeMode, :trendPrice, :trendExtreme,
            :trendReversal, :trendDelta, :targetDelta, :lambda, :tradingDays,
            :threshold, :upTrendTrigger, :downTrendTrigger, :tradeState,
            :bandLevel, :reversalLevel);
    """;

    public const string GetFuturesItiSignalsCanonicalByContract = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal
        WHERE contractId = :contractId;
    """;

    public const string GetFuturesItiSignalsAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal;
    """;

    public const string GetFuturesItiSignalsCanonicalByContractDay = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string GetFuturesItiSignalsByContractMonth = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_month
        WHERE contractId = :contractId AND yearMonth = :yearMonth
        AND valueDate >= :startDate AND valueDate <= :endDate;
    """;

    public const string GetFuturesItiSignalsByContractDayMode = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId AND valueDate = :valueDate
        AND intrinsicTimeMode = :intrinsicTimeMode;
    """;

    public const string GetFuturesItiSignalsByContractDayModeAfterSequence = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId AND valueDate = :valueDate
        AND intrinsicTimeMode = :intrinsicTimeMode AND sequenceId > :sequenceId;
    """;

    public const string GetLastFuturesItiSignalByTrendModeMonth = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_trend_mode_month
        WHERE contractId = :contractId AND intrinsicTimeTrend = :intrinsicTimeTrend
        AND intrinsicTimeMode = :intrinsicTimeMode AND yearMonth = :yearMonth
        AND valueDate <= :valueDate LIMIT 1;
    """;

    public const string GetFuturesItiSignalsByTrendModeMonth = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_trend_mode_month
        WHERE contractId = :contractId AND intrinsicTimeTrend = :intrinsicTimeTrend
        AND intrinsicTimeMode = :intrinsicTimeMode AND yearMonth = :yearMonth
        AND valueDate >= :startDate AND valueDate <= :endDate;
    """;

    public const string GetFuturesItiSignalProjectionScopesSource = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode"
        FROM futures_iti_signal;
    """;

    public const string GetFuturesItiSignalProjectionScopesDayTarget = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode"
        FROM futures_iti_signal_by_contract_day;
    """;

    public const string GetFuturesItiSignalByContractDayAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day;
    """;

    public const string GetFuturesItiSignalByContractMonthAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_month;
    """;

    public const string GetFuturesItiSignalByTrendModeMonthAll = """
        SELECT contractId AS "ContractId", valueDate AS "ValueDate",
            timePeriod AS "TimePeriod", sequenceId AS "SequenceId",
            intrinsicTime AS "IntrinsicTime", intrinsicTimeGroupId AS "IntrinsicTimeGroupId",
            intrinsicTimeLength AS "IntrinsicTimeLength", intrinsicPrice AS "IntrinsicPrice",
            intrinsicTimeTrend AS "IntrinsicTimeTrend", intrinsicTimeMode AS "IntrinsicTimeMode",
            trendPrice AS "TrendPrice", trendExtreme AS "TrendExtreme",
            trendReversal AS "TrendReversal", trendDelta AS "TrendDelta",
            targetDelta AS "TargetDelta", lambda AS "Lambda", tradingDays AS "TradingDays",
            threshold AS "Threshold", upTrendTrigger AS "UpTrendTrigger",
            downTrendTrigger AS "DownTrendTrigger", tradeState AS "TradeState",
            bandLevel AS "BandLevel", reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_trend_mode_month;
    """;

    public const string TruncateFuturesItiSignalByContractDay =
        "TRUNCATE futures_iti_signal_by_contract_day;";

    public const string TruncateFuturesItiSignalByContractMonth =
        "TRUNCATE futures_iti_signal_by_contract_month;";

    public const string TruncateFuturesItiSignalByTrendModeMonth =
        "TRUNCATE futures_iti_signal_by_trend_mode_month;";

    public const string GetFuturesRsiSignalsForTrend = """
        SELECT rsi AS "RSI"
        FROM futures_rsi_signal
        WHERE contractid = :contractid
        AND timeperiod = :timePeriod
        AND periodlength = :periodLength
        AND valuedate = :valuedate
        AND timestamp >= :startTime
        AND timestamp <= :endTime;
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND intrinsicTimeMode = 'TrendReversalChanged'
        LIMIT 1;
    """;

    public const string GetLastFuturesItiSignalTrendDirectionChange = """
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND intrinsicTimeMode = 'TrendDirectionChanged'
        LIMIT 1;
    """;

    public const string GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged = """
        SELECT sequenceid AS "Value"
        FROM futures_iti_signal_by_contract_day
        WHERE contractid = :contractid
        AND valuedate = :valuedate
        AND intrinsictimemode = 'TrendDirectionChanged'
        LIMIT 1;
    """;

    public const string GetLastFuturesItiSignalTrendExtremeChange = """
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND SequenceId > :lastTrendDirectionChangedSequenceId
        AND intrinsicTimeMode = 'TrendExtremeChanged'
        LIMIT 1;
    """;

    public const string GetLastFuturesItiSignalTrendReversalChange = """
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId 
        AND valueDate = :valueDate
        AND SequenceId > :lastTrendDirectionChangedSequenceId
        AND intrinsicTimeMode = 'TrendReversalChanged'
        Limit 1;
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


    public const string GetLastFuturesRsiSignal = """
        SELECT contractid AS "ContractId",
            valuedate AS "ValueDate",
            timePeriod AS "TimePeriod",
            periodLength AS "PeriodLength",
            timestamp AS "Timestamp",
            price AS "FuturesPrice",
            pricechange AS "PriceChange",
            pricegain AS "PriceGain",
            priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain",
            averagepriceloss AS "AveragePriceLoss",
            rs AS "RS",
            rsi AS "RSI",
            rsiaverage AS "RSIAverage",
            rsislope AS "RSISlope",
            sourceSequence AS "SourceSequence",
            sourceEventTimestamp AS "SourceEventTimestamp",
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
            price AS "FuturesPrice",
            pricechange AS "PriceChange",
            pricegain AS "PriceGain",
            priceloss AS "PriceLoss",
            averagepricegain AS "AveragePriceGain",
            averagepriceloss AS "AveragePriceLoss",
            rs AS "RS",
            rsi AS "RSI",
            rsiaverage AS "RSIAverage",
            rsislope AS "RSISlope",
            sourceSequence AS "SourceSequence",
            sourceEventTimestamp AS "SourceEventTimestamp",
            windowsize AS "WindowSize"
        FROM futures_rsi_signal
           WHERE contractid = :contractId
        AND timePeriod = :timePeriod
        AND periodLength = :periodLength
        LIMIT 1;
    """;

    public const string InsertFuturesTdiSignal = """
        INSERT INTO futures_traders_dynamic_index_signal (
            contractId,
            timePeriod,
            configurationId,
            valueDate,
            timestamp,
            schemaVersion,
            rsiPeriod,
            priceLinePeriod,
            signalLinePeriod,
            marketBasePeriod,
            volatilityBandPeriod,
            volatilityBandDeviation,
            price,
            rsi,
            priceLine,
            signalLine,
            marketBaseLine,
            upperVolatilityBand,
            lowerVolatilityBand,
            bandWidth,
            priceSignalDivergence,
            crossType,
            marketState,
            trendDirection,
            trendStrength,
            sourceSequence,
            sourceEventTimestamp
        ) VALUES (
            :contractId,
            :timePeriod,
            :configurationId,
            :valueDate,
            :timestamp,
            :schemaVersion,
            :rsiPeriod,
            :priceLinePeriod,
            :signalLinePeriod,
            :marketBasePeriod,
            :volatilityBandPeriod,
            :volatilityBandDeviation,
            :price,
            :rsi,
            :priceLine,
            :signalLine,
            :marketBaseLine,
            :upperVolatilityBand,
            :lowerVolatilityBand,
            :bandWidth,
            :priceSignalDivergence,
            :crossType,
            :marketState,
            :trendDirection,
            :trendStrength,
            :sourceSequence,
            :sourceEventTimestamp
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
            TimePeriod AS "TimePeriod",
            Timestamp AS "Timestamp",
            SchemaVersion AS "SchemaVersion",
            ConfigurationId AS "ConfigurationId",
            RsiPeriod AS "RsiPeriod",
            PriceLinePeriod AS "PriceLinePeriod",
            SignalLinePeriod AS "SignalLinePeriod",
            MarketBasePeriod AS "MarketBasePeriod",
            VolatilityBandPeriod AS "VolatilityBandPeriod",
            VolatilityBandDeviation AS "VolatilityBandDeviation",
            Price AS "Price",
            Rsi AS "Rsi",
            PriceLine AS "PriceLine",
            SignalLine AS "SignalLine",
            MarketBaseLine AS "MarketBaseLine",
            UpperVolatilityBand AS "UpperVolatilityBand",
            LowerVolatilityBand AS "LowerVolatilityBand",
            BandWidth AS "BandWidth",
            PriceSignalDivergence AS "PriceSignalDivergence",
            CrossType AS "CrossType",
            MarketState AS "MarketState",
            TrendDirection AS "TrendDirection",
            TrendStrength AS "TrendStrength",
            SourceSequence AS "SourceSequence",
            SourceEventTimestamp AS "SourceEventTimestamp"
        FROM futures_traders_dynamic_index_signal
        WHERE ContractId = :contractId
          AND TimePeriod = :timePeriod
          AND ConfigurationId = :configurationId
          AND ValueDate = :valueDate
        LIMIT 1;
    """;

    public const string InsertFuturesTradeSignal = """
        INSERT INTO futures_trade_signal (
            contractId,
            valueDate,
            timePeriod,
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
            :timePeriod,
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

    public const string InsertFuturesTradeSignalIndex = """
        INSERT INTO futures_trade_signal_lookup_by_scope (
            scope,
            entryId,
            sequenceId,
            contractId,
            valueDate,
            timePeriod
        ) VALUES (
            :scope,
            :entryId,
            :sequenceId,
            :contractId,
            :valueDate,
            :timePeriod
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
        WHERE contractId = :contractId
        AND valueDate = :valueDate
        AND timePeriod = :timePeriod
        LIMIT 1;
    """;

    public const string GetLastFuturesTradeSignal = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId"
        FROM futures_trade_signal_lookup_by_scope
        WHERE scope = :scope
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
        VALUES (:contractId, :valueDate, :openPrice, :highPrice, :lowPrice, :closePrice, :volume);
    """;

    public const string GetFuturesTradeSignalJsonAll = """
        SELECT JSON * FROM futures_trade_signal;
    """;

    public const string InsertFuturesTradeSignalQuarantine = """
        INSERT INTO futures_trade_signal_quarantine (
            fingerprint,
            sourcePayload,
            reason,
            quarantinedOn
        ) VALUES (
            :fingerprint,
            :sourcePayload,
            :reason,
            :quarantinedOn
        );
    """;

    public const string GetLastFuturesItiSignalByTimePeriod = """
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
            tradeState AS "TradeState",
            bandLevel AS "BandLevel",
            reversalLevel AS "ReversalLevel"
        FROM futures_iti_signal
        WHERE contractId = :contractId
        AND valueDate = :valueDate
        AND timePeriod = :timePeriod
        LIMIT 1;
    """;

    public const string InsertVixFuturesContractIndex = """
        INSERT INTO vix_futures_contract_index (bucket, contractId)
        VALUES (:bucket, :contractId);
    """;

    public const string GetVixFuturesContractIds = """
        SELECT contractId AS "ContractId"
        FROM vix_futures_contract_index
        WHERE bucket = :bucket;
    """;

    public const string GetVixFuturesContractIndexAll = """
        SELECT bucket AS "Bucket",
            contractId AS "ContractId"
        FROM vix_futures_contract_index;
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

    public const string GetVixFuturesEodDataThroughDate = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume"
        FROM vix_futures_eod_data
        WHERE contractId = :contractId
        AND valueDate <= :valueDate;
    """;

    public const string GetVixFuturesEodDataAll = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            openPrice AS "OpenPrice",
            highPrice AS "HighPrice",
            lowPrice AS "LowPrice",
            closePrice AS "ClosePrice",
            volume AS "Volume"
        FROM vix_futures_eod_data;
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

    public const string InsertYieldCurveRateByDate = """
        INSERT INTO yield_curve_rate_by_date (
            lookupId, valueDate, oneMonth, twoMonth, threeMonth, sixMonth,
            oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear,
            twentyYear, thirtyYear
        ) VALUES (
            :id, :valueDate, :oneMonth, :twoMonth, :threeMonth, :sixMonth,
            :oneYear, :twoYear, :threeYear, :fiveYear, :sevenYear, :tenYear,
            :twentyYear, :thirtyYear
        );
    """;

    public const string DeleteYieldCurveRate = """
        DELETE FROM yield_curve_rates
        WHERE id = 1
        AND valueDate = :valueDate;
    """;

    public const string DeleteYieldCurveRateByDate = """
        DELETE FROM yield_curve_rate_by_date
        WHERE lookupId = 1 AND valueDate = :valueDate;
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
        FROM yield_curve_rate_by_date
        WHERE lookupId = 1 AND valueDate = :valueDate;
    """;

    public const string GetLastYieldCurveRate = """
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
        FROM yield_curve_rate_by_date WHERE lookupId = 1 LIMIT 1;
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
        FROM yield_curve_rate_by_date
        WHERE lookupId = 1 AND valueDate >= :startDate
        AND valueDate <= :endDate LIMIT 5000;
    """;

    public const string GetYieldCurveRateYears = """
        SELECT rateYear AS "RateYear"
        FROM yield_curve_rate_year
        WHERE lookupId = :lookupId LIMIT 200;
    """;

    public const string InsertYieldCurveRateYear = """
        INSERT INTO yield_curve_rate_year (lookupId, rateYear)
        VALUES (:lookupId, :rateYear);
    """;

    public const string GetYieldCurveRateProjectionSource = """
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

    public const string GetYieldCurveRateYearAll = """
        SELECT rateYear AS "RateYear" FROM yield_curve_rate_year;
    """;

    public const string GetYieldCurveRateByDateAll = """
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
        FROM yield_curve_rate_by_date;
    """;

    public const string TruncateYieldCurveRateByDate =
        "TRUNCATE yield_curve_rate_by_date;";

    public const string TruncateYieldCurveRateYear =
        "TRUNCATE yield_curve_rate_year;";

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

    public const string GetNormalCurveData = """
        SELECT 
            StdDevIndex AS "StdDevIndex",
            Percent AS "Percent"
        FROM normal_curve_data;
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

    public const string GetFuturesTradeSignalIdByValueDate = """
        SELECT
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            timePeriod AS "TimePeriod",
            sequenceId AS "SequenceId"
        FROM futures_trade_signal_lookup_by_scope
        WHERE scope = :scope;
    """;

    public const string DeleteFuturesItiTimeFrameState = """
        DELETE FROM futures_iti_timeframe_state
        WHERE contractId = :contractId
        AND timePeriod = :timePeriod
        AND calendarBucketStart = :calendarBucketStart;
    """;

    public const string DeleteFuturesItiSignalByContractDay = """
        DELETE FROM futures_iti_signal_by_contract_day
        WHERE contractId = :contractId AND valueDate = :valueDate
        AND intrinsicTimeMode = :intrinsicTimeMode AND sequenceId = :sequenceId
        AND timePeriod = :timePeriod AND intrinsicTimeTrend = :intrinsicTimeTrend
        AND intrinsicTimeGroupId = :intrinsicTimeGroupId;
    """;

    public const string DeleteFuturesItiSignalByContractMonth = """
        DELETE FROM futures_iti_signal_by_contract_month
        WHERE contractId = :contractId AND yearMonth = :yearMonth
        AND valueDate = :valueDate AND sequenceId = :sequenceId
        AND timePeriod = :timePeriod AND intrinsicTimeMode = :intrinsicTimeMode
        AND intrinsicTimeTrend = :intrinsicTimeTrend
        AND intrinsicTimeGroupId = :intrinsicTimeGroupId;
    """;

    public const string DeleteFuturesItiSignalByTrendModeMonth = """
        DELETE FROM futures_iti_signal_by_trend_mode_month
        WHERE contractId = :contractId AND intrinsicTimeTrend = :intrinsicTimeTrend
        AND intrinsicTimeMode = :intrinsicTimeMode AND yearMonth = :yearMonth
        AND valueDate = :valueDate AND sequenceId = :sequenceId
        AND timePeriod = :timePeriod AND intrinsicTimeGroupId = :intrinsicTimeGroupId;
    """;

    public const string InsertFuturesMacdSignal = """
        INSERT INTO futures_macd_signal_v2 (
            contractId,
            valueDate,
            timePeriod,
            signalEmaPeriod,
            fastEmaPeriod,
            slowEmaPeriod,
            timestamp,
            futuresPrice,
            fastEma,
            slowEma,
            macdLine,
            signalLine,
            histogram,
            macd,
            macdStrength,
            configurationId,
            observationId,
            marketDataAsOf,
            sourceSequence,
            calculationVersion,
            calculationMethod,
            schemaVersion,
            isValid
        ) VALUES (
            :contractId,
            :valueDate,
            :timePeriod,
            :signalEmaPeriod,
            :fastEmaPeriod,
            :slowEmaPeriod,
            :timestamp,
            :futuresPrice,
            :fastEma,
            :slowEma,
            :macdLine,
            :signalLine,
            :histogram,
            :macd,
            :macdStrength,
            :configurationId,
            :observationId,
            :marketDataAsOf,
            :sourceSequence,
            :calculationVersion,
            :calculationMethod,
            :schemaVersion,
            :isValid
        );
    """;

    public const string GetLastFuturesMacdSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod AS "TimePeriod",
            SignalEmaPeriod AS "SignalEmaPeriod",
            FastEmaPeriod AS "FastEmaPeriod",
            SlowEmaPeriod AS "SlowEmaPeriod",
            Timestamp AS "Timestamp",
            FuturesPrice AS "FuturesPrice",
            FastEma AS "FastEma",
            SlowEma AS "SlowEma",
            MacdLine AS "MacdLine",
            SignalLine AS "SignalLine",
            Histogram AS "Histogram",
            MACD AS "MACD",
            MACDStrength AS "MACDStrength"
        FROM futures_macd_signal_v2
        WHERE ContractId = :contractId
        AND TimePeriod = :timePeriod
        AND SignalEmaPeriod = :signalEmaPeriod
        AND FastEmaPeriod = :fastEmaPeriod
        AND SlowEmaPeriod = :slowEmaPeriod
        AND ValueDate = :valueDate LIMIT 1;
    """;

    public const string GetLastFuturesMacdDailySignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod AS "TimePeriod",
            SignalEmaPeriod AS "SignalEmaPeriod",
            FastEmaPeriod AS "FastEmaPeriod",
            SlowEmaPeriod AS "SlowEmaPeriod",
            Timestamp AS "Timestamp",
            FuturesPrice AS "FuturesPrice",
            FastEma AS "FastEma",
            SlowEma AS "SlowEma",
            MacdLine AS "MacdLine",
            SignalLine AS "SignalLine",
            Histogram AS "Histogram",
            MACD AS "MACD",
            MACDStrength AS "MACDStrength"
        FROM futures_macd_signal_v2
        WHERE ContractId = :contractId
        AND TimePeriod = :timePeriod
        AND SignalEmaPeriod = :signalEmaPeriod
        AND FastEmaPeriod = :fastEmaPeriod
        AND SlowEmaPeriod = :slowEmaPeriod
        LIMIT 1;
    """;

    public const string InsertFuturesAtrSignal = """
        INSERT INTO futures_atr_signal (
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            timestamp,
            futuresPrice,
            atrValue,
            trueRange,
            atr,
            atrStrength,
            configurationId,
            observationId,
            marketDataAsOf,
            sourceSequence,
            calculationVersion,
            calculationMethod,
            schemaVersion,
            isValid,
            previousAtrValue,
            atrBaseline,
            atrRatio,
            isWarm
        ) VALUES (
            :contractId,
            :valueDate,
            :timePeriod,
            :periodLength,
            :timestamp,
            :futuresPrice,
            :atrValue,
            :trueRange,
            :atr,
            :atrStrength,
            :configurationId,
            :observationId,
            :marketDataAsOf,
            :sourceSequence,
            :calculationVersion,
            :calculationMethod,
            :schemaVersion,
            :isValid,
            :previousAtrValue,
            :atrBaseline,
            :atrRatio,
            :isWarm
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
            ATRStrength AS "ATRStrength",
            PreviousAtrValue AS "PreviousAtrValue",
            AtrBaseline AS "AtrBaseline",
            AtrRatio AS "AtrRatio",
            IsWarm AS "IsWarm",
            ConfigurationId AS "ConfigurationId",
            ObservationId AS "ObservationId",
            MarketDataAsOf AS "MarketDataAsOf",
            SourceSequence AS "SourceSequence",
            CalculationVersion AS "CalculationVersion",
            CalculationMethod AS "CalculationMethod",
            SchemaVersion AS "SchemaVersion",
            IsValid AS "IsValid"
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
            ATRStrength AS "ATRStrength",
            PreviousAtrValue AS "PreviousAtrValue",
            AtrBaseline AS "AtrBaseline",
            AtrRatio AS "AtrRatio",
            IsWarm AS "IsWarm",
            ConfigurationId AS "ConfigurationId",
            ObservationId AS "ObservationId",
            MarketDataAsOf AS "MarketDataAsOf",
            SourceSequence AS "SourceSequence",
            CalculationVersion AS "CalculationVersion",
            CalculationMethod AS "CalculationMethod",
            SchemaVersion AS "SchemaVersion",
            IsValid AS "IsValid"
        FROM futures_atr_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        LIMIT 1;
    """;

    public const string DeleteFuturesAtrSignal = """
        DELETE FROM futures_atr_signal
        WHERE contractId = :contractId
        AND timePeriod = :timePeriod
        AND periodLength = :periodLength
        AND valueDate = :valueDate
    """;

    public const string InsertFuturesAdxSignal = """
        INSERT INTO futures_adx_signal (
            contractId,
            valueDate,
            timePeriod,
            periodLength,
            timestamp,
            futuresPrice,
            plusDI,
            minusDI,
            adxValue,
            adx,
            adxStrength,
            configurationId,
            observationId,
            marketDataAsOf,
            sourceSequence,
            calculationVersion,
            calculationMethod,
            schemaVersion,
            isValid
        ) VALUES (
            :contractId,
            :valueDate,
            :timePeriod,
            :periodLength,
            :timestamp,
            :futuresPrice,
            :plusDI,
            :minusDI,
            :adxValue,
            :adx,
            :adxStrength,
            :configurationId,
            :observationId,
            :marketDataAsOf,
            :sourceSequence,
            :calculationVersion,
            :calculationMethod,
            :schemaVersion,
            :isValid
        );
    """;

    public const string GetLastFuturesAdxSignal = """
        SELECT ContractId AS "ContractId",
            ValueDate AS "ValueDate",
            TimePeriod as "TimePeriod",
            PeriodLength as "PeriodLength",
            Timestamp AS "Timestamp",
            FuturesPrice AS "FuturesPrice",
            PlusDI AS "PlusDI",
            MinusDI AS "MinusDI",
            AdxValue AS "AdxValue",
            ADX AS "ADX",
            ADXStrength AS "ADXStrength",
            ConfigurationId AS "ConfigurationId",
            ObservationId AS "ObservationId",
            MarketDataAsOf AS "MarketDataAsOf",
            SourceSequence AS "SourceSequence",
            CalculationVersion AS "CalculationVersion",
            CalculationMethod AS "CalculationMethod",
            SchemaVersion AS "SchemaVersion",
            IsValid AS "IsValid"
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
            FuturesPrice AS "FuturesPrice",
            PlusDI AS "PlusDI",
            MinusDI AS "MinusDI",
            AdxValue AS "AdxValue",
            ADX AS "ADX",
            ADXStrength AS "ADXStrength",
            ConfigurationId AS "ConfigurationId",
            ObservationId AS "ObservationId",
            MarketDataAsOf AS "MarketDataAsOf",
            SourceSequence AS "SourceSequence",
            CalculationVersion AS "CalculationVersion",
            CalculationMethod AS "CalculationMethod",
            SchemaVersion AS "SchemaVersion",
            IsValid AS "IsValid"
        FROM futures_adx_signal
        WHERE ContractId = :contractId 
        AND TimePeriod = :timePeriod
        AND PeriodLength = :periodLength
        LIMIT 1;
    """;

    public const string DeleteFuturesAdxSignal = """
        DELETE FROM futures_adx_signal
        WHERE contractId = :contractId
        AND timePeriod = :timePeriod
        AND periodLength = :periodLength
        AND valueDate = :valueDate
    """;

    public const string UpsertMarketOutlookSnapshot = """
        INSERT INTO market_outlook_snapshot
            (contractId, valueDate, revision, updatedOn, eodData, futuresTradeSignal, missingInputs)
        VALUES
            (:contractId, :valueDate, :revision, :updatedOn, :eodData, :futuresTradeSignal, :missingInputs);
    """;

    public const string GetMarketOutlookSnapshot = """
        SELECT contractId AS "ContractId",
            valueDate AS "ValueDate",
            revision AS "Revision",
            updatedOn AS "UpdatedOn",
            eodData AS "EodData",
            futuresTradeSignal AS "FuturesTradeSignal",
            missingInputs AS "MissingInputs"
        FROM market_outlook_snapshot
        WHERE contractId = :contractId
        AND valueDate <= :valueDate
        LIMIT 1;
    """;

    public const string UpsertMarketOutlookWorkingState = """
        INSERT INTO market_outlook_working_state
            (contractId, valueDate, revision, updatedOn, status, workingState)
        VALUES
            (:contractId, :valueDate, :revision, :updatedOn, :status, :workingState);
    """;

    public const string GetMarketOutlookWorkingState = """
        SELECT workingState AS "WorkingState"
        FROM market_outlook_working_state
        WHERE contractId = :contractId
        AND valueDate = :valueDate;
    """;

}
