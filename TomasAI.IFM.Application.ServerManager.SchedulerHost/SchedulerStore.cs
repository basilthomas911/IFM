using Npgsql;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed partial class SchedulerStore(NpgsqlDataSource dataSource, SchedulerHostOptions options)
{
    public async Task<IReadOnlyList<TaskCatalogItemDto>> GetTaskCatalogAsync(CancellationToken cancellationToken)
    {
        var result = new List<TaskCatalogItemDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_key, display_name, description, executable_path, required_environment,
                   risk_classification, manifest_version, executable_available, maximum_runtime_seconds
            FROM ifm_scheduler.task_catalog_snapshot
            ORDER BY display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TaskCatalogItemDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                Enum.Parse<SchedulerRiskClassification>(reader.GetString(5)),
                reader.GetString(6),
                reader.GetBoolean(7),
                reader.GetInt32(8)));
        }

        return result;
    }

    public async Task<IReadOnlyList<ScheduleSummaryDto>> GetSchedulesAsync(CancellationToken cancellationToken)
    {
        var result = new List<ScheduleSummaryDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT schedule_definition_id, name, task_key, enabled, schedule_kind, schedule_expression,
                   schedule_explanation, time_zone_id, misfire_policy, previous_fire_utc, next_fire_utc,
                   version, updated_by, updated_at_utc, description, maximum_runtime_seconds,
                   successful_retention_days, failed_retention_days
            FROM ifm_scheduler.schedule_definition
            WHERE deleted_at_utc IS NULL
            ORDER BY name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ScheduleSummaryDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                Enum.Parse<ScheduleKind>(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                Enum.Parse<SchedulerMisfirePolicy>(reader.GetString(8)),
                ReadTimestamp(reader, 9),
                ReadTimestamp(reader, 10),
                reader.GetInt64(11),
                reader.GetString(12),
                ReadRequiredTimestamp(reader, 13),
                reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetInt32(15),
                reader.GetInt32(16),
                reader.GetInt32(17)));
        }

        return result;
    }

    public async Task<IReadOnlyList<TaskRunSummaryDto>> GetRecentRunsAsync(CancellationToken cancellationToken)
    {
        var result = new List<TaskRunSummaryDto>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, occurrence_id, attempt_id, schedule_definition_id, task_key, state, origin,
                   scheduled_fire_utc, started_at_utc, finished_at_utc, process_id, exit_code, detail
            FROM ifm_scheduler.task_run
            ORDER BY created_at_utc DESC
            LIMIT $1;
            """;
        command.Parameters.AddWithValue(options.RecentRunLimit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TaskRunSummaryDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4),
                Enum.Parse<ScheduledRunState>(reader.GetString(5)),
                Enum.Parse<ScheduledRunOrigin>(reader.GetString(6)),
                ReadRequiredTimestamp(reader, 7),
                ReadTimestamp(reader, 8),
                ReadTimestamp(reader, 9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetString(12)));
        }

        return result;
    }

    public async Task<bool> TryCreateRunAsync(NewScheduledRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ifm_scheduler.task_run
            (run_id, occurrence_id, attempt_id, schedule_definition_id, task_key, state, origin,
             quartz_fire_instance_id, scheduled_fire_utc, stdout_path, stderr_path, created_at_utc)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, now());
            """;
        command.Parameters.AddWithValue(run.RunId);
        command.Parameters.AddWithValue(run.OccurrenceId);
        command.Parameters.AddWithValue(run.AttemptId);
        command.Parameters.AddWithValue((object?)run.ScheduleDefinitionId ?? DBNull.Value);
        command.Parameters.AddWithValue(run.TaskKey);
        command.Parameters.AddWithValue(ScheduledRunState.Planned.ToString());
        command.Parameters.AddWithValue(run.Origin.ToString());
        command.Parameters.AddWithValue((object?)run.QuartzFireInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue(run.ScheduledFireUtc);
        command.Parameters.AddWithValue(run.StdoutPath);
        command.Parameters.AddWithValue(run.StderrPath);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            await using var attempt = connection.CreateCommand();
            attempt.Transaction = transaction;
            attempt.CommandText = """
                INSERT INTO ifm_scheduler.task_attempt
                (attempt_id, run_id, attempt_number, state, stdout_path, stderr_path)
                VALUES ($1, $2, 1, $3, $4, $5);
                """;
            attempt.Parameters.AddWithValue(run.AttemptId);
            attempt.Parameters.AddWithValue(run.RunId);
            attempt.Parameters.AddWithValue(ScheduledRunState.Planned.ToString());
            attempt.Parameters.AddWithValue(run.StdoutPath);
            attempt.Parameters.AddWithValue(run.StderrPath);
            await attempt.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task TransitionRunAsync(
        Guid runId,
        ScheduledRunState next,
        string? detail,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        int? exitCode,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT state FROM ifm_scheduler.task_run WHERE run_id = $1 FOR UPDATE;";
        read.Parameters.AddWithValue(runId);
        var currentText = (string?)(await read.ExecuteScalarAsync(cancellationToken))
            ?? throw new InvalidOperationException($"Scheduled run '{runId}' does not exist.");
        var current = Enum.Parse<ScheduledRunState>(currentText);
        ScheduledRunStateMachine.EnsureTransition(current, next);

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE ifm_scheduler.task_run
            SET state = $2,
                started_at_utc = CASE WHEN $2 = 'Running' THEN COALESCE(started_at_utc, now()) ELSE started_at_utc END,
                finished_at_utc = CASE WHEN $3 THEN now() ELSE finished_at_utc END,
                process_id = COALESCE($4, process_id),
                process_started_at_utc = COALESCE($5, process_started_at_utc),
                exit_code = COALESCE($6, exit_code),
                detail = COALESCE($7, detail)
            WHERE run_id = $1;
            """;
        update.Parameters.AddWithValue(runId);
        update.Parameters.AddWithValue(next.ToString());
        update.Parameters.AddWithValue(ScheduledRunStateMachine.IsTerminal(next));
        update.Parameters.AddWithValue((object?)processId ?? DBNull.Value);
        update.Parameters.AddWithValue((object?)processStartedAtUtc ?? DBNull.Value);
        update.Parameters.AddWithValue((object?)exitCode ?? DBNull.Value);
        update.Parameters.AddWithValue((object?)detail ?? DBNull.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);

        await using var updateAttempt = connection.CreateCommand();
        updateAttempt.Transaction = transaction;
        updateAttempt.CommandText = """
            UPDATE ifm_scheduler.task_attempt
            SET state = $2,
                started_at_utc = CASE WHEN $2 = 'Running' THEN COALESCE(started_at_utc, now()) ELSE started_at_utc END,
                finished_at_utc = CASE WHEN $3 THEN now() ELSE finished_at_utc END,
                process_id = COALESCE($4, process_id),
                process_started_at_utc = COALESCE($5, process_started_at_utc),
                exit_code = COALESCE($6, exit_code),
                detail = COALESCE($7, detail)
            WHERE run_id = $1;
            """;
        updateAttempt.Parameters.AddWithValue(runId);
        updateAttempt.Parameters.AddWithValue(next.ToString());
        updateAttempt.Parameters.AddWithValue(ScheduledRunStateMachine.IsTerminal(next));
        updateAttempt.Parameters.AddWithValue((object?)processId ?? DBNull.Value);
        updateAttempt.Parameters.AddWithValue((object?)processStartedAtUtc ?? DBNull.Value);
        updateAttempt.Parameters.AddWithValue((object?)exitCode ?? DBNull.Value);
        updateAttempt.Parameters.AddWithValue((object?)detail ?? DBNull.Value);
        await updateAttempt.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordTerminalRunAsync(
        NewScheduledRun run,
        ScheduledRunState state,
        string detail,
        CancellationToken cancellationToken)
    {
        if (!ScheduledRunStateMachine.IsTerminal(state))
        {
            throw new InvalidOperationException($"Run state '{state}' is not terminal.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ifm_scheduler.task_run
            (run_id, occurrence_id, attempt_id, schedule_definition_id, task_key, state, origin,
             quartz_fire_instance_id, scheduled_fire_utc, finished_at_utc, detail, stdout_path,
             stderr_path, created_at_utc)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, now(), $10, $11, $12, now());
            """;
        command.Parameters.AddWithValue(run.RunId);
        command.Parameters.AddWithValue(run.OccurrenceId);
        command.Parameters.AddWithValue(run.AttemptId);
        command.Parameters.AddWithValue((object?)run.ScheduleDefinitionId ?? DBNull.Value);
        command.Parameters.AddWithValue(run.TaskKey);
        command.Parameters.AddWithValue(state.ToString());
        command.Parameters.AddWithValue(run.Origin.ToString());
        command.Parameters.AddWithValue((object?)run.QuartzFireInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue(run.ScheduledFireUtc);
        command.Parameters.AddWithValue(detail);
        command.Parameters.AddWithValue(run.StdoutPath);
        command.Parameters.AddWithValue(run.StderrPath);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var attempt = connection.CreateCommand();
        attempt.Transaction = transaction;
        attempt.CommandText = """
            INSERT INTO ifm_scheduler.task_attempt
            (attempt_id, run_id, attempt_number, state, finished_at_utc, detail, stdout_path, stderr_path)
            VALUES ($1, $2, 1, $3, now(), $4, $5, $6);
            """;
        attempt.Parameters.AddWithValue(run.AttemptId);
        attempt.Parameters.AddWithValue(run.RunId);
        attempt.Parameters.AddWithValue(state.ToString());
        attempt.Parameters.AddWithValue(detail);
        attempt.Parameters.AddWithValue(run.StdoutPath);
        attempt.Parameters.AddWithValue(run.StderrPath);
        await attempt.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> RecoverIncompleteRunsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH abandoned AS
            (
                UPDATE ifm_scheduler.task_run
                SET state = 'Abandoned',
                    finished_at_utc = now(),
                    detail = 'Scheduler Host restarted before a durable terminal result was recorded.'
                WHERE state IN ('Planned', 'Starting', 'Running', 'Cancelling')
                RETURNING run_id
            ), updated_attempts AS
            (
                UPDATE ifm_scheduler.task_attempt
                SET state = 'Abandoned',
                    finished_at_utc = now(),
                    detail = 'Scheduler Host restarted before a durable terminal result was recorded.'
                WHERE run_id IN (SELECT run_id FROM abandoned)
                RETURNING attempt_id
            )
            SELECT count(*) FROM abandoned;
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateScheduleFireTimesAsync(
        Guid scheduleDefinitionId,
        DateTimeOffset? previousFireUtc,
        DateTimeOffset? nextFireUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ifm_scheduler.schedule_definition
            SET previous_fire_utc = $2, next_fire_utc = $3
            WHERE schedule_definition_id = $1;
            """;
        command.Parameters.AddWithValue(scheduleDefinitionId);
        command.Parameters.AddWithValue((object?)previousFireUtc ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)nextFireUtc ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset? ReadTimestamp(NpgsqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : ReadRequiredTimestamp(reader, ordinal);

    private static DateTimeOffset ReadRequiredTimestamp(NpgsqlDataReader reader, int ordinal)
        => new(reader.GetDateTime(ordinal).ToUniversalTime());
}

public sealed record NewScheduledRun(
    Guid RunId,
    Guid OccurrenceId,
    Guid AttemptId,
    Guid? ScheduleDefinitionId,
    string TaskKey,
    ScheduledRunOrigin Origin,
    string? QuartzFireInstanceId,
    DateTimeOffset ScheduledFireUtc,
    string StdoutPath,
    string StderrPath);
