using System.Data;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class TradePlanMetadataBenchmarks
{
    const string Table = "trade_plan.iron_condor_trade_plan";
    const string Scope = "portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6";
    const int Rows = 65537;
    const int DeepOffset = 60000;
    const int Size = 50;
    const long Anchor = Rows * 2L;
    const long OlderAnchor = 2048;
    static readonly DateOnly Date = new(2026, 11, 10);
    static readonly DateTime Start = Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    static readonly string AggregateSql = Table.BuildTradePlanStatement(TradePlanDbSql.OffsetHistoryMetadataTemplate);
    static readonly string LatestSql = Table.BuildTradePlanStatement(TradePlanDbSql.LatestHistoryMetadataTemplate);
    static readonly string AnchoredSql = SplitSql(anchored: true);
    static readonly string GuardedSql = Table.BuildTradePlanStatement(TradePlanDbSql.AnchoredHistoryMetadataTemplate);
    TradePlanDbContext store = null!;
    StrategyPositionId position;
    object?[] scope = null!;
    string connectionString = null!;
    byte[] deepCursor = null!;

    static string SplitSql(bool anchored)
    {
        var filter = Scope + (anchored ? " AND planrevision <= $7" : "");
        var maximum = anchored ? "$7::bigint"
            : $"COALESCE((SELECT planrevision FROM {Table} WHERE {filter} ORDER BY planrevision DESC LIMIT 1),0)";
        return $"""
            SELECT (SELECT count(*) FROM {Table} WHERE {filter}),
                   {maximum},
                   (SELECT calculatedatutc FROM {Table} WHERE {filter} AND calculatedatutc IS NOT NULL
                    ORDER BY calculatedatutc DESC LIMIT 1);
            """;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        connectionString = builder.ConnectionString;
        var settings = new DbConnectionSettings().Add(TradePlanDbContext.TradePlanDbConnection, connectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradePlanSchemaDb(settings, logger).CreateAllAsync().ConfigureAwait(false);
        store = new(settings, logger);
        var plan = TradePlanContextConventionBenchmarks.Plan(320001, 1, -1m, true);
        position = plan.Position.Id;
        scope = BindScope(position, Date);

        await using var connection = Connect();
        await connection.OpenAsync().ConfigureAwait(false);
        await Execute(connection, $"TRUNCATE {Table};").ConfigureAwait(false);
        // Give the penultimate revision a later timestamp than the anchor row.
        TradePlanHistoryPagingBenchmarks.Seed(connection, plan, 1, Rows,
            n => Start.AddSeconds(n == Rows - 1 ? Rows + 10 : n - 1), chartSamples: true);
        var original = await ProductionPage(0, null, includeChart: true).ConfigureAwait(false);
        TradePlanHistoryPagingBenchmarks.Seed(connection, plan, Rows + 1, Rows + 2,
            n => Start.AddSeconds(n == Rows + 1 ? Rows + 30 : Rows + 20), chartSamples: true);
        var other = TradePlanContextConventionBenchmarks.Plan(320101, 1, -1m, true);
        TradePlanHistoryPagingBenchmarks.Seed(connection, other, 1, 128,
            n => Start.AddDays(1).AddSeconds(n), chartSamples: true);
        var nullable = TradePlanContextConventionBenchmarks.Plan(320201, 1, -1m, true);
        TradePlanHistoryPagingBenchmarks.Seed(connection, nullable, 1, 2, chartSamples: true);
        await Execute(connection, $"UPDATE {Table} SET calculatedatutc=NULL WHERE portfolioid=320201;").ConfigureAwait(false);
        await Execute(connection, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);

        VerifyEqual(original, await ProductionPage(0, Anchor, includeChart: true).ConfigureAwait(false));
        var preceding = await ProductionPage(DeepOffset - Size, Anchor).ConfigureAwait(false);
        deepCursor = preceding.PagingState ?? throw new InvalidOperationException("The deep cursor was missing.");
        await VerifyPagesAsync().ConfigureAwait(false);
        foreach (var (label, sql, values) in new (string, string, object?[])[]
        {
            ("aggregate latest", AggregateSql, [.. scope, Bigint(null)]),
            ("split latest", LatestSql, scope),
            ("aggregate anchored", AggregateSql, [.. scope, Anchor]),
            ("split anchored", AnchoredSql, [.. scope, Anchor]),
            ("aggregate older anchor", AggregateSql, [.. scope, OlderAnchor]),
            ("split older anchor", AnchoredSql, [.. scope, OlderAnchor]),
            ("guarded anchored", GuardedSql, [.. scope, Anchor]),
            ("guarded older anchor", GuardedSql, [.. scope, OlderAnchor])
        })
            await Explain(connection, label, sql, values).ConfigureAwait(false);
    }

    async Task VerifyPagesAsync()
    {
        foreach (var (offset, anchor, cursor) in new (int, long?, byte[]?)[]
        {
            (0, null, null), (0, Anchor, null), (DeepOffset, Anchor, null), (DeepOffset, Anchor, deepCursor),
            (DeepOffset, null, deepCursor), (Rows - Size, Anchor, null), (Rows, Anchor, null),
            (0, OlderAnchor, null), (0, 1L, null)
        })
        {
            var before = await BaselinePage(offset, anchor, cursor, includeChart: true).ConfigureAwait(false);
            var after = await ProductionPage(offset, anchor, cursor, includeChart: true).ConfigureAwait(false);
            VerifyEqual(before, after);
            if (cursor is not null)
                VerifyEqual(await BaselinePage(offset + Size, anchor, before.PagingState).ConfigureAwait(false),
                    await ProductionPage(offset + Size, anchor, after.PagingState).ConfigureAwait(false));
        }
        var anchored = await ProductionPage(0, Anchor, includeChart: true).ConfigureAwait(false);
        var latest = await ProductionPage(0, null, includeChart: true).ConfigureAwait(false);
        if (anchored.TotalCount != Rows || anchored.MaximumRevision != Anchor || anchored.ChartEndUtc != Start.AddSeconds(Rows + 10)
            || latest.TotalCount != Rows + 2 || latest.MaximumRevision != Anchor + 4 || latest.ChartEndUtc != Start.AddSeconds(Rows + 30)
            || anchored.ChartPoints.Length == 0 || latest.ChartPoints.Length == 0)
            throw new InvalidOperationException("The anchor, count or chart endpoints did not match the fixture.");
        foreach (var (id, date, count, max) in new (StrategyPositionId, DateOnly, long, long)[]
        {
            (position, Date.AddDays(1), 0, 0),
            (TradePlanContextConventionBenchmarks.Plan(320201, 1, -1m, true).Position.Id, Date, 2, 4)
        })
        {
            foreach (long? anchor in new long?[] { null, max })
            {
                await using var session = await store.OpenHistoryPageAsync(id, TradeStrategyKind.IronCondor, date, 0, Size, anchor, null)
                    .ConfigureAwait(false);
                if (session.Page.TotalCount != count || session.Page.MaximumRevision != max || session.Page.ChartEndUtc is not null
                    || session.Page.Items.Length != count)
                    throw new InvalidOperationException("Empty scope or all-null chart time behavior changed.");
            }
        }
        foreach (var anchor in new[] { Anchor, OlderAnchor, 1L })
        {
            var expected = await ReadMetadataAsync(scope, anchor, false).ConfigureAwait(false);
            if (expected != await ReadGuardedAsync(anchor).ConfigureAwait(false))
                throw new InvalidOperationException("Guarded endpoint changed anchored metadata.");
        }
        Console.WriteLine($"Correctness passed. Anchored revision {anchored.MaximumRevision}, count {anchored.TotalCount}, chart end {anchored.ChartEndUtc:O}; refreshed revision {latest.MaximumRevision}, count {latest.TotalCount}, chart end {latest.ChartEndUtc:O}. Chart points, page payloads, and cursor bytes matched. Empty and all-null scopes matched.");
    }

    static void VerifyEqual(StrategyTradePlanHistoryPage before, StrategyTradePlanHistoryPage after)
    {
        TradePlanHistoryPagingBenchmarks.VerifyEqual(before, after);
        var serializer = MessagePackBinarySerializer.Shared;
        if (!(before.PagingState ?? []).AsSpan().SequenceEqual(after.PagingState ?? [])
            || !serializer.Serialize(before.ChartPoints).AsSpan().SequenceEqual(serializer.Serialize(after.ChartPoints)))
            throw new InvalidOperationException("Chart points or continuation bytes differed.");
    }

    [Benchmark] public Task<Metadata> AggregateLatestMetadata() => ReadMetadataAsync(scope, null, false);
    [Benchmark] public Task<Metadata> SplitLatestMetadata() => ReadMetadataAsync(scope, null, true);
    [Benchmark] public Task<Metadata> AggregateAnchoredMetadata() => ReadMetadataAsync(scope, Anchor, false);
    [Benchmark] public Task<Metadata> SplitAnchoredMetadata() => ReadMetadataAsync(scope, Anchor, true);
    [Benchmark] public Task<StrategyTradePlanHistoryPage> AggregateFirstPage() => BaselinePage(0, null);
    [Benchmark] public Task<StrategyTradePlanHistoryPage> SplitFirstPage() => SplitPage(0, null);
    [Benchmark] public Task<StrategyTradePlanHistoryPage> AggregateDeepPage() => BaselinePage(DeepOffset, Anchor, deepCursor);
    [Benchmark] public Task<StrategyTradePlanHistoryPage> SplitDeepPage() => SplitPage(DeepOffset, Anchor, deepCursor);
    [Benchmark] public Task<Metadata> AggregateOlderMetadata() => ReadMetadataAsync(scope, OlderAnchor, false);
    [Benchmark] public Task<Metadata> SplitOlderMetadata() => ReadMetadataAsync(scope, OlderAnchor, true);
    [Benchmark] public Task<Metadata> GuardedOlderMetadata() => ReadGuardedAsync(OlderAnchor);
    [Benchmark] public Task<Metadata> GuardedRecentMetadata() => ReadGuardedAsync(Anchor);

    async Task<Metadata> ReadGuardedAsync(long anchor)
    {
        await using var connection = Connect();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = Command(connection, GuardedSql, [.. scope, anchor]);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await reader.ReadAsync().ConfigureAwait(false);
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
    }

    async Task<StrategyTradePlanHistoryPage> ProductionPage(int offset, long? anchor, byte[]? cursor = null, bool includeChart = false)
    {
        await using var session = await store.OpenHistoryPageAsync(position, TradeStrategyKind.IronCondor, Date, offset, Size, anchor, cursor)
            .ConfigureAwait(false);
        return await ReadPageWithChartAsync(session, includeChart).ConfigureAwait(false);
    }

    Task<StrategyTradePlanHistoryPage> BaselinePage(int offset, long? anchor, byte[]? cursor = null, bool includeChart = false) =>
        ReadPrototypePageAsync(offset, anchor, cursor, includeChart, split: false);

    Task<StrategyTradePlanHistoryPage> SplitPage(int offset, long? anchor, byte[]? cursor = null, bool includeChart = false) =>
        ReadPrototypePageAsync(offset, anchor, cursor, includeChart, split: true);

    async Task<StrategyTradePlanHistoryPage> ReadPrototypePageAsync(int offset, long? anchor, byte[]? cursor, bool includeChart, bool split)
    {
        await using var connection = Connect();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead).ConfigureAwait(false);
        var resume = cursor is null ? null : JsonSerializer.Deserialize<Cursor>(cursor);
        var metadata = await ReadMetadataAsync(connection, transaction, scope, anchor ?? resume?.MaximumRevision, split).ConfigureAwait(false);
        var (sql, values) = resume is not null
            ? (TradePlanDbSql.ContinueHistoryRowsTemplate, new object?[] { metadata.Anchor, resume.LastRevision, Size })
            : offset == 0 ? (TradePlanDbSql.FirstHistoryRowsTemplate, [metadata.Anchor, Size])
            : (TradePlanDbSql.OffsetHistoryRowsTemplate, [metadata.Anchor, offset, Size]);
        var items = new List<StrategyTradePlanSnapshot>(Size);
        long last = 0;
        await using (var command = Command(connection, Table.BuildTradePlanStatement(sql), [.. scope, .. values], transaction))
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                items.Add(MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(reader.GetFieldValue<byte[]>(0)));
                last = reader.GetInt64(1);
            }
        }
        var next = items.Count > 0 && offset + items.Count < metadata.Count
            ? JsonSerializer.SerializeToUtf8Bytes(new Cursor(Table.PageScope(scope), metadata.Anchor, last, offset + items.Count))
            : null;
        var page = new StrategyTradePlanHistoryPage(items.ToArray(), next)
        {
            Offset = offset, TotalCount = metadata.Count, MaximumRevision = metadata.Anchor, ChartEndUtc = metadata.ChartEndUtc
        };
        if (!includeChart || offset != 0 || page.ChartEndUtc is null)
            return page;
        var chartSession = new PostgresTradePlanOffsetReadSession(connection, transaction, Table, scope, page);
        return await ReadPageWithChartAsync(chartSession, includeChart).ConfigureAwait(false);
    }

    static async Task<StrategyTradePlanHistoryPage> ReadPageWithChartAsync(ITradePlanOffsetReadSession session, bool includeChart)
    {
        var page = session.Page;
        return includeChart && page.Offset == 0 && page.ChartEndUtc is { } end
            ? page with { ChartPoints = await session.ReadChartAsync(end.AddHours(-6), end, default).ConfigureAwait(false) }
            : page;
    }

    async Task<Metadata> ReadMetadataAsync(object?[] keys, long? anchor, bool split)
    {
        await using var connection = Connect();
        await connection.OpenAsync().ConfigureAwait(false);
        return await ReadMetadataAsync(connection, null, keys, anchor, split).ConfigureAwait(false);
    }

    static async Task<Metadata> ReadMetadataAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        object?[] keys, long? anchor, bool split)
    {
        var sql = !split ? AggregateSql : anchor.HasValue ? AnchoredSql : LatestSql;
        var values = split && anchor is null ? keys : [.. keys, Bigint(anchor)];
        await using var command = Command(connection, sql, values, transaction);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await reader.ReadAsync().ConfigureAwait(false);
        return new(reader.GetInt64(0), anchor ?? reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
    }

    static object?[] BindScope(StrategyPositionId id, DateOnly date) =>
        (object?[])new GetTradePlan(id.Trade.PortfolioId, id.Trade.FundId, id.Trade.OrderId, id.Trade.TradeId, id.PositionId, date).Bind();

    NpgsqlConnection Connect() => new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);

    static NpgsqlCommand Command(NpgsqlConnection connection, string sql, object?[] values, NpgsqlTransaction? transaction = null)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        PostgresValueBinding.AddParameters(command, values);
        return command;
    }

    static async Task Explain(NpgsqlConnection connection, string label, string sql, object?[] values)
    {
        Console.WriteLine($"Plan for {label}:");
        await using var command = Command(connection, "EXPLAIN (ANALYZE, BUFFERS) " + sql, values);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if ((label is "split latest" or "guarded anchored" or "guarded older anchor")
            && (!plan.Contains("Index Only Scan using iron_condor_trade_plan_observation_time", StringComparison.Ordinal)
                || !plan.Contains("Index Only Scan using iron_condor_trade_plan_pkey", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Missing expected metadata index access for {label}.");
        if (label is "guarded anchored" or "guarded older anchor")
        {
            if (!plan.Contains("rows=128 loops=1", StringComparison.Ordinal)
                || !plan.Contains("One-Time Filter:", StringComparison.Ordinal)
                || !lines.Any(line => label == "guarded anchored"
                    ? line.Contains("(never executed)", StringComparison.Ordinal)
                    : line.Contains("rows=1024 loops=1", StringComparison.Ordinal)))
                throw new InvalidOperationException($"The bounded probe or aggregate fallback differed for {label}.");
        }
    }

    static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public sealed record Metadata(long Count, long Anchor, DateTime? ChartEndUtc);
    sealed record Cursor(string Scope, long MaximumRevision, long LastRevision, int Offset);
}
