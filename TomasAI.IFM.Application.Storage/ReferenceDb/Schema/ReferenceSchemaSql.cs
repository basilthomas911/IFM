namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

/// <summary>Native PostgreSQL reference tables and explicit history-order indexes. No runtime CQL translation.</summary>
public static class ReferenceSchemaSql
{
    /// <summary>Drops the ReferenceDb PostgreSQL schema during an explicit schema teardown.</summary>
    public const string Drop = "DROP SCHEMA IF EXISTS reference CASCADE;";

    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS reference;
        
        CREATE TABLE IF NOT EXISTS reference.instrument_definition (
        snapshot_id uuid, dataset text, bucket int, record_index bigint,
                    publisher_id int, instrument_id bigint, raw_symbol text, asset text,
                    instrument_class text, currency text, exchange text, definition_json text,
                    PRIMARY KEY (snapshot_id,dataset,bucket,record_index)
        );
        
        CREATE TABLE IF NOT EXISTS reference.instrument_definition_product (
        snapshot_id uuid, family int, symbol text, exchange text, currency text, symbol_id int,
                    PRIMARY KEY (snapshot_id,family,symbol,exchange,currency)
        );
        
        CREATE TABLE IF NOT EXISTS reference.instrument_definition_snapshot (
        catalog text PRIMARY KEY, snapshot_id uuid, completed_utc timestamptz, record_count bigint, datasets_json text
        );
        
        CREATE TABLE IF NOT EXISTS reference.instrument_definition_selection (
        snapshot_id uuid, dataset text, root text, options boolean, instrument_key text, payload bytea,
                    PRIMARY KEY (snapshot_id,dataset,root,options,instrument_key)
        );
        
        CREATE TABLE IF NOT EXISTS reference.instrument_definition_selection_status (
        snapshot_id uuid PRIMARY KEY, complete boolean
        );
        
        CREATE TABLE IF NOT EXISTS reference.trade_strategy_symbol (
        family int, exchange text, symbol text, currency text, id int,
                  PRIMARY KEY (family, exchange, symbol, currency)
        );
        
        CREATE TABLE IF NOT EXISTS reference.trade_strategy_family_catalog (
        catalog text PRIMARY KEY, revision bigint, payload_json text
        );
        
        CREATE TABLE IF NOT EXISTS reference.reference_projection_state (
        projectionName text PRIMARY KEY,
            generation uuid,
            completed boolean,
            completedOn timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS reference.reference_projection_mutation (
        projectionName text,
            mutationId uuid,
            startedOn timestamptz,
            PRIMARY KEY (projectionName, mutationId)
        );
        
        CREATE TABLE IF NOT EXISTS reference.reference_projection_ownership (
        projectionName text PRIMARY KEY,
            ownerMutationId uuid,
            conflicted boolean,
            claimedOn timestamptz
        );
        
        CREATE TABLE IF NOT EXISTS reference.lookup_type (
        LookupTypeName text,
            ShortCode text,
            OrderId int,
            Description text,
            CreatedOn timestamptz,
            CreatedBy text,
            PRIMARY KEY (LookupTypeName, OrderId, ShortCode)
        );
        
        CREATE TABLE IF NOT EXISTS reference.mdi_forward_loss_ratio (
        trendDirection text,
            tradeType text,
            mdi int,
            forwardLossRatio double precision,
            createdBy text,
            createdOn timestamptz,
            updatedBy text,
            updatedOn timestamptz,
            PRIMARY KEY (trendDirection, tradeType, mdi)
        );
        
        CREATE TABLE IF NOT EXISTS reference.scheduled_job_days (
        jobId int PRIMARY KEY,
            monday boolean,
            tuesday boolean,
            wednesday boolean,
            thursday boolean,
            friday boolean,
            saturday boolean,
            sunday boolean
        );
        
        CREATE TABLE IF NOT EXISTS reference.scheduled_job (
        JobId int,
            JobName text,
            JobSchedule text,
            JobScheduleDate timestamptz,
            JobScheduleInterval double precision,
            TaskName text,
            TaskEnabled boolean,
            CreatedOn timestamptz,
            CreatedBy text,
            UpdatedOn timestamptz,
            UpdatedBy text,
            PRIMARY KEY (JobId, JobName)
        );
        
        -- Fail migration on conflicting legacy facts rather than choosing a winner.
        CREATE UNIQUE INDEX IF NOT EXISTS scheduled_job_unique_id ON reference.scheduled_job(jobId);
        CREATE UNIQUE INDEX IF NOT EXISTS scheduled_job_unique_name ON reference.scheduled_job(jobName COLLATE "C");

        CREATE TABLE IF NOT EXISTS reference.scheduled_job_by_name (
        jobName text PRIMARY KEY,
            jobId int,
            reservationToken uuid
        );
        
        CREATE TABLE IF NOT EXISTS reference.scheduled_job_write_ownership (
        scopeType text,
            scopeKey text,
            operationId uuid,
            startedOn timestamptz,
            PRIMARY KEY (scopeType, scopeKey)
        );
        
        CREATE TABLE IF NOT EXISTS reference.trade_strategy_family (
        catalog text, tradeStrategyFamilyId int, definitionVersion bigint,
            systemKey text, family text, strategy text, timeFrame text, symbol text, currency text,
            description text, state text, createdOnUtc timestamptz, createdBy text,
            PRIMARY KEY (catalog, systemKey, definitionVersion)
        );
        
        CREATE INDEX IF NOT EXISTS trade_strategy_family_ordered ON reference.trade_strategy_family (catalog, systemKey ASC, definitionVersion DESC);
        """;
}
