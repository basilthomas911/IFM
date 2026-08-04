using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Compares the former one-round-trip-per-row PostgreSQL path with production bounded NpgsqlBatch writes.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class PostgresBulkWriteBenchmarks
{
    const string ConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    const string CredentialVariable = "POSTGRES_TEST_KEY";
    const string RuntimeEnvironmentVariable = "DOTNET_ENVIRONMENT";
    const string ConnectionName = "PostgresBulkBenchmark";
    const string ProviderName = "System.Data.Postgres";
    const int LegacyPartitionBase = -1_799_990_000;
    const int RedesignedPartitionBase = -1_799_980_000;

    const string CreateTable = """
        CREATE TABLE IF NOT EXISTS framework_storage_postgres_bulk_write_benchmark (
            partition_id integer NOT NULL,
            row_id integer NOT NULL,
            payload text NOT NULL,
            created_on timestamp NOT NULL,
            PRIMARY KEY (partition_id, row_id)
        );
        """;

    const string UpsertRow = """
        INSERT INTO framework_storage_postgres_bulk_write_benchmark
            (partition_id, row_id, payload, created_on)
        VALUES ($1, $2, $3, $4)
        ON CONFLICT (partition_id, row_id) DO UPDATE
        SET payload = EXCLUDED.payload, created_on = EXCLUDED.created_on;
        """;

    NpgsqlDataSource _dataSource = null!;
    BenchmarkRepository _repository = null!;
    BulkWriteBindValue[] _legacyValues = null!;
    BulkWriteBindValue[] _redesignedValues = null!;
    string? _previousDotNetEnvironment;
    bool _runtimeEnvironmentOverridden;

    [Params(100, 1000)]
    public int RowCount { get; set; }

    [Params(1, 32)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {ConnectionVariable} for a dedicated PostgreSQL test database.");

        var credentialJson = Environment.GetEnvironmentVariable(CredentialVariable);
        if (string.IsNullOrWhiteSpace(credentialJson))
            throw new InvalidOperationException($"Set {CredentialVariable} to a JSON object containing userid and password.");

        var credentials = JsonSerializer.Deserialize<CredentialDocument>(credentialJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"{CredentialVariable} contains invalid JSON.");
        if (string.IsNullOrWhiteSpace(credentials.UserId) || string.IsNullOrWhiteSpace(credentials.Password))
            throw new InvalidOperationException($"{CredentialVariable} must contain non-empty userid and password properties.");

        _previousDotNetEnvironment = Environment.GetEnvironmentVariable(RuntimeEnvironmentVariable);
        Environment.SetEnvironmentVariable(RuntimeEnvironmentVariable, "Test");
        _runtimeEnvironmentOverridden = true;
        try
        {
            var resolved = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Username = credentials.UserId,
                Password = credentials.Password
            };
            _dataSource = NpgsqlDataSource.Create(resolved.ConnectionString);
            await using (var command = _dataSource.CreateCommand(CreateTable))
                await command.ExecuteNonQueryAsync();

            var settings = new DbConnectionSettings().Add(ConnectionName, connectionString, ProviderName);
            _repository = new BenchmarkRepository(settings[ConnectionName]);
            _legacyValues = CreateValues(LegacyPartitionBase);
            _redesignedValues = CreateValues(RedesignedPartitionBase);
            await CleanupAsync();
        }
        catch
        {
            try
            {
                if (_dataSource is not null)
                    await _dataSource.DisposeAsync();
            }
            finally
            {
                RestoreRuntimeEnvironment();
            }
            throw;
        }
    }

    [GlobalCleanup(Target = nameof(LegacySequentialCommands))]
    public Task LegacyGlobalCleanup()
        => VerifyAndCleanupAsync(LegacyPartitionBase);

    [GlobalCleanup(Target = nameof(RedesignedBoundedBatch))]
    public Task RedesignedGlobalCleanup()
        => VerifyAndCleanupAsync(RedesignedPartitionBase);

    async Task VerifyAndCleanupAsync(int partitionBase)
    {
        try
        {
            if (_dataSource is null)
                return;

            await using var command = _dataSource.CreateCommand("""
                SELECT COUNT(*)
                FROM framework_storage_postgres_bulk_write_benchmark
                WHERE partition_id >= $1 AND partition_id < $2;
                """);
            command.Parameters.Add(Integer(partitionBase));
            command.Parameters.Add(Integer(partitionBase + PartitionCount));
            var persistedRows = (long)(await command.ExecuteScalarAsync() ?? 0L);
            if (persistedRows != RowCount)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL benchmark persisted {persistedRows} rows; expected {RowCount}.");
            }
        }
        finally
        {
            try
            {
                if (_dataSource is not null)
                {
                    await CleanupAsync();
                    await _dataSource.DisposeAsync();
                }
            }
            finally
            {
                RestoreRuntimeEnvironment();
            }
        }
    }

    void RestoreRuntimeEnvironment()
    {
        if (!_runtimeEnvironmentOverridden)
            return;

        Environment.SetEnvironmentVariable(RuntimeEnvironmentVariable, _previousDotNetEnvironment);
        _runtimeEnvironmentOverridden = false;
    }

    [Benchmark(Baseline = true, Description = "Before: sequential prepared commands")]
    public async Task LegacySequentialCommands()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(UpsertRow, connection, transaction);
        var first = true;
        foreach (var value in _legacyValues)
        {
            command.Parameters.Clear();
            foreach (var parameter in (NpgsqlParameter[])value.Bind())
                command.Parameters.Add(parameter);
            if (first)
            {
                await command.PrepareAsync();
                first = false;
            }
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    [Benchmark(Description = "After: bounded NpgsqlBatch")]
    public Task RedesignedBoundedBatch()
        => _repository.Use(UpsertRow)
            .SetParameters(_redesignedValues)
            .ExecuteCommandAsync();

    BulkWriteBindValue[] CreateValues(int partitionBase)
    {
        var createdOn = new DateTime(2026, 8, 3, 12, 0, 0);
        var values = new BulkWriteBindValue[RowCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = new BulkWriteBindValue(
                partitionBase + index % PartitionCount,
                index,
                $"bulk-benchmark-{index:D6}",
                createdOn);
        }
        return values;
    }

    async Task CleanupAsync()
    {
        await using var command = _dataSource.CreateCommand("""
            DELETE FROM framework_storage_postgres_bulk_write_benchmark
            WHERE partition_id >= $1 AND partition_id < $2;
            """);
        command.Parameters.Add(Integer(LegacyPartitionBase));
        command.Parameters.Add(Integer(RedesignedPartitionBase + PartitionCount));
        await command.ExecuteNonQueryAsync();
    }

    readonly record struct BulkWriteBindValue(
        int PartitionId,
        int RowId,
        string Payload,
        DateTime CreatedOn) : IBindValue
    {
        public object Bind() => Values(
            Integer(PartitionId),
            Integer(RowId),
            Text(Payload),
            Timestamp(CreatedOn));
    }

    sealed record CredentialDocument(string UserId, string Password);

    sealed class BenchmarkRepository(IDbConnectionSetting connectionSetting)
        : ObjectDataRepository<BenchmarkRepository>(connectionSetting, NullLogger<DbProvider>.Instance)
    {
        public override IObjectRepository Database => this;
    }
}
