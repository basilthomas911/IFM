using System.Data;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
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

/// <summary>Compares the current offset session with an anchored revision-cursor prototype.</summary>
[MemoryDiagnoser]
public class TradePlanHistoryPagingBenchmarks
{
    const string Table = "trade_plan.iron_condor_trade_plan";
    const string ScopeFilter = "portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6";
    const int RowCount = 65537;
    const int PageSize = 50;
    const int DeepOffset = 60000;
    static readonly DateOnly ValueDate = new(2026, 11, 10);
    static readonly DateTime Start = ValueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    static readonly string OffsetRows = Table.BuildTradePlanStatement(TradePlanDbSql.OffsetHistoryRowsTemplate);
    static readonly string Metadata = Table.BuildTradePlanStatement(TradePlanDbSql.OffsetHistoryMetadataTemplate);
    static readonly string KeysetFirst = KeysetSql(false);
    static readonly string KeysetAfter = KeysetSql(true);
    static readonly string CursorLookup = $"""
        SELECT planrevision FROM {Table}
        WHERE {ScopeFilter} AND planrevision <= $7 ORDER BY planrevision DESC OFFSET $8 LIMIT 1;
        """;

    string connectionString = null!;
    TradePlanDbContext store = null!;
    StrategyTradePlanSnapshot prototype = null!;
    object?[] scope = null!;
    long originalAnchor;
    long latestAnchor;
    RevisionCursor deepCursor = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        connectionString = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");

        var settings = new DbConnectionSettings().Add(TradePlanDbContext.TradePlanDbConnection, connectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradePlanSchemaDb(settings, logger).CreateAllAsync().ConfigureAwait(false);
        store = new(settings, logger);
        prototype = TradePlanContextConventionBenchmarks.Plan(320001, 1, -1m, true);
        var id = prototype.Position.Id;
        scope = (object?[])new GetTradePlan(id.Trade.PortfolioId, id.Trade.FundId, id.Trade.OrderId,
            id.Trade.TradeId, id.PositionId, ValueDate).Bind();

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, $"TRUNCATE {Table};").ConfigureAwait(false);
        Seed(connection, 1, RowCount);
        var original = await ReadOffsetPageAsync(0, null).ConfigureAwait(false);
        originalAnchor = original.MaximumRevision!.Value;
        if (originalAnchor != RowCount * 2L || original.TotalCount != RowCount || original.ChartEndUtc != At(RowCount))
            throw new InvalidOperationException("Unexpected initial revision anchor or metadata.");
        VerifyEqual(original, (await ReadKeysetPageAsync(0, null, null).ConfigureAwait(false)).Page);
        var cursor = await AcquireDeepCursor().ConfigureAwait(false);
        deepCursor = new(originalAnchor, cursor);
        if (cursor != 2L * (RowCount - DeepOffset + 1))
            throw new InvalidOperationException("The deep cursor did not match the key preceding the requested page.");
        var deepBefore = await ReadOffsetPageAsync(DeepOffset, originalAnchor).ConfigureAwait(false);

        Seed(connection, RowCount + 1, RowCount + 2);
        await ExecuteAsync(connection, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
        var refreshed = await ReadOffsetPageAsync(0, null).ConfigureAwait(false);
        latestAnchor = refreshed.MaximumRevision!.Value;
        if (latestAnchor != (RowCount + 2) * 2L || refreshed.TotalCount != RowCount + 2 || refreshed.ChartEndUtc != At(RowCount + 2)
            || refreshed.Items[0].PlanRevision != latestAnchor)
            throw new InvalidOperationException("Refreshing the first page did not include the new revisions.");
        VerifyEqual(refreshed, (await ReadKeysetPageAsync(0, null, null).ConfigureAwait(false)).Page);
        VerifyEqual(original, await ReadOffsetPageAsync(0, originalAnchor).ConfigureAwait(false));
        VerifyEqual(original, (await ReadKeysetPageAsync(0, originalAnchor, null).ConfigureAwait(false)).Page);
        var deepAfter = await ReadOffsetPageAsync(DeepOffset, originalAnchor).ConfigureAwait(false);
        VerifyEqual(deepBefore, deepAfter);
        var resumed = await ReadKeysetPageAsync(DeepOffset, originalAnchor, deepCursor).ConfigureAwait(false);
        VerifyEqual(deepAfter, resumed.Page);
        var following = await ReadKeysetPageAsync(DeepOffset + PageSize, originalAnchor, resumed.Next).ConfigureAwait(false);
        VerifyEqual(await ReadOffsetPageAsync(DeepOffset + PageSize, originalAnchor).ConfigureAwait(false), following.Page);
        if (resumed.Page.Items[0].PlanRevision != deepCursor.LastRevision - 2
            || resumed.Page.Items[^1].PlanRevision <= following.Page.Items[0].PlanRevision)
            throw new InvalidOperationException("The continuation missed or repeated a sparse revision.");
        var tailCursor = new RevisionCursor(originalAnchor, 2L * 38);
        var tail = await ReadKeysetPageAsync(RowCount - 37, originalAnchor, tailCursor).ConfigureAwait(false);
        VerifyEqual(await ReadOffsetPageAsync(RowCount - 37, originalAnchor).ConfigureAwait(false), tail.Page);
        if (tail.Next is not null || tail.Page.Items.Length != 37)
            throw new InvalidOperationException("The terminal page returned an unexpected cursor.");

        Console.WriteLine($"Correctness passed. Anchor {originalAnchor} survived two newer revisions. Refresh anchor {latestAnchor}. First, deep, following and terminal pages matched with count and chart end.");
        await ExplainAsync(connection, "offset first rows", OffsetRows, [.. scope, latestAnchor, 0, PageSize]).ConfigureAwait(false);
        await ExplainAsync(connection, "keyset first rows", KeysetFirst, [.. scope, latestAnchor, PageSize + 1]).ConfigureAwait(false);
        await ExplainAsync(connection, "offset deep rows", OffsetRows, [.. scope, originalAnchor, DeepOffset, PageSize]).ConfigureAwait(false);
        await ExplainAsync(connection, "keyset deep rows", KeysetAfter, [.. scope, originalAnchor, deepCursor.LastRevision, PageSize + 1]).ConfigureAwait(false);
        await ExplainAsync(connection, "first page metadata", Metadata, [.. scope, Bigint(null)]).ConfigureAwait(false);
        await ExplainAsync(connection, "anchored metadata", Metadata, [.. scope, originalAnchor]).ConfigureAwait(false);
        await ExplainAsync(connection, "deep cursor lookup", CursorLookup, [.. scope, originalAnchor, DeepOffset - 1]).ConfigureAwait(false);
    }

