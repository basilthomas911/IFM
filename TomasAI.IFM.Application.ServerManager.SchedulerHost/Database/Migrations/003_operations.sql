ALTER TABLE ifm_scheduler.schedule_definition
    ADD COLUMN IF NOT EXISTS maximum_runtime_seconds INTEGER NULL,
    ADD COLUMN IF NOT EXISTS successful_retention_days INTEGER NOT NULL DEFAULT 30,
    ADD COLUMN IF NOT EXISTS failed_retention_days INTEGER NOT NULL DEFAULT 180,
    ADD COLUMN IF NOT EXISTS deleted_at_utc TIMESTAMPTZ NULL;

ALTER TABLE ifm_scheduler.task_attempt
    ADD COLUMN IF NOT EXISTS stdout_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS stderr_truncated BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS output_retained BOOLEAN NOT NULL DEFAULT TRUE;

CREATE TABLE IF NOT EXISTS ifm_scheduler.request_receipt
(
    request_id UUID PRIMARY KEY,
    operation TEXT NOT NULL,
    actor TEXT NOT NULL,
    response_json JSONB NOT NULL,
    occurred_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_audit_entry_entity
    ON ifm_scheduler.audit_entry(entity_type, entity_id, occurred_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_task_run_terminal_retention
    ON ifm_scheduler.task_run(finished_at_utc)
    WHERE state NOT IN ('Planned', 'Starting', 'Running', 'Cancelling');
