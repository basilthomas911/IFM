namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>CQL statements for raw ScyllaDB trades and quotes.</summary>
public static class TradeTickQuoteDbSql
{
    /// <summary>CQL for delete futures option tick data.</summary>
    internal const string DeleteFuturesOptionTickData =
        """
        DELETE FROM futures_option_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate;
        """;

    /// <summary>CQL for delete futures tick data.</summary>
    internal const string DeleteFuturesTickData =
        """
        DELETE FROM futures_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate;
        """;

    /// <summary>CQL for delete futures tick data by time.</summary>
    internal const string DeleteFuturesTickDataByTime =
        """
        DELETE FROM futures_tick_data_by_time
            WHERE contractid = :contractId AND valuedate = :valueDate;
        """;

    /// <summary>CQL for get futures option tick data.</summary>
    internal const string GetFuturesOptionTickData =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                optionprice AS "OptionPrice",
                bidprice AS "BidPrice",
                askprice AS "AskPrice",
                bidsize AS "BidSize",
                asksize AS "AskSize",
                impliedvolatility AS "ImpliedVolatility",
                underlyingprice AS "UnderlyingPrice",
                delta AS "Delta",
                gamma AS "Gamma",
                vega AS "Vega",
                theta AS "Theta",
                rho AS "Rho"
            FROM futures_option_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate AND tickid = :tickId;
        """;

    /// <summary>CQL for get futures tick at or before.</summary>
    internal const string GetFuturesTickAtOrBefore =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                price AS "Price",
                size AS "Size"
            FROM futures_tick_data_by_time
            WHERE contractid = :contractId
            AND valuedate = :valueDate
            AND ticktime <= :tickTime
            ORDER BY ticktime DESC, tickid DESC
            LIMIT 1;
        """;

    /// <summary>CQL for get futures tick data.</summary>
    internal const string GetFuturesTickData =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                price AS "Price",
                size AS "Size"
            FROM futures_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate AND tickid = :tickId;
        """;

    /// <summary>CQL for get futures tick data by date.</summary>
    internal const string GetFuturesTickDataByDate =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                price AS "Price",
                size AS "Size"
            FROM futures_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate;
        """;

    /// <summary>CQL for get futures tick hlvdata.</summary>
    internal const string GetFuturesTickHLVData =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                MAX(price) AS "HighPrice",
                MIN(price) AS "LowPrice",
                SUM(CAST(size AS bigint)) AS "Volume"
            FROM futures_tick_data
            WHERE contractid = :contractId
            AND valuedate = :valueDate
            GROUP BY contractid, valuedate;
        """;

    /// <summary>CQL for get last futures option tick data.</summary>
    internal const string GetLastFuturesOptionTickData =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                optionprice AS "OptionPrice",
                bidprice AS "BidPrice",
                askprice AS "AskPrice",
                bidsize AS "BidSize",
                asksize AS "AskSize",
                impliedvolatility AS "ImpliedVolatility",
                underlyingprice AS "UnderlyingPrice",
                delta AS "Delta",
                gamma AS "Gamma",
                vega AS "Vega",
                theta AS "Theta",
                rho AS "Rho"
            FROM futures_option_tick_data
            WHERE contractid = :contractId
            AND valuedate = :valueDate
            ORDER BY valuedate DESC, tickid DESC
            LIMIT 1;
        """;

    /// <summary>CQL for get last futures option tick data id.</summary>
    internal const string GetLastFuturesOptionTickDataId =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                MAX(tickid) AS "TickId"
            FROM futures_option_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate
            GROUP BY contractid, valuedate;
        """;

    /// <summary>CQL for get last futures tick data.</summary>
    internal const string GetLastFuturesTickData =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                tickid AS "TickId",
                ticktime AS "TickTime",
                price AS "Price",
                size AS "Size"
            FROM futures_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate
            ORDER BY valuedate DESC, tickid DESC
            LIMIT 1;
        """;

    /// <summary>CQL for get last futures tick data id.</summary>
    internal const string GetLastFuturesTickDataId =
        """
        SELECT
                contractid AS "ContractId",
                valuedate AS "ValueDate",
                MAX(tickid) AS "TickId"
            FROM futures_tick_data
            WHERE contractid = :contractId AND valuedate = :valueDate
            GROUP BY contractid, valuedate;
        """;