    [Benchmark] public Task<StrategyTradePlanHistoryPage> OffsetFirstPage() => ReadOffsetPageAsync(0, null);
    [Benchmark] public Task<StrategyTradePlanHistoryPage> OffsetDeepPage() => ReadOffsetPageAsync(DeepOffset, originalAnchor);
    [Benchmark] public Task<KeysetPage> KeysetFirstPage() => ReadKeysetPageAsync(0, null, null);
    [Benchmark] public Task<KeysetPage> KeysetDeepPage() => ReadKeysetPageAsync(DeepOffset, originalAnchor, deepCursor);
    [Benchmark] public Task<HistoryRows> OffsetFirstRows() => ReadRowsAsync(OffsetRows, [.. scope, latestAnchor, 0, PageSize]);
    [Benchmark] public Task<HistoryRows> OffsetDeepRows() => ReadRowsAsync(OffsetRows, [.. scope, originalAnchor, DeepOffset, PageSize]);
    [Benchmark] public Task<HistoryRows> KeysetFirstRows() => ReadRowsAsync(KeysetFirst, [.. scope, latestAnchor, PageSize + 1], keyset: true);
    [Benchmark] public Task<HistoryRows> KeysetDeepRows() =>
        ReadRowsAsync(KeysetAfter, [.. scope, originalAnchor, deepCursor.LastRevision, PageSize + 1], keyset: true);

    [Benchmark]
    public async Task<long> AcquireDeepCursor()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = Command(connection, CursorLookup, [.. scope, originalAnchor, DeepOffset - 1]);
        return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    async Task<StrategyTradePlanHistoryPage> ReadOffsetPageAsync(int offset, long? anchor)
    {
        await using var session = await store.OpenOffsetHistoryAsync(prototype.Position.Id, TradeStrategyKind.IronCondor,
            ValueDate, offset, PageSize, anchor).ConfigureAwait(false);
        return session.Page;
    }

