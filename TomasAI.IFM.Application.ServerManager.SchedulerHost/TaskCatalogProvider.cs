using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class TaskCatalogProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SchedulerHostOptions _options;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReadOnlyDictionary<string, ScheduledTaskCatalogDefinition> _byKey;

    public TaskCatalogProvider(SchedulerHostOptions options, NpgsqlDataSource dataSource)
    {
        _options = options;
        _dataSource = dataSource;
        _byKey = options.TaskCatalog.ToDictionary(task => task.TaskKey, StringComparer.OrdinalIgnoreCase);
    }

    public ScheduledTaskCatalogDefinition GetRequired(string taskKey)
        => _byKey.TryGetValue(taskKey, out var definition)
            ? definition
            : throw new InvalidOperationException($"Task catalog key '{taskKey}' is not approved.");

    public async Task SynchronizeSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var definition in _options.TaskCatalog)
        {
            var executablePath = definition.ResolveExecutablePath(_options);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ifm_scheduler.task_catalog_snapshot
                (task_key, display_name, description, executable_path, working_directory, definition_json,
                 required_environment, risk_classification, manifest_version, executable_available,
                 maximum_runtime_seconds, updated_at_utc)
                VALUES ($1, $2, $3, $4, $5, $6::jsonb, $7, $8, $9, $10, $11, now())
                ON CONFLICT (task_key) DO UPDATE SET
                    display_name = excluded.display_name,
                    description = excluded.description,
                    executable_path = excluded.executable_path,
                    working_directory = excluded.working_directory,
                    definition_json = excluded.definition_json,
                    required_environment = excluded.required_environment,
                    risk_classification = excluded.risk_classification,
                    manifest_version = excluded.manifest_version,
                    executable_available = excluded.executable_available,
                    maximum_runtime_seconds = excluded.maximum_runtime_seconds,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue(definition.TaskKey);
            command.Parameters.AddWithValue(definition.DisplayName);
            command.Parameters.AddWithValue(definition.Description);
            command.Parameters.AddWithValue(executablePath);
            command.Parameters.AddWithValue(definition.ResolveWorkingDirectory(_options));
            command.Parameters.AddWithValue(JsonSerializer.Serialize(definition, JsonOptions));
            command.Parameters.AddWithValue(definition.RequiredEnvironment);
            command.Parameters.AddWithValue(definition.RiskClassification.ToString());
            command.Parameters.AddWithValue(definition.ManifestVersion);
            command.Parameters.AddWithValue(definition.IsExecutableAvailable(_options));
            command.Parameters.AddWithValue(definition.MaximumRuntimeSeconds);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
