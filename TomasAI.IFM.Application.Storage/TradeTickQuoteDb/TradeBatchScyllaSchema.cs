namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Additive raw-trade chunk schema. All identities remain immutable across redelivery.</summary>
public static class TradeBatchScyllaSchema
{
    public const string CreateItem = """
        CREATE TYPE IF NOT EXISTS tick_trade_item_v1 (
            source_sequence bigint, event_ns bigint, receive_ns bigint, header_flags smallint,
            price_raw bigint, price decimal, size bigint, action smallint, side smallint, dbn_flags smallint,
            sequence_id bigint, aggregation_ticks bigint, event_id uuid, command_id uuid, event_log_id bigint,
            aggregate_id text, event_source text, received_ticks bigint, acceptance_ordinal bigint
        );
        """;
    public const string CreateManifest = """
        CREATE TABLE IF NOT EXISTS tick_trade_batch_manifest_v1 (
            asset_type_id tinyint, contract_id text, value_date date, batch_id uuid,
            content_hash blob, chunk_count int, dataset text, definition_date date,
            publisher_id int, instrument_id bigint, source_epoch uuid,
            PRIMARY KEY ((asset_type_id, contract_id, value_date), batch_id)
        );
        """;
    public const string CreateChunks = """
        CREATE TABLE IF NOT EXISTS tick_trade_batch_data_v1 (
            asset_type_id tinyint, contract_id text, value_date date, utc_hour int,
            event_second bigint, chunk_id uuid, batch_id uuid, chunk_index int,
            content_hash blob, record_count int, min_event_ns bigint, max_event_ns bigint,
            dataset text, definition_date date, publisher_id int, instrument_id bigint, source_epoch uuid,
            trade_data frozen<list<frozen<tick_trade_item_v1>>>,
            PRIMARY KEY ((asset_type_id, contract_id, value_date, utc_hour), event_second, chunk_id)
        );
        """;
    public const string InsertManifest = """
        INSERT INTO tick_trade_batch_manifest_v1
        (asset_type_id,contract_id,value_date,batch_id,content_hash,chunk_count,dataset,definition_date,publisher_id,instrument_id,source_epoch)
        VALUES (?,?,?,?,?,?,?,?,?,?,?) IF NOT EXISTS;
        """;
    public const string ReadManifest = """
        SELECT content_hash,chunk_count FROM tick_trade_batch_manifest_v1
        WHERE asset_type_id=? AND contract_id=? AND value_date=? AND batch_id=?;
        """;
    public const string InsertChunk = """
        INSERT INTO tick_trade_batch_data_v1
        (asset_type_id,contract_id,value_date,utc_hour,event_second,chunk_id,batch_id,chunk_index,
         content_hash,record_count,min_event_ns,max_event_ns,dataset,definition_date,publisher_id,instrument_id,source_epoch,trade_data)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) IF NOT EXISTS;
        """;
    public const string ReadChunk = """
        SELECT content_hash FROM tick_trade_batch_data_v1
        WHERE asset_type_id=? AND contract_id=? AND value_date=? AND utc_hour=? AND event_second=? AND chunk_id=?;
        """;
    public const string ReadRange = """
        SELECT event_second,chunk_id,content_hash,record_count,trade_data FROM tick_trade_batch_data_v1
        WHERE asset_type_id=? AND contract_id=? AND value_date=? AND utc_hour=? AND event_second>=? AND event_second<=?;
        """;
}
