using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed partial class SchedulerStore
{
    private static readonly JsonSerializerOptions OperationJson = new(JsonSerializerDefaults.Web);

    public Task<SchedulerOperationResultDto> CreateScheduleAsync(
        Guid requestId,
        string actor,
        ScheduleDefinitionInputDto input,
        string explanation,
        string manifestVersion,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            SchedulerProtocol.CreateScheduleOperation,
            actor,
            async (connection, transaction) =>
            {
                var id = input.ScheduleDefinitionId ?? Guid.NewGuid();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO ifm_scheduler.schedule_definition
                    (schedule_definition_id, name, description, task_key, catalog_manifest_version, enabled,
                     schedule_kind, schedule_expression, schedule_explanation, time_zone_id, misfire_policy,
                     maximum_runtime_seconds, successful_retention_days, failed_retention_days,
                     created_by, created_at_utc, updated_by, updated_at_utc)
                    VALUES ($1, $2, $3, $4, $5, false, $6, $7, $8, $9, $10, $11, $12, $13,
                            $14, now(), $14, now());
                    """;
                AddScheduleParameters(command, id, input, explanation, manifestVersion, actor);
                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    throw new SchedulerConflictException($"A schedule named '{input.Name}' already exists.");
                }

                await WriteAuditAsync(connection, transaction, "Schedule", id.ToString("D"), "Created", actor, input, cancellationToken);
                return new SchedulerOperationResultDto(
                    SchedulerProtocol.CreateScheduleOperation,
                    "Schedule created disabled. Validate it and explicitly enable it when approved.",
                    id,
                    1);
            },
            cancellationToken);

    public Task<SchedulerOperationResultDto> UpdateScheduleAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        ScheduleDefinitionInputDto input,
        string explanation,
        string manifestVersion,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            SchedulerProtocol.UpdateScheduleOperation,
            actor,
            async (connection, transaction) =>
            {
                var id = input.ScheduleDefinitionId
                    ?? throw new SchedulerValidationException("Schedule ID is required for update.");
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE ifm_scheduler.schedule_definition
                    SET name = $2, description = $3, task_key = $4, catalog_manifest_version = $5,
                        schedule_kind = $6, schedule_expression = $7, schedule_explanation = $8,
                        time_zone_id = $9, misfire_policy = $10, maximum_runtime_seconds = $11,
                        successful_retention_days = $12, failed_retention_days = $13,
                        version = version + 1, updated_by = $14, updated_at_utc = now()
                    WHERE schedule_definition_id = $1 AND version = $15 AND deleted_at_utc IS NULL;
                    """;
                AddScheduleParameters(command, id, input, explanation, manifestVersion, actor);
                command.Parameters.AddWithValue(expectedVersion);
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new SchedulerConflictException("The schedule changed or was deleted. Refresh before saving.");
                }

                await WriteAuditAsync(connection, transaction, "Schedule", id.ToString("D"), "Updated", actor, input, cancellationToken);
                return new SchedulerOperationResultDto(
                    SchedulerProtocol.UpdateScheduleOperation,
                    "Schedule updated.",
                    id,
                    expectedVersion + 1);
            },
            cancellationToken);

    public Task<SchedulerOperationResultDto> SetScheduleEnabledAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        SetScheduleEnabledDto input,
        string reason,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            SchedulerProtocol.SetScheduleEnabledOperation,
            actor,
            async (connection, transaction) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE ifm_scheduler.schedule_definition
                    SET enabled = $2, version = version + 1, updated_by = $3, updated_at_utc = now()
                    WHERE schedule_definition_id = $1 AND version = $4 AND deleted_at_utc IS NULL;
                    """;
                command.Parameters.AddWithValue(input.ScheduleDefinitionId);
                command.Parameters.AddWithValue(input.Enabled);
                command.Parameters.AddWithValue(actor);
                command.Parameters.AddWithValue(expectedVersion);
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new SchedulerConflictException("The schedule changed or was deleted. Refresh before changing its state.");
                }

                await WriteAuditAsync(
                    connection,
                    transaction,
                    "Schedule",
                    input.ScheduleDefinitionId.ToString("D"),
                    input.Enabled ? "Enabled" : "Disabled",
                    actor,
                    new { input.Enabled, Reason = reason },
                    cancellationToken);
                return new SchedulerOperationResultDto(
                    SchedulerProtocol.SetScheduleEnabledOperation,
                    input.Enabled ? "Schedule enabled." : "Schedule disabled.",
                    input.ScheduleDefinitionId,
                    expectedVersion + 1);
            },
            cancellationToken);

    public Task<SchedulerOperationResultDto> DeleteScheduleAsync(
        Guid requestId,
        string actor,
        long expectedVersion,
        Guid scheduleId,
        string reason,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            SchedulerProtocol.DeleteScheduleOperation,
            actor,
            async (connection, transaction) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE ifm_scheduler.schedule_definition
                    SET deleted_at_utc = now(), enabled = false, version = version + 1,
                        updated_by = $2, updated_at_utc = now()
                    WHERE schedule_definition_id = $1 AND version = $3 AND enabled = false
                      AND deleted_at_utc IS NULL;
                    """;
                command.Parameters.AddWithValue(scheduleId);
                command.Parameters.AddWithValue(actor);
                command.Parameters.AddWithValue(expectedVersion);
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new SchedulerConflictException("Only an unchanged, disabled schedule can be deleted.");
                }

                await WriteAuditAsync(connection, transaction, "Schedule", scheduleId.ToString("D"), "Deleted", actor, new { Reason = reason }, cancellationToken);
                return new SchedulerOperationResultDto(
                    SchedulerProtocol.DeleteScheduleOperation,
                    "Schedule deleted. Existing run history was retained.",
                    scheduleId,
                    expectedVersion + 1);
            },
            cancellationToken);

    public async Task<ScheduleSummaryDto> GetScheduleAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = (await GetSchedulesAsync(cancellationToken))
            .SingleOrDefault(value => value.ScheduleDefinitionId == scheduleId);
        return schedule ?? throw new SchedulerValidationException($"Schedule '{scheduleId}' does not exist.");
    }

    public async Task<TaskRunSummaryDto> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, occurrence_id, attempt_id, schedule_definition_id, task_key, state, origin,
                   scheduled_fire_utc, started_at_utc, finished_at_utc, process_id, exit_code, detail
            FROM ifm_scheduler.task_run
            WHERE run_id = $1;
            """;
        command.Parameters.AddWithValue(runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new SchedulerValidationException($"Run '{runId}' does not exist in retained history.");
        }

        return new TaskRunSummaryDto(
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
            reader.IsDBNull(12) ? null : reader.GetString(12));
    }

    public async Task<RunOutputLocation> GetRunOutputLocationAsync(
        Guid runId,
        TaskOutputStream stream,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN $2 = 'StandardOutput' THEN stdout_path ELSE stderr_path END,
                   CASE WHEN $2 = 'StandardOutput' THEN stdout_truncated ELSE stderr_truncated END,
                   output_retained
            FROM ifm_scheduler.task_attempt
            WHERE run_id = $1
            ORDER BY attempt_number DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(runId);
        command.Parameters.AddWithValue(stream.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new SchedulerValidationException($"Run '{runId}' does not have an output attempt.");
        }

        return new RunOutputLocation(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2));
    }

    public async Task RecordOutputDispositionAsync(
        Guid runId,
        bool stdoutTruncated,
        bool stderrTruncated,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ifm_scheduler.task_attempt
            SET stdout_truncated = $2, stderr_truncated = $3
            WHERE run_id = $1;
            """;
        command.Parameters.AddWithValue(runId);
        command.Parameters.AddWithValue(stdoutTruncated);
        command.Parameters.AddWithValue(stderrTruncated);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetentionCandidate>> GetRetentionCandidatesAsync(CancellationToken cancellationToken)
    {
        var result = new List<RetentionCandidate>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.run_id, a.stdout_path, a.stderr_path
            FROM ifm_scheduler.task_run r
            JOIN ifm_scheduler.task_attempt a ON a.run_id = r.run_id
            LEFT JOIN ifm_scheduler.schedule_definition s ON s.schedule_definition_id = r.schedule_definition_id
            WHERE a.output_retained
              AND r.finished_at_utc IS NOT NULL
              AND r.state NOT IN ('Planned', 'Starting', 'Running', 'Cancelling', 'Abandoned')
              AND r.finished_at_utc < now() - make_interval(days => CASE
                    WHEN r.state = 'Succeeded' THEN COALESCE(s.successful_retention_days, $1)
                    ELSE COALESCE(s.failed_retention_days, $2)
                  END);
            """;
        command.Parameters.AddWithValue(options.SuccessfulRunRetentionDays);
        command.Parameters.AddWithValue(options.FailedRunRetentionDays);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RetentionCandidate(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return result;
    }

    public async Task MarkOutputDeletedAsync(Guid runId, string actor, string reason, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE ifm_scheduler.task_attempt SET output_retained = false WHERE run_id = $1;";
        update.Parameters.AddWithValue(runId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await WriteAuditAsync(connection, transaction, "Run", runId.ToString("D"), "OutputDeleted", actor, new { Reason = reason }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<SchedulerOperationResultDto> QueueRunRequestAsync(
        Guid requestId,
        string operation,
        string actor,
        string taskKey,
        Guid? scheduleId,
        Guid runId,
        Guid occurrenceId,
        Guid attemptId,
        ScheduledRunOrigin origin,
        int? maximumRuntimeSeconds,
        string reason,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            operation,
            actor,
            async (connection, transaction) =>
            {
                var payload = new QueuedRunRequest(
                    taskKey,
                    scheduleId,
                    runId,
                    occurrenceId,
                    attemptId,
                    origin,
                    maximumRuntimeSeconds);
                await using var outbox = connection.CreateCommand();
                outbox.Transaction = transaction;
                outbox.CommandText = """
                    INSERT INTO ifm_scheduler.outbox(outbox_id, event_type, payload, occurred_at_utc)
                    VALUES ($1, 'RunRequested', $2::jsonb, now());
                    """;
                outbox.Parameters.AddWithValue(Guid.NewGuid());
                outbox.Parameters.AddWithValue(JsonSerializer.Serialize(payload, OperationJson));
                await outbox.ExecuteNonQueryAsync(cancellationToken);
                await WriteAuditAsync(connection, transaction, "Run", runId.ToString("D"), origin.ToString(), actor, new { Reason = reason, ScheduleId = scheduleId }, cancellationToken);
                return new SchedulerOperationResultDto(
                    operation,
                    origin == ScheduledRunOrigin.Retry ? "Retry queued." : "Manual run queued.",
                    scheduleId,
                    null,
                    runId,
                    occurrenceId);
            },
            cancellationToken);

    public Task<SchedulerOperationResultDto> RecordControlOperationAsync(
        Guid requestId,
        string operation,
        string actor,
        Guid entityId,
        string action,
        string reason,
        string message,
        CancellationToken cancellationToken)
        => ExecuteIdempotentAsync(
            requestId,
            operation,
            actor,
            async (connection, transaction) =>
            {
                await WriteAuditAsync(connection, transaction, "Run", entityId.ToString("D"), action, actor, new { Reason = reason }, cancellationToken);
                return new SchedulerOperationResultDto(operation, message, entityId, null, entityId);
            },
            cancellationToken);

    public async Task<IReadOnlyList<OutboxRunRequest>> GetPendingRunRequestsAsync(CancellationToken cancellationToken)
    {
        var result = new List<OutboxRunRequest>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT outbox_id, payload::text
            FROM ifm_scheduler.outbox
            WHERE event_type = 'RunRequested' AND published_at_utc IS NULL
            ORDER BY occurred_at_utc
            LIMIT 20;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = JsonSerializer.Deserialize<QueuedRunRequest>(reader.GetString(1), OperationJson)
                ?? throw new InvalidOperationException("Run-request outbox payload is invalid.");
            result.Add(new OutboxRunRequest(reader.GetGuid(0), payload));
        }

        return result;
    }

    public async Task MarkOutboxPublishedAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ifm_scheduler.outbox SET published_at_utc = now() WHERE outbox_id = $1 AND published_at_utc IS NULL;";
        command.Parameters.AddWithValue(outboxId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SchedulerOperationResultDto> ExecuteIdempotentAsync(
        Guid requestId,
        string operation,
        string actor,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<SchedulerOperationResultDto>> mutation,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var advisory = connection.CreateCommand())
        {
            advisory.Transaction = transaction;
            advisory.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended($1, 0));";
            advisory.Parameters.AddWithValue(requestId.ToString("D"));
            await advisory.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var prior = connection.CreateCommand())
        {
            prior.Transaction = transaction;
            prior.CommandText = "SELECT operation, response_json::text FROM ifm_scheduler.request_receipt WHERE request_id = $1;";
            prior.Parameters.AddWithValue(requestId);
            await using var reader = await prior.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var priorOperation = reader.GetString(0);
                if (!string.Equals(priorOperation, operation, StringComparison.Ordinal))
                {
                    throw new SchedulerConflictException("Request ID was already used for a different operation.");
                }

                var priorResult = JsonSerializer.Deserialize<SchedulerOperationResultDto>(reader.GetString(1), OperationJson)
                    ?? throw new InvalidOperationException("Stored request receipt is invalid.");
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return priorResult with { Replayed = true };
            }
        }

        var result = await mutation(connection, transaction);
        await using var receipt = connection.CreateCommand();
        receipt.Transaction = transaction;
        receipt.CommandText = """
            INSERT INTO ifm_scheduler.request_receipt(request_id, operation, actor, response_json, occurred_at_utc)
            VALUES ($1, $2, $3, $4::jsonb, now());
            """;
        receipt.Parameters.AddWithValue(requestId);
        receipt.Parameters.AddWithValue(operation);
        receipt.Parameters.AddWithValue(actor);
        receipt.Parameters.AddWithValue(JsonSerializer.Serialize(result, OperationJson));
        await receipt.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void AddScheduleParameters(
        NpgsqlCommand command,
        Guid id,
        ScheduleDefinitionInputDto input,
        string explanation,
        string manifestVersion,
        string actor)
    {
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(input.Name.Trim());
        command.Parameters.AddWithValue(input.Description.Trim());
        command.Parameters.AddWithValue(input.TaskKey);
        command.Parameters.AddWithValue(manifestVersion);
        command.Parameters.AddWithValue(input.Kind.ToString());
        command.Parameters.AddWithValue(input.ScheduleExpression.Trim());
        command.Parameters.AddWithValue(explanation);
        command.Parameters.AddWithValue(input.TimeZoneId);
        command.Parameters.AddWithValue(input.MisfirePolicy.ToString());
        command.Parameters.AddWithValue((object?)input.MaximumRuntimeSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue(input.SuccessfulRetentionDays);
        command.Parameters.AddWithValue(input.FailedRetentionDays);
        command.Parameters.AddWithValue(actor);
    }

    private static async Task WriteAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string entityType,
        string entityId,
        string action,
        string actor,
        object detail,
        CancellationToken cancellationToken)
    {
        await using var audit = connection.CreateCommand();
        audit.Transaction = transaction;
        audit.CommandText = """
            INSERT INTO ifm_scheduler.audit_entry
            (audit_id, entity_type, entity_id, action, actor, detail, occurred_at_utc)
            VALUES ($1, $2, $3, $4, $5, $6::jsonb, now());
            """;
        audit.Parameters.AddWithValue(Guid.NewGuid());
        audit.Parameters.AddWithValue(entityType);
        audit.Parameters.AddWithValue(entityId);
        audit.Parameters.AddWithValue(action);
        audit.Parameters.AddWithValue(actor);
        audit.Parameters.AddWithValue(JsonSerializer.Serialize(detail, OperationJson));
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record RunOutputLocation(string Path, bool Truncated, bool Retained);

public sealed record RetentionCandidate(Guid RunId, string StdoutPath, string StderrPath);

public sealed record QueuedRunRequest(
    string TaskKey,
    Guid? ScheduleDefinitionId,
    Guid RunId,
    Guid OccurrenceId,
    Guid AttemptId,
    ScheduledRunOrigin Origin,
    int? MaximumRuntimeSeconds);

public sealed record OutboxRunRequest(Guid OutboxId, QueuedRunRequest Request);

public sealed class SchedulerConflictException(string message) : Exception(message);

public sealed class SchedulerValidationException(string message) : Exception(message);
