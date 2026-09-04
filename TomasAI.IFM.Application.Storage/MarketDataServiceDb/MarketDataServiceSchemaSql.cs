namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

public static class MarketDataServiceSchemaSql
{
    public const string CreateSchema = "CREATE SCHEMA IF NOT EXISTS market_data_service;";
    public const string CreateAssignments = """
        CREATE TABLE IF NOT EXISTS market_data_service.futures_rollover_contract_assignment (
          contract_role text PRIMARY KEY,
          root_symbol text NOT NULL,
          contract_id text NOT NULL UNIQUE,
          description text NOT NULL,
          local_symbol text NOT NULL,
          security_type text NOT NULL,
          currency text NOT NULL,
          exchange text NOT NULL,
          multiplier text NOT NULL,
          last_trade_date date NOT NULL,
          next_rollover_date date NOT NULL,
          source_contract_hash text NOT NULL,
          row_version bigint NOT NULL CHECK(row_version>0),
          created_on_utc timestamptz NOT NULL,
          created_by text NOT NULL CHECK(length(created_by)>0),
          updated_on_utc timestamptz NOT NULL,
          updated_by text NOT NULL CHECK(length(updated_by)>0),
          CONSTRAINT ck_market_data_assignment_role_root CHECK(
            (contract_role='EsQuarterly' AND root_symbol='ES') OR
            (contract_role IN ('VxFrontMonth','VxSecondMonth') AND root_symbol='VX')),
          CONSTRAINT ck_market_data_assignment_identifiers CHECK(length(contract_id)>0 AND length(local_symbol)>0),
          CONSTRAINT ck_market_data_assignment_hash CHECK(length(source_contract_hash)=64)
        );
        """;
    public const string CreateWatchdogLog = """
        CREATE TABLE IF NOT EXISTS market_data_service.watchdog_status_log (
          watchdog_status_log_id bigint PRIMARY KEY,
          observation_id uuid NOT NULL UNIQUE,
          correlation_id uuid NOT NULL,
          value_date date NOT NULL,
          observed_on_utc timestamptz NOT NULL,
          operation_reason text NOT NULL,
          major_status text NOT NULL CHECK(major_status IN ('Up','Resetting','Down')),
          display_health text NOT NULL CHECK(display_health IN ('Green','Yellow','Orange','Red','Inactive')),
          core_contracts_ready boolean NOT NULL,
          recovery_attempt integer NOT NULL CHECK(recovery_attempt BETWEEN 0 AND 3),
          native_backend text NOT NULL,
          native_abi_version integer NOT NULL,
          native_generation uuid NOT NULL,
          failure_stage text NOT NULL,
          failure_detail text NOT NULL CHECK(length(failure_detail)<=512),
          feed_status_details jsonb NOT NULL,
          row_version bigint NOT NULL CHECK(row_version>0),
          created_on_utc timestamptz NOT NULL,
          created_by text NOT NULL,
          updated_on_utc timestamptz NOT NULL,
          updated_by text NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_watchdog_status_log_observed ON market_data_service.watchdog_status_log(observed_on_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_watchdog_status_log_value_date ON market_data_service.watchdog_status_log(value_date,observed_on_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_watchdog_status_log_major ON market_data_service.watchdog_status_log(major_status,observed_on_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_watchdog_status_log_core_ready ON market_data_service.watchdog_status_log(core_contracts_ready,observed_on_utc DESC);
        """;
    public const string CreateDatasetIncidents = """
        CREATE TABLE IF NOT EXISTS market_data_service.dataset_incident_current (
          dataset text PRIMARY KEY CHECK(length(dataset) BETWEEN 1 AND 64),
          value_date date NOT NULL,
          incident_id uuid NOT NULL,
          transition_id uuid NOT NULL UNIQUE,
          correlation_id uuid NOT NULL,
          observed_on_utc timestamptz NOT NULL,
          is_open boolean NOT NULL,
          snapshot jsonb NOT NULL,
          row_version bigint NOT NULL CHECK(row_version>0)
        );
        CREATE INDEX IF NOT EXISTS ix_dataset_incident_current_open
          ON market_data_service.dataset_incident_current(is_open,value_date);
        CREATE TABLE IF NOT EXISTS market_data_service.dataset_incident_transition (
          transition_id uuid PRIMARY KEY,
          incident_id uuid NOT NULL,
          correlation_id uuid NOT NULL,
          dataset text NOT NULL CHECK(length(dataset) BETWEEN 1 AND 64),
          value_date date NOT NULL,
          observed_on_utc timestamptz NOT NULL,
          is_open boolean NOT NULL,
          snapshot jsonb NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_dataset_incident_transition_dataset
          ON market_data_service.dataset_incident_transition(dataset,value_date,observed_on_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_dataset_incident_transition_correlation
          ON market_data_service.dataset_incident_transition(correlation_id);
        """;
}
