CREATE TABLE IF NOT EXISTS ifm_scheduler.task_catalog_snapshot
(
    task_key TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    description TEXT NOT NULL,
    executable_path TEXT NOT NULL,
    working_directory TEXT NOT NULL,
    definition_json JSONB NOT NULL,
    required_environment TEXT NOT NULL,
    risk_classification TEXT NOT NULL,
    manifest_version TEXT NOT NULL,
    executable_available BOOLEAN NOT NULL,
    maximum_runtime_seconds INTEGER NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS ifm_scheduler.schedule_definition
(
    schedule_definition_id UUID PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    task_key TEXT NOT NULL REFERENCES ifm_scheduler.task_catalog_snapshot(task_key),
    catalog_manifest_version TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    schedule_kind TEXT NOT NULL,
    schedule_expression TEXT NOT NULL,
    schedule_explanation TEXT NOT NULL,
    time_zone_id TEXT NOT NULL,
    misfire_policy TEXT NOT NULL,
    previous_fire_utc TIMESTAMPTZ NULL,
    next_fire_utc TIMESTAMPTZ NULL,
    version BIGINT NOT NULL DEFAULT 1,
    created_by TEXT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_by TEXT NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS ifm_scheduler.task_run
(
    run_id UUID PRIMARY KEY,
    occurrence_id UUID NOT NULL,
    attempt_id UUID NOT NULL UNIQUE,
    schedule_definition_id UUID NULL REFERENCES ifm_scheduler.schedule_definition(schedule_definition_id),
    task_key TEXT NOT NULL REFERENCES ifm_scheduler.task_catalog_snapshot(task_key),
    state TEXT NOT NULL,
    origin TEXT NOT NULL,
    quartz_fire_instance_id TEXT NULL,
    scheduled_fire_utc TIMESTAMPTZ NOT NULL,
    started_at_utc TIMESTAMPTZ NULL,
    finished_at_utc TIMESTAMPTZ NULL,
    process_id INTEGER NULL,
    process_started_at_utc TIMESTAMPTZ NULL,
    exit_code INTEGER NULL,
    detail TEXT NULL,
    stdout_path TEXT NULL,
    stderr_path TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_task_run_active_schedule
    ON ifm_scheduler.task_run(schedule_definition_id)
    WHERE schedule_definition_id IS NOT NULL
      AND state IN ('Planned', 'Starting', 'Running', 'Cancelling');

CREATE INDEX IF NOT EXISTS ix_task_run_recent
    ON ifm_scheduler.task_run(created_at_utc DESC);

CREATE TABLE IF NOT EXISTS ifm_scheduler.task_attempt
(
    attempt_id UUID PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES ifm_scheduler.task_run(run_id) ON DELETE CASCADE,
    attempt_number INTEGER NOT NULL,
    state TEXT NOT NULL,
    started_at_utc TIMESTAMPTZ NULL,
    finished_at_utc TIMESTAMPTZ NULL,
    process_id INTEGER NULL,
    process_started_at_utc TIMESTAMPTZ NULL,
    exit_code INTEGER NULL,
    detail TEXT NULL,
    stdout_path TEXT NOT NULL,
    stderr_path TEXT NOT NULL,
    UNIQUE (run_id, attempt_number)
);

CREATE TABLE IF NOT EXISTS ifm_scheduler.audit_entry
(
    audit_id UUID PRIMARY KEY,
    entity_type TEXT NOT NULL,
    entity_id TEXT NOT NULL,
    action TEXT NOT NULL,
    actor TEXT NOT NULL,
    detail JSONB NOT NULL,
    occurred_at_utc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS ifm_scheduler.outbox
(
    outbox_id UUID PRIMARY KEY,
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    occurred_at_utc TIMESTAMPTZ NOT NULL,
    published_at_utc TIMESTAMPTZ NULL
);
