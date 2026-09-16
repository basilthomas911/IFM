using Npgsql;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class ScheduleSeedProvider(
    SchedulerHostOptions options,
    TaskCatalogProvider catalog,
    ScheduleValidationService validator,
    NpgsqlDataSource dataSource)
{
    public async Task SeedDefinitionsAsync(CancellationToken cancellationToken)
    {
        if (!options.SeedInitialSchedules)
            return;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var seed in options.InitialSchedules)
        {
            if (seed.Enabled && string.IsNullOrWhiteSpace(seed.ActivationApprovalReference))
            {
                throw new InvalidOperationException(
                    $"Seed schedule '{seed.Name}' attempted to start enabled without an activation approval reference.");
            }

            var input = seed.ToInput();
            var validation = validator.Validate(input);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Seed schedule '{seed.Name}' is invalid: {string.Join(" ", validation.Errors)}");
            }

            var task = catalog.GetRequired(seed.TaskKey);
            if (seed.Enabled && !task.IsExecutableAvailable(options))
            {
                throw new InvalidOperationException(
                    $"Approved seed schedule '{seed.Name}' cannot be enabled because its deployed executable is unavailable or hash-invalid.");
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ifm_scheduler.schedule_definition AS existing
                (schedule_definition_id, name, description, task_key, catalog_manifest_version, enabled,
                 schedule_kind, schedule_expression, schedule_explanation, time_zone_id, misfire_policy,
                 maximum_runtime_seconds, successful_retention_days, failed_retention_days,
                 created_by, created_at_utc, updated_by, updated_at_utc)
                VALUES ($1, $2, $3, $4, $5, $14, $6, $7, $8, $9, $10, $11, $12, $13,
                        'deployment-seed', now(), 'deployment-seed', now())
                ON CONFLICT (schedule_definition_id) DO UPDATE
                SET name = EXCLUDED.name,
                    description = EXCLUDED.description,
                    task_key = EXCLUDED.task_key,
                    catalog_manifest_version = EXCLUDED.catalog_manifest_version,
                    enabled = EXCLUDED.enabled,
                    schedule_kind = EXCLUDED.schedule_kind,
                    schedule_expression = EXCLUDED.schedule_expression,
                    schedule_explanation = EXCLUDED.schedule_explanation,
                    time_zone_id = EXCLUDED.time_zone_id,
                    misfire_policy = EXCLUDED.misfire_policy,
                    maximum_runtime_seconds = EXCLUDED.maximum_runtime_seconds,
                    successful_retention_days = EXCLUDED.successful_retention_days,
                    failed_retention_days = EXCLUDED.failed_retention_days,
                    version = existing.version + 1,
                    updated_by = 'deployment-seed',
                    updated_at_utc = now()
                WHERE existing.updated_by = 'deployment-seed'
                  AND ROW(
                      existing.name,
                      existing.description,
                      existing.task_key,
                      existing.catalog_manifest_version,
                      existing.enabled,
                      existing.schedule_kind,
                      existing.schedule_expression,
                      existing.schedule_explanation,
                      existing.time_zone_id,
                      existing.misfire_policy,
                      existing.maximum_runtime_seconds,
                      existing.successful_retention_days,
                      existing.failed_retention_days)
                    IS DISTINCT FROM ROW(
                      EXCLUDED.name,
                      EXCLUDED.description,
                      EXCLUDED.task_key,
                      EXCLUDED.catalog_manifest_version,
                      EXCLUDED.enabled,
                      EXCLUDED.schedule_kind,
                      EXCLUDED.schedule_expression,
                      EXCLUDED.schedule_explanation,
                      EXCLUDED.time_zone_id,
                      EXCLUDED.misfire_policy,
                      EXCLUDED.maximum_runtime_seconds,
                      EXCLUDED.successful_retention_days,
                      EXCLUDED.failed_retention_days);
                """;
            command.Parameters.AddWithValue(seed.ScheduleDefinitionId);
            command.Parameters.AddWithValue(seed.Name);
            command.Parameters.AddWithValue(seed.Description);
            command.Parameters.AddWithValue(seed.TaskKey);
            command.Parameters.AddWithValue(task.ManifestVersion);
            command.Parameters.AddWithValue(seed.Kind.ToString());
            command.Parameters.AddWithValue(seed.ScheduleExpression);
            command.Parameters.AddWithValue(validation.Explanation);
            command.Parameters.AddWithValue(seed.TimeZoneId);
            command.Parameters.AddWithValue(seed.MisfirePolicy.ToString());
            command.Parameters.AddWithValue((object?)seed.MaximumRuntimeSeconds ?? DBNull.Value);
            command.Parameters.AddWithValue(seed.SuccessfulRetentionDays);
            command.Parameters.AddWithValue(seed.FailedRetentionDays);
            command.Parameters.AddWithValue(seed.Enabled);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
