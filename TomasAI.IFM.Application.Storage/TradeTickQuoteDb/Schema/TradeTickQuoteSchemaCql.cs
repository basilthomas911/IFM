namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;

internal static class TradeTickQuoteSchemaCql
{
    /// <summary>Stores only short-lived infrastructure capability probes, never application domain state.</summary>
    public const string CreateRecoveryProbeTable = """
CREATE TABLE IF NOT EXISTS ifm_recovery_probe (
    probe_id uuid PRIMARY KEY,
    observed_at_utc timestamp
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
    optionPrice double,
    bidPrice double,
    askPrice double,
    bidSize int,
    askSize int,
    impliedVolatility double,
    underlyingPrice double,
    delta double,
    gamma double,
    vega double,
    theta double,
    rho double,
    PRIMARY KEY (contractId, valueDate, tickId)
    ) WITH CLUSTERING ORDER BY (valueDate ASC, tickId ASC);
    """;
}
