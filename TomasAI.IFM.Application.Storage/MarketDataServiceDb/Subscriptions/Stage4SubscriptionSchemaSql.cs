namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;

/// <summary>Additive schema, deliberately not registered in application startup. Explicit test application only.</summary>
public static class Stage4SubscriptionSchemaSql
{
    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS market_data_service;
        CREATE TABLE IF NOT EXISTS market_data_service.stage4_intent_current (
          scope text NOT NULL CHECK(length(scope) BETWEEN 1 AND 128),
          dataset text NOT NULL CHECK(length(dataset) BETWEEN 1 AND 64),
          revision bigint NOT NULL CHECK(revision>=0),
          snapshot jsonb NOT NULL CHECK(octet_length(snapshot::text)<=16777216),
          PRIMARY KEY(scope,dataset)
        );
        CREATE TABLE IF NOT EXISTS market_data_service.stage4_intent_operation (
          scope text NOT NULL, dataset text NOT NULL, operation_id uuid NOT NULL,
          request_digest text NOT NULL CHECK(length(request_digest)=64),
          result jsonb NOT NULL CHECK(octet_length(result::text)<=4096),
          created_at_utc timestamptz NOT NULL,
          PRIMARY KEY(scope,dataset,operation_id),
          FOREIGN KEY(scope,dataset) REFERENCES market_data_service.stage4_intent_current(scope,dataset)
        );
        CREATE TABLE IF NOT EXISTS market_data_service.stage4_intent_outbox (
          scope text NOT NULL, dataset text NOT NULL, transition_id uuid NOT NULL,
          operation_id uuid NOT NULL, revision bigint NOT NULL CHECK(revision>0),
          payload jsonb NOT NULL CHECK(octet_length(payload::text)<=4096),
          created_at_utc timestamptz NOT NULL, delivered_at_utc timestamptz NULL,
          PRIMARY KEY(scope,dataset,transition_id),
          UNIQUE(scope,dataset,revision),
          FOREIGN KEY(scope,dataset,operation_id) REFERENCES market_data_service.stage4_intent_operation(scope,dataset,operation_id)
        );
        CREATE INDEX IF NOT EXISTS ix_stage4_intent_outbox_pending
          ON market_data_service.stage4_intent_outbox(scope,dataset,revision) WHERE delivered_at_utc IS NULL;
        CREATE TABLE IF NOT EXISTS market_data_service.stage4_authority_watermark (
          scope text NOT NULL, dataset text NOT NULL, source_id text NOT NULL CHECK(length(source_id) BETWEEN 1 AND 256),
          source_version bigint NOT NULL CHECK(source_version>0), source_event_id uuid NOT NULL,
          fact_digest text NOT NULL CHECK(length(fact_digest)=64),
          owner_digest text NOT NULL CHECK(length(owner_digest)=64),
          PRIMARY KEY(scope,dataset,source_id),
          UNIQUE(scope,dataset,owner_digest),
          FOREIGN KEY(scope,dataset) REFERENCES market_data_service.stage4_intent_current(scope,dataset)
        );
        CREATE TABLE IF NOT EXISTS market_data_service.stage4_lease_identity (
          scope text NOT NULL, dataset text NOT NULL, lease_id uuid NOT NULL,
          source_id text NOT NULL CHECK(length(source_id) BETWEEN 1 AND 256),
          owner_digest text NOT NULL CHECK(length(owner_digest)=64),
          lease_digest text NOT NULL CHECK(length(lease_digest)=64),
          created_revision bigint NOT NULL CHECK(created_revision>0),
          released_revision bigint NULL CHECK(released_revision>created_revision),
          PRIMARY KEY(scope,dataset,lease_id),
          FOREIGN KEY(scope,dataset) REFERENCES market_data_service.stage4_intent_current(scope,dataset)
        );
        """;
}
