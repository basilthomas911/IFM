namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;

internal static class ScheduledTaskSchemaSql
{
    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS scheduled_task;
        CREATE TABLE IF NOT EXISTS scheduled_task.futures_completed_end_of_day (
            environment text PRIMARY KEY, completed_value_date date NOT NULL, run_id uuid NOT NULL);
        CREATE TABLE IF NOT EXISTS scheduled_task.scheduled_task_definition (
            environment text, host_id text, schedule_id uuid, revision bigint NOT NULL, payload bytea NOT NULL,
            PRIMARY KEY (environment, host_id, schedule_id));
        CREATE TABLE IF NOT EXISTS scheduled_task.scheduled_task_catalog (
            environment text, host_id text, revision bigint NOT NULL, payload bytea NOT NULL,
            PRIMARY KEY (environment, host_id));
        CREATE TABLE IF NOT EXISTS scheduled_task.scheduled_task_run (
            environment text, host_id text, schedule_id uuid, intended_fire timestamptz, run_id uuid,
            revision bigint NOT NULL, payload bytea NOT NULL,
            PRIMARY KEY (environment, host_id, schedule_id, intended_fire, run_id));
        CREATE INDEX IF NOT EXISTS ix_scheduled_task_run_history ON scheduled_task.scheduled_task_run
            (environment, host_id, schedule_id, intended_fire DESC, run_id ASC);
        """;
}
