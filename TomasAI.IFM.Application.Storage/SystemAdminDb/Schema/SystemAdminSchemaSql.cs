namespace TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;

public static class SystemAdminSchemaSql
{
    public const string CreateSchema = "CREATE SCHEMA IF NOT EXISTS system_admin;";

    public const string CreateRecoveryOperation = """
CREATE TABLE IF NOT EXISTS system_admin.database_recovery_operation (
    operation_id uuid PRIMARY KEY,
    backup_set_id uuid NULL,
    protection_set_id text NOT NULL,
    source smallint NOT NULL,
    operation_kind smallint NOT NULL,
    phase smallint NOT NULL,
    outcome smallint NOT NULL,
    progress_percent integer NOT NULL CHECK (progress_percent BETWEEN 0 AND 100),
    state_revision bigint NOT NULL,
    created_utc timestamptz NOT NULL,
    completed_utc timestamptz NULL,
    safe_diagnostic_reference text NOT NULL DEFAULT '',
    restore_point_id text NULL,
    restore_class smallint NOT NULL DEFAULT 0,
    fresh_target_profile text NOT NULL DEFAULT '',
    validation_revision bigint NOT NULL DEFAULT 0,
    cutover_state smallint NOT NULL DEFAULT 0,
    policy_revision bigint NOT NULL DEFAULT 0,
    backup_lineage_json text NOT NULL DEFAULT '',
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL
);
ALTER TABLE system_admin.database_recovery_operation
    ADD COLUMN IF NOT EXISTS backup_lineage_json text NOT NULL DEFAULT '';
CREATE INDEX IF NOT EXISTS ix_database_recovery_operation_history
    ON system_admin.database_recovery_operation (source, protection_set_id, created_utc DESC, operation_id);
CREATE INDEX IF NOT EXISTS ix_database_recovery_operation_backup_set
    ON system_admin.database_recovery_operation (backup_set_id) WHERE backup_set_id IS NOT NULL;
""";

    public const string CreateRecoveryPhase = """
CREATE TABLE IF NOT EXISTS system_admin.database_recovery_phase (
    operation_id uuid NOT NULL,
    phase smallint NOT NULL,
    event_revision bigint NOT NULL,
    outcome smallint NOT NULL,
    progress_percent integer NOT NULL,
    observed_utc timestamptz NOT NULL,
    host_id text NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (operation_id, phase, event_revision)
);
""";

    public const string CreateRecoveryRunStats = """
CREATE TABLE IF NOT EXISTS system_admin.database_recovery_run_stats (
    operation_id uuid NOT NULL,
    source smallint NOT NULL,
    phase smallint NOT NULL,
    engine smallint NOT NULL,
    statistics_revision bigint NOT NULL,
    started_utc timestamptz NULL,
    completed_utc timestamptz NULL,
    elapsed_ticks bigint NULL,
    source_bytes bigint NULL,
    stored_bytes bigint NULL,
    transferred_bytes bigint NULL,
    restored_bytes bigint NULL,
    artifact_count integer NULL,
    average_throughput double precision NULL,
    peak_throughput double precision NULL,
    retry_count integer NULL,
    warning_count integer NULL,
    achieved_rpo_ticks bigint NULL,
    achieved_rto_ticks bigint NULL,
    host_id text NULL,
    policy_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (operation_id, phase, engine, statistics_revision)
);
""";

    public const string CreateRestorePoint = """
CREATE TABLE IF NOT EXISTS system_admin.database_restore_point (
    restore_point_id text NOT NULL,
    source smallint NOT NULL,
    backup_set_id uuid NULL,
    protection_set_id text NOT NULL,
    recovery_point_utc timestamptz NOT NULL,
    verification_level smallint NOT NULL DEFAULT 0,
    verified_utc timestamptz NULL,
    restore_tested_utc timestamptz NULL,
    eligible boolean NOT NULL DEFAULT false,
    legal_hold boolean NOT NULL DEFAULT false,
    manifest_revision bigint NOT NULL DEFAULT 0,
    backup_lineage_json text NOT NULL DEFAULT '',
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (restore_point_id, source)
);
ALTER TABLE system_admin.database_restore_point
    ADD COLUMN IF NOT EXISTS backup_lineage_json text NOT NULL DEFAULT '';
CREATE INDEX IF NOT EXISTS ix_database_restore_point_latest
    ON system_admin.database_restore_point (source, protection_set_id, recovery_point_utc DESC);
""";

    public const string CreateArtifactReplica = """
CREATE TABLE IF NOT EXISTS system_admin.database_artifact_replica (
    artifact_replica_id text NOT NULL,
    source smallint NOT NULL,
    operation_id uuid NOT NULL,
    artifact_id text NOT NULL,
    engine smallint NOT NULL,
    replica_state smallint NOT NULL,
    safe_destination_reference text NOT NULL DEFAULT '',
    bytes bigint NULL,
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (artifact_replica_id, source)
);
""";

    public const string CreateRecoveryError = """
CREATE TABLE IF NOT EXISTS system_admin.database_recovery_error (
    operation_id uuid NOT NULL,
    error_identity uuid NOT NULL,
    classification smallint NOT NULL,
    safe_diagnostic_reference text NOT NULL DEFAULT '',
    observed_utc timestamptz NOT NULL,
    occurrence_count integer NOT NULL DEFAULT 1,
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (operation_id, error_identity)
);
""";

    public const string CreateBackupPolicy = """
CREATE TABLE IF NOT EXISTS system_admin.database_backup_policy (
    environment_identity text NOT NULL,
    policy_id text NOT NULL,
    policy_revision bigint NOT NULL,
    definition_json jsonb NOT NULL,
    enforced boolean NOT NULL,
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (environment_identity, policy_id)
);
""";

    public const string CreateServiceHealth = """
CREATE TABLE IF NOT EXISTS system_admin.database_backup_service_health (
    environment_identity text NOT NULL,
    source smallint NOT NULL,
    host_id text NOT NULL,
    capability_state smallint NOT NULL,
    ready boolean NOT NULL,
    last_service_sequence bigint NOT NULL,
    observed_utc timestamptz NOT NULL,
    safe_diagnostic_reference text NOT NULL DEFAULT '',
    reconciled boolean NOT NULL DEFAULT false,
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (environment_identity, source, host_id)
);
""";

    public const string CreateRetentionState = """
CREATE TABLE IF NOT EXISTS system_admin.database_retention_state (
    plan_id uuid NOT NULL,
    source smallint NOT NULL,
    plan_revision bigint NOT NULL,
    evaluation_boundary_utc timestamptz NOT NULL,
    retain_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    delete_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    approved boolean NOT NULL,
    outcome smallint NOT NULL,
    source_revision bigint NOT NULL,
    last_event_id bigint NOT NULL,
    last_source_event_id uuid NOT NULL,
    PRIMARY KEY (plan_id, source)
);
""";

    public const string CreateProjectionCheckpoint = """
CREATE TABLE IF NOT EXISTS system_admin.database_backup_projection_checkpoint (
    projector_name text PRIMARY KEY,
    last_event_id bigint NOT NULL,
    applied_count bigint NOT NULL,
    updated_utc timestamptz NOT NULL
);
""";

    public const string CreateProjectionReceipt = """
CREATE TABLE IF NOT EXISTS system_admin.database_backup_projection_receipt (
    projector_name text NOT NULL,
    event_id bigint NOT NULL,
    event_hash text NOT NULL,
    source_event_id uuid NOT NULL,
    applied_utc timestamptz NOT NULL,
    PRIMARY KEY (projector_name, event_id)
);
""";
}
