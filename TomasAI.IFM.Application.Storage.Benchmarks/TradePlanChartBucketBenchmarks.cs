using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class TradePlanChartBucketBenchmarks
{
    const string Table = "trade_plan.iron_condor_trade_plan";
    const string ForwardVersion = "IronCondor/EntryCreditForwardPnl/v2";
    const int RowsPerDate = 120_000;
    const long Anchor = RowsPerDate - 81;
    const string Anchored = "AnchoredMinute";
    const string Range = "RangeQuarterHour";
    static readonly DateOnly Day = new(2026, 10, 13);
    static readonly DateTime Start = Day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    static readonly Guid Position = new("39256026-6450-49ae-a8ac-8e9ab7bcf48b");
    static readonly string AnchoredCurrent =
        """
        SELECT DISTINCT ON (date_bin(INTERVAL '1 minute',calculatedatutc,timestamptz '2000-01-01')) calculatedatutc,
            combinedspreadprice,CASE WHEN calculationversion = 'IronCondor/EntryCreditForwardPnl/v2' THEN -combinedforwardprice ELSE combinedforwardprice END
        FROM {0}
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6
            AND ($7::bigint IS NULL OR planrevision <= $7) AND calculatedatutc >= $8 AND calculatedatutc <= $9
            AND combinedspreadprice IS NOT NULL
        ORDER BY date_bin(INTERVAL '1 minute',calculatedatutc,timestamptz '2000-01-01'),calculatedatutc DESC,planrevision DESC;
        """.Replace("{0}", Table, StringComparison.Ordinal);

    const string RangeCurrent =
        """
        SELECT DISTINCT ON (date_bin(INTERVAL '15 minutes',calculatedatutc,timestamptz '2000-01-01'))
            calculatedatutc,combinedspreadprice,
            CASE WHEN calculationversion = 'IronCondor/EntryCreditForwardPnl/v2' THEN -combinedforwardprice ELSE combinedforwardprice END
        FROM trade_plan.iron_condor_trade_plan
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5
            AND valuedate >= $6 AND valuedate <= $7 AND calculatedatutc >= $8 AND calculatedatutc <= $9
            AND combinedspreadprice IS NOT NULL
        ORDER BY date_bin(INTERVAL '15 minutes',calculatedatutc,timestamptz '2000-01-01'),calculatedatutc DESC,planrevision DESC;
        """;

    [Params(Anchored, Range)] public string Chart { get; set; } = null!;

    static string Sql(bool anchored, bool seeks) => (anchored, seeks) switch
    {
        (true, true) => TradePlanDbSql.OffsetHistoryChartTemplate.Replace("{0}", Table, StringComparison.Ordinal),
        (false, true) => TradePlanDbSql.TradePlanDbContextExtensions_ReadChartRangeAsync_1,
        (true, false) => AnchoredCurrent,
        _ => RangeCurrent
    };

    static async Task<List<StrategyTradePlanChartPoint>> Read(NpgsqlConnection db, string sql, object?[] values)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, values);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var points = new List<StrategyTradePlanChartPoint>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            points.Add(new(reader.GetDateTime(0),reader.GetDecimal(1)) { ForwardSpread = reader.IsDBNull(2) ? null : reader.GetDecimal(2) });
        return points;
    }

    [Benchmark(Baseline = true)] public Task<List<StrategyTradePlanChartPoint>> Current() => Run(false);
    [Benchmark] public Task<List<StrategyTradePlanChartPoint>> BucketSeeks() => Run(true);

    async Task<List<StrategyTradePlanChartPoint>> Run(bool seeks)
    {
        var anchored = Chart == Anchored;
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        return await Read(db, Sql(anchored, seeks), Values(anchored, Anchor, Start, Start.AddHours(4))).ConfigureAwait(false);
    }

    static object?[] Values(bool anchored, long? anchor, DateTime start, DateTime end) =>
        [1, 1, 1, 1, Position, Day, anchored ? (object?)anchor : Day.AddDays(1), start, end];

    internal static async Task PrepareAsync(bool verifyApplied = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        await Execute(db, TradePlanSchemaDb.CreateStatement).ConfigureAwait(false);
        await using (var copy = db.BeginBinaryImport($"""
            COPY {Table} (portfolioid,fundid,orderid,tradeid,positionid,valuedate,planrevision,calculatedatutc,
                combinedspreadprice,combinedforwardprice,calculationversion) FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in Samples())
            {
                copy.StartRow();
                copy.Write(1); copy.Write(1); copy.Write(1); copy.Write(1);
                copy.Write(Position); copy.Write(Day.AddDays(row.DayOffset)); copy.Write(row.Revision);
                copy.Write(row.At, NpgsqlDbType.TimestampTz);
                if (row.Spread is { } spread) copy.Write(spread); else copy.WriteNull();
                if (row.Forward is { } forward) copy.Write(forward); else copy.WriteNull();
                copy.Write(row.Flip ? ForwardVersion : "Legacy");
            }
            copy.Complete();
        }
        await Execute(db, $"VACUUM (ANALYZE) {Table}").ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(TradePlanDbContext.TradePlanDbConnection,
            new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")).ConnectionString,
            "System.Data.Postgres");
        var context = new TradePlanDbContext(settings, NullLogger<DbProvider>.Instance);
        var position = new StrategyPositionId(new TradeEntityId(1, 1, 1, 1), Position);
        foreach (var (label, anchored, anchor, start, end) in new[]
        {
            ("dense-minute", true, (long?)Anchor, Start, Start.AddHours(4)),
            ("dense-quarter-hour", false, null, Start, Start.AddHours(4)),
            ("unaligned-anchored", true, (long?)4_005, Start.AddSeconds(49), Start.AddMinutes(15).AddSeconds(22)),
            ("unaligned-range", false, null, Start.AddSeconds(49), Start.AddMinutes(15).AddSeconds(22)),
            ("unanchored-minute", true, null, Start, Start.AddMinutes(2)),
            ("inclusive-boundary", false, null, Start.AddMinutes(15), Start.AddMinutes(15)),
            ("empty", true, (long?)Anchor, Start.AddDays(-1), Start.AddHours(-1)),
            ("empty-anchor", true, (long?)0, Start, Start.AddMinutes(2)),
            ("inverted", false, null, Start.AddSeconds(2), Start.AddSeconds(1))
        })
        {
            var expected = Samples().Where(row => row.At >= start && row.At <= end && row.Spread.HasValue
                    && (!anchored || (row.DayOffset == 0 && (anchor is null || row.Revision <= anchor))))
                .GroupBy(row => row.At.Ticks / TimeSpan.FromMinutes(anchored ? 1 : 15).Ticks)
                .OrderBy(group => group.Key)
                .Select(group => group.OrderByDescending(row => row.At).ThenByDescending(row => row.Revision).First())
                .Select(row => new StrategyTradePlanChartPoint(row.At, row.Spread!.Value)
                    { ForwardSpread = row.Flip ? -row.Forward : row.Forward }).ToArray();
            var values = Values(anchored, anchor, start, end);
            var current = await Read(db, Sql(anchored, false), values).ConfigureAwait(false);
            var seeks = await Read(db, Sql(anchored, true), values).ConfigureAwait(false);
            if (!expected.SequenceEqual(current) || !expected.SequenceEqual(seeks))
                throw new InvalidOperationException($"Chart winners differed for {label}.");
            if (verifyApplied)
            {
                StrategyTradePlanChartPoint[] actual;
                if (anchored)
                {
                    // Open beyond the last revision so the price-only fixture needs no history payloads.
                    await using var session = await context.OpenOffsetHistoryAsync(position, TradeStrategyKind.IronCondor,
                        Day, RowsPerDate, 1, anchor).ConfigureAwait(false);
                    actual = await session.ReadChartAsync(start, end, default).ConfigureAwait(false);
                }
                else
                {
                    var series = await context.GetChartRangeAsync(position, Day, Day.AddDays(1), start, end).ConfigureAwait(false);
                    if (series.StartUtc != start || series.EndUtc != end)
                        throw new InvalidOperationException($"Chart window differed for {label}.");
                    actual = series.Points;
                }
                if (!expected.SequenceEqual(actual))
                    throw new InvalidOperationException($"Production chart points differed for {label}.");
            }
            Console.WriteLine($"CHART,{label},points={expected.Length},verified timestamps,spread and forward spread");
        }
        foreach (var anchored in new[] { true, false })
            foreach (var seeks in new[] { false, true })
                await Explain(db, anchored, seeks, verifyApplied && seeks).ConfigureAwait(false);
    }

    static IEnumerable<Sample> Samples()
    {
        for (var day = 0; day < 2; day++)
            for (var i = 0; i < RowsPerDate; i++)
            {
                var second = i / 10 + (i >= 30_000 ? 1_800 : 0);
                yield return new(day, day * RowsPerDate + i + 1L, Start.AddSeconds(second),
                    i % 11 == 0 ? null : (i + day * 100_000m) / 100m,
                    i % 7 == 0 ? null : (i + 100m) / 100m, i % 3 != 0);
            }
    }

    static async Task Explain(NpgsqlConnection db, bool anchored, bool seeks, bool verifyPlan)
    {
        Console.WriteLine($"PLAN,{(anchored ? Anchored : Range)},{(seeks ? "seeks" : "current")}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + Sql(anchored, seeks), db);
        PostgresValueBinding.AddParameters(command, Values(anchored, Anchor, Start, Start.AddHours(4)));
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if (verifyPlan &&
            (!plan.Contains("Index Only Scan using iron_condor_trade_plan_observation_time", StringComparison.Ordinal)
             || !plan.Contains("Heap Fetches: 0", StringComparison.Ordinal)
             || plan.Contains("temp read=", StringComparison.Ordinal)
             || plan.Contains("external merge", StringComparison.Ordinal)))
            throw new InvalidOperationException("Expected index-only bucket seeks and no disk spill.");
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    sealed record Sample(int DayOffset, long Revision, DateTime At, decimal? Spread, decimal? Forward, bool Flip);
}
