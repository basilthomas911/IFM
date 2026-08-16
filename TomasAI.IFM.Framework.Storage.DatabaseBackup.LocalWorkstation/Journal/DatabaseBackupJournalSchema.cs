namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;

internal static class DatabaseBackupJournalSchema
{
    public const string Sql = """
CREATE TABLE IF NOT EXISTS journal_operation (
    operation_id TEXT PRIMARY KEY,
    source INTEGER NOT NULL,
    operation_kind INTEGER NOT NULL,
    protection_set_id TEXT NOT NULL,
    definition_hash TEXT NOT NULL,
    intent_event_id TEXT NOT NULL,
    intent_type TEXT NOT NULL,
    intent_json TEXT NOT NULL,
    phase INTEGER NOT NULL,
    terminal INTEGER NOT NULL DEFAULT 0,
    lease_host_id TEXT NULL,
    lease_expires_utc TEXT NULL,
    fencing_token INTEGER NOT NULL DEFAULT 0,
    last_service_sequence INTEGER NOT NULL DEFAULT 0,
    admitted_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS journal_inbox (
    event_id TEXT PRIMARY KEY,
    operation_id TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    admitted_utc TEXT NOT NULL,
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE TABLE IF NOT EXISTS journal_checkpoint (
    operation_id TEXT NOT NULL,
    fencing_token INTEGER NOT NULL,
    phase INTEGER NOT NULL,
    terminal INTEGER NOT NULL,
    safe_diagnostic_reference TEXT NOT NULL,
    observed_utc TEXT NOT NULL,
    PRIMARY KEY (operation_id, fencing_token, phase),
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE TABLE IF NOT EXISTS journal_artifact_replica (
    operation_id TEXT NOT NULL,
    artifact_replica_id TEXT NOT NULL,
    state INTEGER NOT NULL,
    safe_destination_reference TEXT NOT NULL,
    fencing_token INTEGER NOT NULL,
    updated_utc TEXT NOT NULL,
    PRIMARY KEY (operation_id, artifact_replica_id),
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE TABLE IF NOT EXISTS journal_outbox (
    event_id TEXT PRIMARY KEY,
    operation_id TEXT NOT NULL,
    service_sequence INTEGER NOT NULL,
    event_type TEXT NOT NULL,
    event_json TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    published INTEGER NOT NULL DEFAULT 0,
    publish_attempts INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL,
    published_utc TEXT NULL,
    UNIQUE (operation_id, service_sequence),
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE TABLE IF NOT EXISTS journal_run_stats (
    operation_id TEXT NOT NULL,
    statistics_revision INTEGER NOT NULL,
    statistics_json TEXT NOT NULL,
    published INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (operation_id, statistics_revision),
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE TABLE IF NOT EXISTS journal_reconciliation (
    operation_id TEXT PRIMARY KEY,
    core_domain_revision INTEGER NOT NULL,
    acknowledged_utc TEXT NOT NULL,
    FOREIGN KEY (operation_id) REFERENCES journal_operation(operation_id)
);
CREATE INDEX IF NOT EXISTS ix_journal_operation_recoverable
    ON journal_operation (terminal, lease_expires_utc, admitted_utc);
CREATE INDEX IF NOT EXISTS ix_journal_outbox_pending
    ON journal_outbox (published, operation_id, service_sequence);
""";
}
