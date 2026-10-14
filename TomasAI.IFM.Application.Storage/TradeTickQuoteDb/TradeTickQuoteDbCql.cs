namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Fixed Scylla queries and mutations for direct raw market observations.</summary>
public static class TradeTickQuoteDbCql
{
    public const string GetLatestRawFuturesTrades = """
        SELECT value_date, price, size, source_sequence, source_event_timestamp_ns,
            source_receive_timestamp_ns, action, header_flags, instrument_id, publisher_id
        FROM tick_trade_data WHERE asset_type_id = ? AND contract_id = ? AND value_date <= ?
        ORDER BY value_date DESC, aggregation_time DESC, sequence_id DESC LIMIT 64;
        """;

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

    public const string InsertFuturesTickData = """
        INSERT INTO futures_tick_data (contractId, valueDate, tickId, tickTime, price, size)
        VALUES (:contractId, :valueDate, :tickId, :tickTime, :price, :size);
    """;

    public const string InsertFuturesTickDataByTime = """
        INSERT INTO futures_tick_data_by_time (contractId, valueDate, tickTime, tickId, price, size)
        VALUES (:contractId, :valueDate, :tickTime, :tickId, :price, :size);
    """;

    public const string InsertFuturesOptionTickData = """
        INSERT INTO futures_option_tick_data (
            contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho
        ) VALUES (
            :contractId, :valueDate, :tickId, :tickTime, :optionPrice, :bidPrice, :askPrice, :bidSize, :askSize, :impliedVolatility, :underlyingPrice, :delta, :gamma, :vega, :theta, :rho
        );
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

    public const string GetFuturesTickAtOrBefore = """
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
        AND tickTime <= :tickTime
        ORDER BY tickTime DESC, tickId DESC
        LIMIT 1;
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

    public const string GetLastFuturesTickDataId = """
        SELECT 
            contractId AS "ContractId", 
            valueDate AS "ValueDate", 
            MAX(tickId) AS "TickId"
        FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate
        GROUP BY contractId, valueDate;
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
        ORDER BY valueDate DESC, tickId DESC
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

    public const string DeleteFuturesTickData = """
        DELETE FROM futures_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesTickDataByTime = """
        DELETE FROM futures_tick_data_by_time
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;

    public const string DeleteFuturesOptionTickData = """
        DELETE FROM futures_option_tick_data
        WHERE contractId = :contractId AND valueDate = :valueDate;
    """;


}
