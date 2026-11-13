using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class WatchdogObservationFilterBenchmarks
{
    const string CurrentSql = """
        SELECT watchdog_status_log_id,observation_id,correlation_id,value_date,observed_on_utc,operation_reason,
            major_status,display_health,core_contracts_ready,recovery_attempt,native_backend,native_abi_version,
            native_generation,failure_stage,failure_detail,feed_status_details,row_version
        FROM market_data_service.watchdog_status_log
        WHERE ($1::date IS NULL OR value_date=$1) AND ($2::text IS NULL OR major_status=$2)
        ORDER BY observed_on_utc DESC LIMIT $3;
        """;
    const int Days = 48;
    const int RowsPerDay = 10_000;
    const int Limit = 100;
    static readonly DateOnly FirstDay = new(2026, 8, 1);
    static readonly DateOnly HistoricalDay = FirstDay.AddDays(6);

    NpgsqlConnection db = null!;
    NpgsqlCommand current = null!;
    NpgsqlCommand split = null!;

    [Params("Broad", "Date", "Status", "Both")]
    public string Scope { get; set; } = null!;

    static (DateOnly? Date, DatabentoMajorStatus? Status) Filters(string scope) => scope switch
    {
        "Broad" => (null, null),
        "Date" => (HistoricalDay, null),
        "Status" => (null, DatabentoMajorStatus.Down),
        "Both" => (HistoricalDay, DatabentoMajorStatus.Down),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    static NpgsqlCommand Command(NpgsqlConnection db, DateOnly? date, DatabentoMajorStatus? status, int limit, bool useSplit)
    {
        var selection = new ObservationListParameter(date, status, limit);
        var (sql, values) = useSplit ? (selection.Statement.Sql, (object?[])selection.Bind())
            : (CurrentSql, Values(TomasAI.IFM.Shared.Storage.BindValues.Date(date), Text(status?.ToString()), Integer(limit)));
        var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, values);
        return command;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        var (date, status) = Filters(Scope);
        current = Command(db, date, status, Limit, useSplit: false);
        split = Command(db, date, status, Limit, useSplit: true);
        await current.PrepareAsync().ConfigureAwait(false);
        await split.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<List<DatabentoWatchdogObservation>> Current() => Read(current);
    [Benchmark] public Task<List<DatabentoWatchdogObservation>> ExplicitFilters() => Read(split);

    internal static async Task<List<DatabentoWatchdogObservation>> Read(NpgsqlCommand command)
    {
        var rows = new List<DatabentoWatchdogObservation>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(reader);
        while (await reader.ReadAsync().ConfigureAwait(false))
            rows.Add(MarketDataServiceDbContext.MapToObservation(record));
        return rows;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (current is not null) await current.DisposeAsync().ConfigureAwait(false);
        if (split is not null) await split.DisposeAsync().ConfigureAwait(false);
        if (db is not null) await db.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task PrepareFixture()
    {
        await using var db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        await SeedFixture(db).ConfigureAwait(false);

        foreach (var scope in new[] { "Broad", "Date", "Status", "Both" })
        {
            var (date, status) = Filters(scope);
            await Verify(db, scope, date, status, Limit).ConfigureAwait(false);
            await PrintPlans(db, scope, date, status).ConfigureAwait(false);
        }
        await Verify(db, "latest date + common status", FirstDay.AddDays(Days - 1), DatabentoMajorStatus.Up, 100).ConfigureAwait(false);
        await Verify(db, "middle date + Resetting", HistoricalDay, DatabentoMajorStatus.Resetting, 7).ConfigureAwait(false);
        await Verify(db, "missing date", FirstDay.AddDays(-1), null, 100).ConfigureAwait(false);
        await Verify(db, "missing date + status", FirstDay.AddDays(-1), DatabentoMajorStatus.Down, 100).ConfigureAwait(false);
        await Verify(db, "one row", null, DatabentoMajorStatus.Down, 1).ConfigureAwait(false);
        await Verify(db, "zero limit", null, null, 0).ConfigureAwait(false);
    }

    internal static async Task SeedFixture(NpgsqlConnection db)
    {
        await Execute(db, MarketDataServiceSchemaSql.CreateSchema + "\n" + MarketDataServiceSchemaSql.CreateWatchdogLog)
            .ConfigureAwait(false);
        await using var seed = new NpgsqlCommand($"""
            INSERT INTO market_data_service.watchdog_status_log
            SELECT n+1,md5('observation:'||n)::uuid,md5('correlation:'||n/100)::uuid,
                $1::date + n/{RowsPerDay},
                ($1::date + n/{RowsPerDay})::timestamp AT TIME ZONE 'UTC' + (n%{RowsPerDay})*interval '1 second',
                'WatchdogPoll',CASE WHEN n%1000=0 THEN 'Down' WHEN n%100=0 THEN 'Resetting' ELSE 'Up' END,
                CASE WHEN n%1000=0 THEN 'Red' WHEN n%100=0 THEN 'Yellow' ELSE 'Green' END,
                n%100<>0, n%4, 'fixture', 1, md5('generation')::uuid, '', repeat('detail', 12), '[]', 1,
                now(),'fixture',now(),'fixture'
            FROM generate_series(0,{Days * RowsPerDay - 1}) n
            """, db);
        PostgresValueBinding.AddParameters(seed, [FirstDay]);
        if (await seed.ExecuteNonQueryAsync().ConfigureAwait(false) != Days * RowsPerDay)
            throw new InvalidOperationException("Unexpected watchdog fixture size.");
        await Execute(db, "VACUUM (ANALYZE) market_data_service.watchdog_status_log").ConfigureAwait(false);

    }

    static async Task Verify(NpgsqlConnection db, string label, DateOnly? date, DatabentoMajorStatus? status, int limit)
    {
        await using var before = Command(db, date, status, limit, useSplit: false);
        await using var after = Command(db, date, status, limit, useSplit: true);
        await before.PrepareAsync().ConfigureAwait(false);
        await after.PrepareAsync().ConfigureAwait(false);
        var baseline = await Read(before).ConfigureAwait(false);
        var candidate = await Read(after).ConfigureAwait(false);
        var context = new MarketDataServiceDbContext(Settings("force_generic_plan"), NullLogger<DbProvider>.Instance);
        var production = await context.ListObservationsAsync(date, status, limit).ConfigureAwait(false);
        var expected = ExpectedIds(date, status, limit);
        if (!candidate.Select(row => row.WatchdogStatusLogId).SequenceEqual(expected)
            || !baseline.Select(Normalize).SequenceEqual(candidate.Select(Normalize))
            || !production.Select(Normalize).SequenceEqual(candidate.Select(Normalize))
            || baseline.Concat(candidate).Concat(production).Any(row => row.FeedStatusDetails.Count != 0))
            throw new InvalidOperationException($"Ordered observations differed for {label}.");
        Console.WriteLine($"ORDERED,{label},{candidate.Count},{string.Join(',', candidate.Select(row => row.WatchdogStatusLogId))}");
    }

    internal static IEnumerable<long> ExpectedIds(DateOnly? date, DatabentoMajorStatus? status, int limit) =>
        Enumerable.Range(0, Days * RowsPerDay).Reverse()
            .Where(n => (date is null || FirstDay.AddDays(n / RowsPerDay) == date) && (status is null ||
                status == (n % 1000 == 0 ? DatabentoMajorStatus.Down : n % 100 == 0 ? DatabentoMajorStatus.Resetting : DatabentoMajorStatus.Up)))
            .Take(limit).Select(n => n + 1L);

    internal static DatabentoWatchdogObservation Normalize(DatabentoWatchdogObservation row) => row with { FeedStatusDetails = [] };

    static async Task PrintPlans(NpgsqlConnection db, string scope, DateOnly? date, DatabentoMajorStatus? status)
    {
        var sql = new ObservationListParameter(date, status, Limit).Statement.Sql;
        var dateArg = date is { } day ? FormattableString.Invariant($"'{day:yyyy-MM-dd}'") : "NULL";
        var statusArg = status is { } major ? $"'{major}'" : "NULL";
        var splitArgs = scope switch
        {
            "Broad" => $"{Limit}",
            "Date" => $"{dateArg},{Limit}",
            "Status" => $"{Limit}",
            _ => $"{dateArg},{statusArg},{Limit}"
        };
        var splitTypes = scope switch
        {
            "Broad" => "integer",
            "Date" => "date,integer",
            "Status" => "integer",
            _ => "date,text,integer"
        };
        await Execute(db, $"PREPARE observations_current(date,text,integer) AS {CurrentSql} PREPARE observations_split({splitTypes}) AS {sql};")
            .ConfigureAwait(false);
        await Explain(db, scope, "current", $"observations_current({dateArg},{statusArg},{Limit})").ConfigureAwait(false);
        await Explain(db, scope, "explicit", $"observations_split({splitArgs})").ConfigureAwait(false);
        await Execute(db, "DEALLOCATE observations_current; DEALLOCATE observations_split;").ConfigureAwait(false);
    }

    static async Task Explain(NpgsqlConnection db, string scope, string variant, string execution)
    {
        Console.WriteLine($"PLAN,force_generic_plan,{scope},{variant}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) EXECUTE " + execution, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if (variant == "explicit" &&
            (plan.Contains("Sort Key:", StringComparison.Ordinal)
            || ((scope is "Date" or "Both") && !plan.Contains("Index Cond: (value_date = $1)", StringComparison.Ordinal))
            || (scope == "Both" && !plan.Contains("Filter: (major_status = $2)", StringComparison.Ordinal))))
            throw new InvalidOperationException($"Unexpected generic plan for {scope}.");
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
