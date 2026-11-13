using BenchmarkDotNet.Attributes;
using Cassandra;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures both latest-ID reads through the storage context against ScyllaDB.</summary>
[MemoryDiagnoser]
public class LatestTickIdBenchmarks
{
    static readonly DateOnly ValueDate = new(2026, 11, 10);
    TradeTickQuoteDbContext database = null!;
    string contract = null!;

    [Params(128, 16384)]
    public int RowsPerDay { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        const string connection = TradeTickQuoteContextConventionBenchmarks.Connection;
        if (Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_SCYLLA") != connection)
            throw new InvalidOperationException("Only the dedicated disposable Scylla benchmark keyspace is allowed.");

        var settings = new DbConnectionSettings().Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
            connection, "System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradeTickQuoteSchemaDb(settings, logger).CreateAllAsync().ConfigureAwait(false);
        database = new(settings, logger);
        contract = "TICK-ID-BENCH-" + Guid.NewGuid().ToString("N");

        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1")
            .WithPort(TradeTickQuoteContextConventionBenchmarks.Port).Build();
        using var session = cluster.Connect(TradeTickQuoteContextConventionBenchmarks.Keyspace);
        var futures = await session.PrepareAsync(
            "INSERT INTO futures_tick_data (contractid, valuedate, tickid) VALUES (?, ?, ?);").ConfigureAwait(false);
        var options = await session.PrepareAsync(
            "INSERT INTO futures_option_tick_data (contractid, valuedate, tickid) VALUES (?, ?, ?);").ConfigureAwait(false);
        await SeedAsync(session, futures).ConfigureAwait(false);
        await SeedAsync(session, options).ConfigureAwait(false);

        if (await GetLastFuturesTickDataId().ConfigureAwait(false) != new FuturesTickDataId(contract, ValueDate, 2L * RowsPerDay)
            || await GetLastFuturesOptionTickDataId().ConfigureAwait(false) != new FuturesOptionTickDataId(contract, ValueDate, 2L * RowsPerDay)
            || await database.GetLastFuturesTickDataIdAsync(contract, ValueDate.AddDays(-1)).ConfigureAwait(false) is not null
            || await database.GetLastFuturesOptionTickDataIdAsync(contract, ValueDate.AddDays(-1)).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("Latest-ID results did not match the seeded identity or the empty day.");

        Console.WriteLine($"Correctness passed for {RowsPerDay} ticks per day in each table. Both reads returned ID {2L * RowsPerDay} and null for the empty day.");
    }

    async Task SeedAsync(ISession session, PreparedStatement insert)
    {
        var date = new LocalDate(ValueDate.Year, ValueDate.Month, ValueDate.Day);
        // Sparse IDs seeded in reverse order distinguish the maximum key from insertion order and row count.
        var chunks = Enumerable.Range(1, RowsPerDay).Reverse().Chunk(32);
        await Parallel.ForEachAsync(chunks, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (chunk, _) =>
        {
            var batch = new BatchStatement().SetBatchType(BatchType.Unlogged);
            foreach (var row in chunk)
                batch.Add(insert.Bind(contract, date, 2L * row));
            using var result = await session.ExecuteAsync(batch).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var nextDate = ValueDate.AddDays(1);
        var nextDay = new LocalDate(nextDate.Year, nextDate.Month, nextDate.Day);
        using var neighbor = await session.ExecuteAsync(insert.Bind(contract, nextDay, 2L * RowsPerDay + 1)).ConfigureAwait(false);
    }

    [Benchmark]
    public Task<FuturesTickDataId?> GetLastFuturesTickDataId() =>
        database.GetLastFuturesTickDataIdAsync(contract, ValueDate);

    [Benchmark]
    public Task<FuturesOptionTickDataId?> GetLastFuturesOptionTickDataId() =>
        database.GetLastFuturesOptionTickDataIdAsync(contract, ValueDate);
}
