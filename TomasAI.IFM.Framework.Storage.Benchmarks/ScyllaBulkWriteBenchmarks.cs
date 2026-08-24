using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cassandra;
using Cassandra.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Compares the former 2,000-row logged-batch implementation with the production bounded-concurrency path.
/// Both cases write the same row shape into disjoint reserved test partitions on the same Scylla cluster.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class ScyllaBulkWriteBenchmarks
{
    const string ConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string CredentialVariable = "SCYLLADB_TEST_KEY";
    const string ConnectionName = "ScyllaBulkBenchmark";
    const string ProviderName = "System.Data.ScyllaDb";
    const int LegacyPartitionBase = -1_999_990_000;
    const int RedesignedPartitionBase = -1_999_980_000;

    const string CreateTable = """
        CREATE TABLE IF NOT EXISTS framework_storage_bulk_write_benchmark (
            partitionId int,
            rowId int,
            payload text,
            createdOn timestamp,
            PRIMARY KEY (partitionId, rowId)
        ) WITH CLUSTERING ORDER BY (rowId ASC);
        """;

    const string InsertRow = """
        INSERT INTO framework_storage_bulk_write_benchmark (partitionId, rowId, payload, createdOn)
        VALUES (:partitionId, :rowId, :payload, :createdOn);
        """;

    const string DeletePartition = """
        DELETE FROM framework_storage_bulk_write_benchmark WHERE partitionId = :partitionId;
        """;

    const string CountPartition = """
        SELECT COUNT(*)
        FROM framework_storage_bulk_write_benchmark
        WHERE partitionId = :partitionId;
        """;

    Cluster _legacyCluster = null!;
    ISession _legacySession = null!;
    PreparedStatement _legacyInsert = null!;
    PreparedStatement _deletePartition = null!;
    PreparedStatement _countPartition = null!;
    BenchmarkRepository _repository = null!;
    BulkWriteBindValue[] _legacyValues = null!;
    BulkWriteBindValue[] _redesignedValues = null!;

    [Params(100, 1000)]
    public int RowCount { get; set; }

    [Params(1, 32)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {ConnectionVariable} to a credential-free connection string for a dedicated Scylla test keyspace.");

        var credentialJson = Environment.GetEnvironmentVariable(CredentialVariable);
        if (string.IsNullOrWhiteSpace(credentialJson))
            throw new InvalidOperationException($"Set {CredentialVariable} to a JSON object containing userid and password.");

        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var credentials = JsonSerializer.Deserialize<CredentialDocument>(credentialJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"{CredentialVariable} contains invalid JSON.");
        if (string.IsNullOrWhiteSpace(credentials.UserId) || string.IsNullOrWhiteSpace(credentials.Password))
            throw new InvalidOperationException($"{CredentialVariable} must contain non-empty userid and password properties.");

        var connection = new CassandraConnectionStringBuilder(connectionString);
        _legacyCluster = Cluster.Builder()
            .AddContactPoints(connection.ContactPoints)
            .WithPort(connection.Port)
            .WithCredentials(credentials.UserId, credentials.Password)
            .WithQueryTimeout(30_000)
            .WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(30_000))
            .WithQueryOptions(new QueryOptions()
                .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                .SetSerialConsistencyLevel(ConsistencyLevel.LocalSerial))
            .WithPoolingOptions(new PoolingOptions()
                .SetMaxConnectionsPerHost(HostDistance.Local, 32)
                .SetCoreConnectionsPerHost(HostDistance.Local, 2)
                .SetMaxSimultaneousRequestsPerConnectionTreshold(HostDistance.Local, 2048))
            .Build();
        _legacySession = await _legacyCluster.ConnectAsync(connection.DefaultKeyspace);
        using (await _legacySession.ExecuteAsync(new SimpleStatement(CreateTable)))
        {
        }
        _legacyInsert = await _legacySession.PrepareAsync(InsertRow);
        _deletePartition = await _legacySession.PrepareAsync(DeletePartition);
        _countPartition = await _legacySession.PrepareAsync(CountPartition);

        var settings = new DbConnectionSettings().Add(ConnectionName, connectionString, ProviderName);
        _repository = new BenchmarkRepository(settings[ConnectionName]);
        _legacyValues = CreateValues(LegacyPartitionBase);
        _redesignedValues = CreateValues(RedesignedPartitionBase);
        await CleanupAsync();
    }

    [GlobalCleanup(Target = nameof(LegacyLoggedBatch))]
    public Task LegacyGlobalCleanup()
        => VerifyAndCleanupAsync(LegacyPartitionBase);

    [GlobalCleanup(Target = nameof(RedesignedBoundedConcurrency))]
    public Task RedesignedGlobalCleanup()
        => VerifyAndCleanupAsync(RedesignedPartitionBase);

    async Task VerifyAndCleanupAsync(int partitionBase)
    {
        try
        {
            if (_legacySession is null)
                return;

            long persistedRows = 0;
            for (var partition = 0; partition < PartitionCount; partition++)
            {
                using var rows = await _legacySession.ExecuteAsync(
                    _countPartition.Bind(partitionBase + partition));
                persistedRows += rows.First().GetValue<long>(0);
            }
            if (persistedRows != RowCount)
            {
                throw new InvalidOperationException(
                    $"Scylla benchmark persisted {persistedRows} rows; expected {RowCount}.");
            }
        }
        finally
        {
            if (_legacySession is not null)
                await CleanupAsync();
            _legacySession?.Dispose();
            _legacyCluster?.Dispose();
        }
    }

    [Benchmark(Baseline = true, Description = "Before: logged batch")]
    public async Task LegacyLoggedBatch()
    {
        var batch = new BatchStatement();
        batch.SetBatchType(BatchType.Logged);
        batch.SetSerialConsistencyLevel(ConsistencyLevel.Serial);
        foreach (var value in _legacyValues)
            batch.Add(_legacyInsert.Bind(value.BindArray()));
        using var rowSet = await _legacySession.ExecuteAsync(batch);
    }

    [Benchmark(Description = "After: bounded concurrency")]
    public async Task RedesignedBoundedConcurrency()
        => await _repository
            .Use($"{nameof(ScyllaBulkWriteBenchmarks)}.{nameof(InsertRow)}", InsertRow)
            .SetParameters(_redesignedValues)
            .ExecuteCommandAsync();

    BulkWriteBindValue[] CreateValues(int partitionBase)
    {
        var createdOn = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
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
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            using var legacyRows = await _legacySession.ExecuteAsync(_deletePartition.Bind(LegacyPartitionBase + partition));
            using var redesignedRows = await _legacySession.ExecuteAsync(_deletePartition.Bind(RedesignedPartitionBase + partition));
        }
    }

    readonly record struct BulkWriteBindValue(int PartitionId, int RowId, string Payload, DateTime CreatedOn) : IBindValue
    {
        public object Bind() => BindArray();

        public object?[] BindArray() => [PartitionId, RowId, Payload, CreatedOn];
    }

    sealed record CredentialDocument(string UserId, string Password);

    sealed class BenchmarkRepository(IDbConnectionSetting connectionSetting)
        : ObjectDataRepository<BenchmarkRepository>(connectionSetting, NullLogger<DbProvider>.Instance)
    {
        public override IObjectRepository Database => this;
    }
}
