using System.Reflection;
using Npgsql;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerDatabaseMigrator(NpgsqlDataSource dataSource, ILogger<SchedulerDatabaseMigrator> logger)
{
    private const long MigrationLockId = 0x49464D5343484544;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockCommand = connection.CreateCommand();
        lockCommand.CommandText = "SELECT pg_advisory_lock($1);";
        lockCommand.Parameters.AddWithValue(MigrationLockId);
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            await EnsureMigrationInfrastructureAsync(connection, cancellationToken);
            foreach (var migration in LoadMigrations())
            {
                if (await IsAppliedAsync(connection, migration.Version, cancellationToken))
                {
                    continue;
                }

                logger.LogInformation("Applying scheduler database migration {Version}.", migration.Version);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                await command.ExecuteNonQueryAsync(cancellationToken);

                await using var record = connection.CreateCommand();
                record.Transaction = transaction;
                record.CommandText = """
                    INSERT INTO ifm_scheduler.schema_migration(version, applied_at_utc)
                    VALUES ($1, now());
                    """;
                record.Parameters.AddWithValue(migration.Version);
                await record.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await using var unlockCommand = connection.CreateCommand();
            unlockCommand.CommandText = "SELECT pg_advisory_unlock($1);";
            unlockCommand.Parameters.AddWithValue(MigrationLockId);
            await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task EnsureMigrationInfrastructureAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE SCHEMA IF NOT EXISTS ifm_quartz;
            CREATE SCHEMA IF NOT EXISTS ifm_scheduler;
            CREATE TABLE IF NOT EXISTS ifm_scheduler.schema_migration
            (
                version TEXT PRIMARY KEY,
                applied_at_utc TIMESTAMPTZ NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM ifm_scheduler.schema_migration WHERE version = $1);";
        command.Parameters.AddWithValue(version);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Migration version query returned no value."));
    }

    private static IReadOnlyList<Migration> LoadMigrations()
    {
        var assembly = typeof(SchedulerDatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains("Database.Migrations", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Migration resource '{name}' is unavailable.");
                using var reader = new StreamReader(stream);
                var fileName = name[(name.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..];
                var version = fileName[..fileName.IndexOf('_')];
                return new Migration(version, reader.ReadToEnd());
            })
            .ToArray();
    }

    private sealed record Migration(string Version, string Sql);
}