    async Task<KeysetPage> ReadKeysetPageAsync(int offset, long? anchor, RevisionCursor? cursor)
    {
        if (cursor is not null && cursor.Anchor != anchor)
            throw new InvalidOperationException("The continuation belongs to a different revision anchor.");
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead).ConfigureAwait(false);
        long count, maximum;
        DateTime? chartEnd;
        await using (var metadata = Command(connection, Metadata, [.. scope, Bigint(anchor)], transaction))
        await using (var reader = await metadata.ExecuteReaderAsync().ConfigureAwait(false))
        {
            await reader.ReadAsync().ConfigureAwait(false);
            count = reader.GetInt64(0);
            maximum = anchor ?? reader.GetInt64(1);
            chartEnd = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
        }
        var rows = cursor is null
            ? await ReadRowsAsync(connection, KeysetFirst, [.. scope, maximum, PageSize + 1], true, transaction).ConfigureAwait(false)
            : await ReadRowsAsync(connection, KeysetAfter, [.. scope, maximum, cursor.LastRevision, PageSize + 1], true, transaction).ConfigureAwait(false);
        return new(new(rows.Items, null) { Offset = offset, TotalCount = count, MaximumRevision = maximum, ChartEndUtc = chartEnd },
            rows.HasMore ? new(maximum, rows.LastRevision) : null);
    }

    async Task<HistoryRows> ReadRowsAsync(string sql, object?[] parameters, bool keyset = false)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        return await ReadRowsAsync(connection, sql, parameters, keyset).ConfigureAwait(false);
    }

    static async Task<HistoryRows> ReadRowsAsync(NpgsqlConnection connection, string sql, object?[] parameters,
        bool keyset, NpgsqlTransaction? transaction = null)
    {
        await using var command = Command(connection, sql, parameters, transaction);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var items = new List<StrategyTradePlanSnapshot>(PageSize);
        long lastRevision = 0;
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (items.Count == PageSize)
                return new(items.ToArray(), lastRevision, true);
            items.Add(MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(reader.GetFieldValue<byte[]>(0)));
            if (keyset) lastRevision = reader.GetInt64(1);
        }
        return new(items.ToArray(), lastRevision, false);
    }

    internal static void Seed(NpgsqlConnection connection, StrategyTradePlanSnapshot prototype, int first, int last,
        Func<int, DateTime>? calculatedAt = null, bool chartSamples = false)
    {
        var chartColumns = chartSamples ? ",combinedspreadprice,combinedforwardprice,calculationversion" : "";
        using var writer = connection.BeginBinaryImport($"""
            COPY {Table} (portfolioid,fundid,orderid,tradeid,positionid,valuedate,planrevision,calculatedatutc,
                state,requiresexit,contenthash,payload{chartColumns}) FROM STDIN (FORMAT BINARY)
            """);
        var position = prototype.Position;
        for (int ordinal = first; ordinal <= last; ordinal++)
        {
            var revision = ordinal * 2L;
            var at = calculatedAt?.Invoke(ordinal) ?? At(ordinal);
            var plan = prototype with
            {
                ValueDate = ValueDate,
                PlanRevision = revision,
                CalculatedAtUtc = at,
                Position = position with { PositionSequence = revision, AsOfUtc = at },
                TotalPnl = -ordinal,
                ContentHash = revision.ToString(CultureInfo.InvariantCulture)
            };
            writer.StartRow();
            writer.Write(position.Id.Trade.PortfolioId);
            writer.Write(position.Id.Trade.FundId);
            writer.Write(position.Id.Trade.OrderId);
            writer.Write(position.Id.Trade.TradeId);
            writer.Write(position.Id.PositionId);
            writer.Write(ValueDate);
            writer.Write(revision);
            writer.Write(at, NpgsqlDbType.TimestampTz);
            writer.Write(plan.State.ToString());
            writer.Write(plan.RequiresExit);
            writer.Write(plan.ContentHash);
            writer.Write(MessagePackBinarySerializer.Shared.Serialize(plan), NpgsqlDbType.Bytea);
            if (chartSamples)
            {
                writer.Write(-10m - ordinal % 17, NpgsqlDbType.Numeric);
                writer.Write(11m + ordinal % 17, NpgsqlDbType.Numeric);
                writer.Write("IronCondor/EntryCreditForwardPnl/v2");
            }
        }
        if (writer.Complete() != (ulong)(last - first + 1))
            throw new InvalidOperationException("Trade Plan fixture row count is incorrect.");
    }

    void Seed(NpgsqlConnection connection, int first, int last) => Seed(connection, prototype, first, last);

    static DateTime At(int ordinal) => Start.AddSeconds(ordinal - 1);

    internal static void VerifyEqual(StrategyTradePlanHistoryPage offset, StrategyTradePlanHistoryPage keyset)
    {
        var serializer = MessagePackBinarySerializer.Shared;
        if (offset.Offset != keyset.Offset || offset.TotalCount != keyset.TotalCount
            || offset.MaximumRevision != keyset.MaximumRevision || offset.ChartEndUtc != keyset.ChartEndUtc
            || !serializer.Serialize(offset.Items).AsSpan().SequenceEqual(serializer.Serialize(keyset.Items)))
            throw new InvalidOperationException("OFFSET and keyset page results or metadata differed.");
    }

    static string KeysetSql(bool continuation) => string.Format(CultureInfo.InvariantCulture, TradePlanDbSql.ReadPageTemplate,
        "planrevision", Table, ScopeFilter + " AND planrevision <= $7" + (continuation ? " AND planrevision < $8" : ""),
        "planrevision DESC", continuation ? "$9" : "$8");

    static async Task ExplainAsync(NpgsqlConnection connection, string label, string sql, object?[] parameters)
    {
        Console.WriteLine($"Plan for {label}:");
        await using var command = Command(connection, "EXPLAIN (ANALYZE, BUFFERS) " + sql, parameters);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            Console.WriteLine(reader.GetString(0));
    }

    NpgsqlConnection CreateConnection() => new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);

    static NpgsqlCommand Command(NpgsqlConnection connection, string sql, object?[] parameters, NpgsqlTransaction? transaction = null)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        PostgresValueBinding.AddParameters(command, parameters);
        return command;
    }

    static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public sealed record RevisionCursor(long Anchor, long LastRevision);
    public sealed record KeysetPage(StrategyTradePlanHistoryPage Page, RevisionCursor? Next);
    public sealed record HistoryRows(StrategyTradePlanSnapshot[] Items, long LastRevision, bool HasMore);
}
