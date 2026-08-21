using Npgsql;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class ScheduleSeedProvider(
    SchedulerHostOptions options,
    TaskCatalogProvider catalog,
    ScheduleValidationService validator,
    NpgsqlDataSource dataSource)
{
    public async Task SeedDisabledDefinitionsAsync(CancellationToken cancellationToken)
    {
        if (!options.SeedInitialSchedules)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var seed in options.InitialSchedules)
        {
            if (seed.Enabled)
            {
                throw new InvalidOperationException($"Seed schedule '{seed.Name}' attempted to start enabled.");
            }

            var input = seed.ToInput();
            var validation = validator.Validate(input);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Seed schedule '{seed.Name}' is invalid: {string.Join(" ", validation.Errors)}");
            }

            var task = catalog.GetRequired(seed.TaskKey);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ifm_scheduler.schedule_definition
                (schedule_definition_id, name, description, task_key, catalog_manifest_version, enabled,
                 schedule_kind, schedule_expression, schedule_explanation, time_zone_id, misfire_policy,
                 maximum_runtime_seconds, successful_retention_days, failed_retention_days,
                 created_by, created_at_utc, updated_by, updated_at_utc)
                VALUES ($1, $2, $3, $4, $5, false, $6, $7, $8, $9, $10, $11, $12, $13,
                        'deployment-seed', now(), 'deployment-seed', now())
                ON CONFLICT (schedule_definition_id) DO NOTHING;
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
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