    /// <summary>CQL for get latest raw futures trades.</summary>
    internal const string GetLatestRawFuturesTrades =
        """
        SELECT value_date, price, size, source_sequence, source_event_timestamp_ns,
            source_receive_timestamp_ns, action, header_flags, instrument_id, publisher_id
        FROM tick_trade_data WHERE asset_type_id = ? AND contract_id = ? AND value_date <= ?
        ORDER BY value_date DESC, aggregation_time DESC, sequence_id DESC LIMIT 64;
        """;

    /// <summary>CQL for get raw futures trade window.</summary>
    internal const string GetRawFuturesTradeWindow =
        """
        SELECT value_date, price, size, source_sequence, source_event_timestamp_ns,
            source_receive_timestamp_ns, action, header_flags, instrument_id, publisher_id, aggregation_timestamp_utc_ticks
        FROM tick_trade_data WHERE asset_type_id = ? AND contract_id = ? AND value_date = ?
            AND aggregation_time >= ? AND aggregation_time <= ?;
        """;

    /// <summary>CQL for get tick quote data exact range.</summary>
    internal const string GetTickQuoteDataExactRange =
        """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND (value_date, aggregation_time) >= (?, ?)
          AND (value_date, aggregation_time) <= (?, ?);
        """;

    /// <summary>CQL for get tick quote data intraday range.</summary>
    internal const string GetTickQuoteDataIntradayRange =
        """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ? AND value_date = ?
          AND aggregation_time >= ? AND aggregation_time <= ?;
        """;

    /// <summary>CQL for get tick quote data range.</summary>
    internal const string GetTickQuoteDataRange =
        """
        SELECT * FROM tick_quote_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND value_date >= ? AND value_date <= ?;
        """;

    /// <summary>CQL for get tick trade data exact range.</summary>
    internal const string GetTickTradeDataExactRange =
        """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND (value_date, aggregation_time) >= (?, ?)
          AND (value_date, aggregation_time) <= (?, ?);
        """;

    /// <summary>CQL for get tick trade data intraday range.</summary>
    internal const string GetTickTradeDataIntradayRange =
        """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ? AND value_date = ?
          AND aggregation_time >= ? AND aggregation_time <= ?;
        """;

    /// <summary>CQL for get tick trade data range.</summary>
    internal const string GetTickTradeDataRange =
        """
        SELECT * FROM tick_trade_data
        WHERE asset_type_id = ? AND contract_id = ?
          AND value_date >= ? AND value_date <= ?;
        """;

    /// <summary>CQL for insert futures option tick data.</summary>
    internal const string InsertFuturesOptionTickData =
        """
        INSERT INTO futures_option_tick_data (
                contractid, valuedate, tickid, ticktime, optionprice, bidprice, askprice, bidsize, asksize,
                impliedvolatility, underlyingprice, delta, gamma, vega, theta, rho
            ) VALUES (
                :contractId, :valueDate, :tickId, :tickTime, :optionPrice, :bidPrice, :askPrice, :bidSize, :askSize,
                :impliedVolatility, :underlyingPrice, :delta, :gamma, :vega, :theta, :rho
            );
        """;

    /// <summary>CQL for insert futures tick data.</summary>
    internal const string InsertFuturesTickData =
        """
        INSERT INTO futures_tick_data (contractid, valuedate, tickid, ticktime, price, size)
            VALUES (:contractId, :valueDate, :tickId, :tickTime, :price, :size);
        """;

    /// <summary>CQL for insert futures tick data by time.</summary>
    internal const string InsertFuturesTickDataByTime =
        """
        INSERT INTO futures_tick_data_by_time (contractid, valuedate, ticktime, tickid, price, size)
            VALUES (:contractId, :valueDate, :tickTime, :tickId, :price, :size);
        """;

    /// <summary>CQL for insert tick quote data.</summary>
    internal const string InsertTickQuoteData =
        """
        INSERT INTO tick_quote_data (
            asset_type_id, contract_id, value_date, aggregation_time, sequence_id,
            aggregation_timestamp_utc, aggregation_timestamp_utc_ticks,
            schema_version, dataset, definition_date, publisher_id, instrument_id,
            actor_event_id, actor_event_log_id, command_id, aggregate_id,
            event_source, received_on, emission_reason, quote_count, quote_data)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
        """;

    /// <summary>CQL for insert tick trade data.</summary>
    internal const string InsertTickTradeData =
        """
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
}
