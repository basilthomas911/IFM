using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;
using static TomasAI.IFM.Application.Storage.Benchmarks.WatchdogObservationFilterBenchmarks;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class WatchdogStatusBenchmarks
{
    const string ParameterizedSql = MarketDataServiceDbSql.ListObservationsByStatus;
    const int Limit = 100;
    NpgsqlConnection db = null!;
    NpgsqlCommand parameterized = null!;
    NpgsqlCommand specific = null!;

    [ParamsAllValues] public DatabentoMajorStatus Status { get; set; }

    static NpgsqlCommand Command(NpgsqlConnection connection, DatabentoMajorStatus status, int limit, bool fixedStatus)
    {
        var selection = new ObservationListParameter(null, status, limit);
        var command = new NpgsqlCommand(fixedStatus ? selection.Statement.Sql : ParameterizedSql, connection);
        PostgresValueBinding.AddParameters(command, fixedStatus ? (object?[])selection.Bind() : [status.ToString(), limit]);
        return command;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        parameterized = Command(db, Status, Limit, false);
        specific = Command(db, Status, Limit, true);
        await parameterized.PrepareAsync().ConfigureAwait(false);
        await specific.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<List<DatabentoWatchdogObservation>> Parameterized() => Read(parameterized);
    [Benchmark] public Task<List<DatabentoWatchdogObservation>> SpecificStatus() => Read(specific);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await parameterized.DisposeAsync().ConfigureAwait(false);
        await specific.DisposeAsync().ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task PrepareAsync()
    {
        await using var db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        await SeedFixture(db).ConfigureAwait(false);
        var context = new MarketDataServiceDbContext(Settings("force_generic_plan"), NullLogger<DbProvider>.Instance);
        foreach (var status in Enum.GetValues<DatabentoMajorStatus>())
        {
            foreach (var limit in new[] { 0, 1, Limit, 500 })
            {
                await using var before = Command(db, status, limit, false);
                await using var after = Command(db, status, limit, true);
                await before.PrepareAsync().ConfigureAwait(false);
                await after.PrepareAsync().ConfigureAwait(false);
                var baseline = await Read(before).ConfigureAwait(false);
                var candidate = await Read(after).ConfigureAwait(false);
                var production = await context.ListObservationsAsync(status: status, pageSize: limit).ConfigureAwait(false);
                if (!baseline.Select(Normalize).SequenceEqual(candidate.Select(Normalize))
                    || !production.Select(Normalize).SequenceEqual(candidate.Select(Normalize))
                    || baseline.Concat(candidate).Concat(production).Any(row => row.FeedStatusDetails.Count != 0)
                    || !candidate.Select(row => row.WatchdogStatusLogId).SequenceEqual(ExpectedIds(null, status, limit)))
                    throw new InvalidOperationException($"Ordered status results differed for {status}, limit {limit}.");
                Console.WriteLine($"ORDERED,{status},{limit},{candidate.Count},{candidate.FirstOrDefault()?.WatchdogStatusLogId},{candidate.LastOrDefault()?.WatchdogStatusLogId}");
            }

            await Execute(db, $"""
                PREPARE watchdog_status_parameterized(text,integer) AS {ParameterizedSql};
                PREPARE watchdog_status_specific(integer) AS {new ObservationListParameter(null, status, Limit).Statement.Sql};
                """).ConfigureAwait(false);
            await Explain(db, status, "parameterized", $"watchdog_status_parameterized('{status}',{Limit})").ConfigureAwait(false);
            await Explain(db, status, "specific", $"watchdog_status_specific({Limit})").ConfigureAwait(false);
            await Execute(db, "DEALLOCATE watchdog_status_parameterized; DEALLOCATE watchdog_status_specific;").ConfigureAwait(false);
        }
        if ((await context.ListObservationsAsync(status: (DatabentoMajorStatus)999).ConfigureAwait(false)).Count != 0)
            throw new InvalidOperationException("Undefined status returned observations.");
        Console.WriteLine("Verified undefined status still returns an empty result.");
    }

    static async Task Explain(NpgsqlConnection connection, DatabentoMajorStatus status, string variant, string execution)
    {
        Console.WriteLine($"PLAN,force_generic_plan,{status},{variant}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) EXECUTE " + execution, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if (variant == "specific" &&
            (plan.Contains("Sort Key:", StringComparison.Ordinal) ||
             (status == DatabentoMajorStatus.Up
                 ? !plan.Contains("Index Scan using ix_watchdog_status_log_observed ", StringComparison.Ordinal)
                 : !plan.Contains("Index Scan using ix_watchdog_status_log_major ", StringComparison.Ordinal)
                   || !plan.Contains($"Index Cond: (major_status = '{status}'::text)", StringComparison.Ordinal))))
            throw new InvalidOperationException($"Unexpected generic plan for {status}.");
    }

    static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
