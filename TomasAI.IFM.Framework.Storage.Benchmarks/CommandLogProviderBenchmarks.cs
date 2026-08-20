using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.CommandLogBenchmark;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Database-only comparison of the authoritative PostgreSQL JSON command log and the isolated ScyllaDB
/// MessagePack candidate. Serialization occurs once in setup so results measure the atomic guard operation.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[WarmupCount(1)]
[IterationCount(5)]
[InvocationCount(1)]
public class CommandLogProviderBenchmarks
{
    const string ScyllaConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string PostgresConnectionVariable = "IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION";
    readonly ConcurrentBag<Guid> _postgresRows = [];
    readonly ConcurrentBag<Guid> _scyllaRows = [];
    PostgresCommandLogBenchmarkStore _postgres = null!;
    ScyllaCommandLogBenchmarkStore _scylla = null!;
    CommandLogBenchmarkEntry _template = null!;
    CommandLogBenchmarkEntry _postgresDuplicate = null!;
    CommandLogBenchmarkEntry _scyllaDuplicate = null!;

    [Params(1, 16, 32)]
    public int ConcurrentRequests { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        var settings = new DbConnectionSettings()
            .Add(
                ScyllaCommandLogBenchmarkStore.ConnectionName,
                Required(ScyllaConnectionVariable),
                "System.Data.ScyllaDb")
            .Add(
                EventSourceActorDbContext.EventSourceActorDbConnection,
                Required(PostgresConnectionVariable),
                "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        _postgres = new PostgresCommandLogBenchmarkStore(settings, logger);
        _scylla = new ScyllaCommandLogBenchmarkStore(settings, logger);
        await _postgres.CreateSchemaAsync();
        await _scylla.CreateSchemaAsync();

        _template = CommandLogBenchmarkEntry.Create(
            Guid.Empty,
            "command-log-benchmark",
            "MarketData",
            nameof(BenchmarkCommand),
            DateTime.UtcNow,
            new BenchmarkCommand("ESU6", 42, "command-log duplicate benchmark"));
        _postgresDuplicate = _template with { CommandId = Guid.NewGuid() };
        _scyllaDuplicate = _template with { CommandId = Guid.NewGuid() };
        await _postgres.TryInsertAsync(_postgresDuplicate);
        await _scylla.TryInsertAsync(_scyllaDuplicate);
    }

    [Benchmark(Baseline = true, Description = "PostgreSQL first insert")]
    [BenchmarkCategory("FirstInsert")]
    public Task<int> PostgresFirstInsert()
        => InsertUniqueAsync(_postgres, _postgresRows);

    [Benchmark(Description = "ScyllaDB first insert")]
    [BenchmarkCategory("FirstInsert")]
    public Task<int> ScyllaFirstInsert()
        => InsertUniqueAsync(_scylla, _scyllaRows);

    [Benchmark(Baseline = true, Description = "PostgreSQL duplicate shortcut")]
    [BenchmarkCategory("Duplicate")]
    public Task<int> PostgresDuplicateShortcut()
        => InsertDuplicateAsync(_postgres, _postgresDuplicate);

    [Benchmark(Description = "ScyllaDB duplicate shortcut")]
    [BenchmarkCategory("Duplicate")]
    public Task<int> ScyllaDuplicateShortcut()
        => InsertDuplicateAsync(_scylla, _scyllaDuplicate);

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DeleteAllAsync(_postgres, _postgresRows.Append(_postgresDuplicate.CommandId));
        await DeleteAllAsync(_scylla, _scyllaRows.Append(_scyllaDuplicate.CommandId));
    }

    async Task<int> InsertUniqueAsync(
        ICommandLogBenchmarkStore store,
        ConcurrentBag<Guid> insertedRows)
    {
        var entries = Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => _template with { CommandId = Guid.NewGuid() })
            .ToArray();
        foreach (var entry in entries)
            insertedRows.Add(entry.CommandId);
        var results = await Task.WhenAll(entries.Select(entry => store.TryInsertAsync(entry)));
        return results.Count(applied => applied);
    }

    async Task<int> InsertDuplicateAsync(
        ICommandLogBenchmarkStore store,
        CommandLogBenchmarkEntry duplicate)
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRequests)
                .Select(_ => store.TryInsertAsync(duplicate)));
        return results.Count(applied => applied);
    }

    static async Task DeleteAllAsync(ICommandLogBenchmarkStore store, IEnumerable<Guid> commandIds)
    {
        foreach (var batch in commandIds.Distinct().Chunk(32))
            await Task.WhenAll(batch.Select(commandId => store.DeleteAsync(commandId)));
    }

    static string Required(string variable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {variable} to a dedicated benchmark database connection.");

    public sealed record BenchmarkCommand(string ContractId, int Sequence, string Description);
}
