using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Tests a date-scoped revision index using the normal order fills and projection entry points.</summary>
[MemoryDiagnoser]
public class FundOrderFillsIndexBenchmarks
{
    const int Portfolio = 10;
    const int Fund = 20;
    const int Days = 9;
    const int OrdersPerDay = 12000;
    const int TargetDay = 4;
    const int FirstOrder = TargetDay * OrdersPerDay + 1;
    const int LastOrder = FirstOrder + OrdersPerDay - 1;
    const int DeepOrder = FirstOrder + 9999;
    const int FirstWriteOrder = Days * OrdersPerDay + 1;
    const string IndexName = "trade_order_fills_page_ordered";
    static readonly DateOnly StartDate = new(2026, 11, 2);
    static readonly DateOnly ValueDate = StartDate.AddDays(TargetDay);
    static readonly DateOnly WriteDate = StartDate.AddDays(Days);
    static readonly MessagePackBinarySerializer Serializer = MessagePackBinarySerializer.Shared;
    TradeDbContext database = null!;
    LegacyFundOrderFillsQuery legacy = null!;
    string initialWal = null!;
    int writes;

    static string ConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return builder.ConnectionString;
    }

    static IDbConnectionSettings Settings() => new DbConnectionSettings()
        .Add(TradeDbContext.TradeDbConnection, ConnectionString(), "System.Data.Postgres");

    static TradeDbContext CreateContext() =>
        new(Settings(), new DbContextFactory(new DbContextResolver(type =>
            throw new InvalidOperationException($"Unexpected repository lookup: {type}."))),
            new UnexpectedSequenceUse(), NullLogger<DbProvider>.Instance);

    static NpgsqlConnection CreateConnection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    internal static async Task PrepareAsync(bool withIndex, bool compareSplit = false, bool verifyMigration = false)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        if (await ScalarAsync(connection, """
            SELECT current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on'
                AND current_setting('full_page_writes') = 'on';
            """).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("The benchmark requires full WAL durability.");
        if (await ScalarAsync(connection, "SELECT to_regclass('trade.trade_order') IS NULL;").ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Run each arm on a fresh disposable database.");

        var schema = new TradeSchemaDb(Settings(), NullLogger<DbProvider>.Instance);
        await schema.CreateAllAsync().ConfigureAwait(false);
        if (verifyMigration)
            Console.WriteLine($"Fresh schema index OID: {await ReadIndexOidAsync(connection).ConfigureAwait(false)}.");

        // Recreate the pre-index layout on this disposable database before loading the upgrade fixture.
        await ExecuteAsync(connection, $"DROP INDEX trade.{IndexName};").ConfigureAwait(false);
        if (await ScalarAsync(connection, $"SELECT to_regclass('trade.{IndexName}') IS NULL;").ConfigureAwait(false) is not true)
            throw new InvalidOperationException("The pre-index layout still has the candidate index.");
        CopyOrders(connection);
        CopyAttempts(connection);
        await ExecuteAsync(connection, $"""
            INSERT INTO trade.established_trade (portfolioid, fundid, orderid, tradeid)
            SELECT {Portfolio}, {Fund}, id, id + n
            FROM generate_series({FirstOrder}, {LastOrder}) AS orders(id)
            CROSS JOIN generate_series(1, 2) AS trades(n)
            WHERE id % 8 = 4 OR (id % 8 = 0 AND n = 1);
            """).ConfigureAwait(false);
        foreach (var table in new[] { "trade_order", "order_execution", "established_trade" })
            await ExecuteAsync(connection, $"VACUUM (ANALYZE) trade.{table};").ConfigureAwait(false);

        var pages = await CapturePagesAsync(CreateLegacy()).ConfigureAwait(false);
        var beforeRows = verifyMigration ? await ReadRowCountsAsync(connection).ConfigureAwait(false) : null;
        if (withIndex)
            await schema.CreateAsync(["trade"]).ConfigureAwait(false);
        if (verifyMigration)
        {
            var indexOid = await ReadIndexOidAsync(connection).ConfigureAwait(false);
            await schema.CreateAsync(["trade"]).ConfigureAwait(false);
            if (indexOid != await ReadIndexOidAsync(connection).ConfigureAwait(false)
                || !Equals(beforeRows, await ReadRowCountsAsync(connection).ConfigureAwait(false)))
                throw new InvalidOperationException("Reapplying the migration changed the index identity or fixture row counts.");
            Console.WriteLine($"Populated upgrade and repeat migration passed. Index OID: {indexOid}. Row counts: {beforeRows}.");
        }
        if (compareSplit)
            await VerifyReplayAsync(CreateContext(), pages).ConfigureAwait(false);
        var bytes = await ScalarAsync(connection,
            $"SELECT COALESCE(pg_relation_size(to_regclass('trade.{IndexName}')), 0);").ConfigureAwait(false);
        Console.WriteLine($"Candidate index bytes: {bytes}.");
        await ExplainAsync(connection, "legacy first", 0, Guid.Empty).ConfigureAwait(false);
        await ExplainAsync(connection, "legacy deep", DeepOrder, AttemptId(1)).ConfigureAwait(false);
        if (compareSplit)
        {
            var first = await ExplainAsync(connection, "production first", 0, Guid.Empty, useSplit: true).ConfigureAwait(false);
            var deep = await ExplainAsync(connection, "production deep", DeepOrder, AttemptId(1), useSplit: true).ConfigureAwait(false);
            if (verifyMigration)
            {
                if (!first.Contains($"Index Scan using {IndexName}", StringComparison.Ordinal)
                    || !deep.Contains($"Index Only Scan using {IndexName}", StringComparison.Ordinal)
                    || !deep.Contains($"orderid >= {DeepOrder}", StringComparison.Ordinal)
                    || deep.Contains("Bitmap Heap Scan", StringComparison.Ordinal))
                    throw new InvalidOperationException("The deployed first or deep query lost its expected index seek.");
                Console.WriteLine("Production first and deep plan verification passed.");
            }
        }
    }

    static LegacyFundOrderFillsQuery CreateLegacy() => new(new DbConnectionSettings()
        .Add(EventSourceActorDbContext.EventSourceActorDbConnection, ConnectionString(), "System.Data.Postgres"));

    static async Task<long> ReadIndexOidAsync(NpgsqlConnection connection)
    {
        var oid = await ScalarAsync(connection, $"""
            SELECT indexrelid::bigint FROM pg_index
            WHERE indexrelid = to_regclass('trade.{IndexName}')
                AND indisvalid AND indisready
                AND pg_get_indexdef(indexrelid) =
                    'CREATE INDEX {IndexName} ON trade.trade_order USING btree (portfolioid, fundid, valuedate, orderid, revision DESC)';
            """).ConfigureAwait(false);
        return oid is long value ? value : throw new InvalidOperationException("The TradeDb index is missing, invalid, or has different keys.");
    }

    static Task<object?> ReadRowCountsAsync(NpgsqlConnection connection) => ScalarAsync(connection, """
        SELECT concat_ws(', ', (SELECT count(*) FROM trade.trade_order),
            (SELECT count(*) FROM trade.order_execution), (SELECT count(*) FROM trade.established_trade));
        """);

    static void CopyOrders(NpgsqlConnection connection)
    {
        using var copy = connection.BeginBinaryImport("""
            COPY trade.trade_order (portfolioid, fundid, orderid, revision, status, valuedate, updatedatutc, definitionhash, payload)
            FROM STDIN (FORMAT BINARY)
            """);
        for (var day = 0; day < Days; day++)
            for (var index = 1; index <= OrdersPerDay; index++)
                for (var revision = 1; revision <= 3; revision++)
                    WriteOrder(copy, Order(new(Portfolio, Fund, day * OrdersPerDay + index), StartDate.AddDays(day), revision));
        for (var id = FirstOrder; id < FirstOrder + 2000; id++)
        {
            WriteOrder(copy, Order(new(Portfolio, Fund + 1, id), ValueDate, 1));
            WriteOrder(copy, Order(new(Portfolio + 1, Fund, id), ValueDate, 1));
        }
        WriteOrder(copy, Order(new(Portfolio, Fund, FirstOrder), ValueDate.AddDays(1), 4));
        WriteOrder(copy, Order(new(Portfolio, Fund, DeepOrder + 1), ValueDate.AddDays(1), 4));
        copy.Complete();
    }

    static void WriteOrder(NpgsqlBinaryImporter copy, TradeOrderDefinition order)
    {
        copy.StartRow();
        copy.Write(order.Id.PortfolioId);
        copy.Write(order.Id.FundId);
        copy.Write(order.Id.OrderId);
        copy.Write(order.Revision);
        copy.Write(order.Status.ToString());
        copy.Write(order.ValueDate);
        copy.Write(AcceptedAt(order.ValueDate).AddMinutes(order.Revision - 1), NpgsqlDbType.TimestampTz);
        copy.Write(order.DefinitionHash);
        copy.Write(Serializer.Serialize(order));
    }

    static void CopyAttempts(NpgsqlConnection connection)
    {
        using var copy = connection.BeginBinaryImport("""
            COPY trade.order_execution (portfolioid, fundid, orderid, executionattemptid, status, startedatutc, payload)
            FROM STDIN (FORMAT BINARY)
            """);
        for (var id = 4; id <= Days * OrdersPerDay; id += 4)
        {
            var date = StartDate.AddDays((id - 1) / OrdersPerDay);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var execution = Execution(id, date, attempt);
                copy.StartRow();
                copy.Write(Portfolio);
                copy.Write(Fund);
                copy.Write(id);
                copy.Write(execution.ExecutionAttemptId);
                copy.Write(execution.Status.ToString());
                copy.Write(execution.StartedAtUtc, NpgsqlDbType.TimestampTz);
                copy.Write(Serializer.Serialize(execution));
            }
        }
        copy.Complete();
    }

    static TradeOrderDefinition Order(TradeOrderId id, DateOnly date, int revision) => new()
    {
        Id = id, ValueDate = date, Revision = revision, DefinitionHash = $"revision-{revision}",
        Status = revision switch { 1 => TradeOrderStatus.Approved, 2 => TradeOrderStatus.Executing,
            3 => TradeOrderStatus.Completed, _ => TradeOrderStatus.Cancelled }
    };

    static DateTime AcceptedAt(DateOnly date) => date.ToDateTime(new(14, 0), DateTimeKind.Utc);
    static Guid AttemptId(int attempt) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)attempt);
    static ExecutionFillEvidence Fill(int orderId, DateTime at) => new()
    {
        ExecutionFillId = new(orderId, 0, 0, new byte[8]), ExecutionAttemptId = AttemptId(3),
        ContractId = "ES-BENCH", SignedQuantity = 1, Price = 5000.25m, FilledAtUtc = at.AddSeconds(5)
    };

    static OrderExecutionDefinition Execution(int id, DateOnly date, int attempt)
    {
        var at = AcceptedAt(date).AddMinutes(10 + attempt);
        return new()
        {
            TradeOrderId = new(Portfolio, Fund, id), OrderRevision = 3, ExecutionAttemptId = AttemptId(attempt),
            Status = attempt switch { 1 => OrderExecutionStatus.Cancelled, 2 => OrderExecutionStatus.Rejected,
                _ => OrderExecutionStatus.Filled },
            StartedAtUtc = at, OrderQuantity = 1, CumulativeFilledQuantity = attempt == 3 ? 1 : 0,
            Fills = attempt == 3 ? [Fill(id, at)] : [], RejectionDetail = attempt == 2 ? "Benchmark rejection" : null
        };
    }

    static IEnumerable<FundOrderFillsRow> ExpectedRows()
    {
        for (var id = FirstOrder; id <= LastOrder; id++)
        {
            if (id % 4 != 0)
            {
                yield return new(id, null, Guid.Empty, "Completed", [], AcceptedAt(ValueDate), Components: []);
                continue;
            }
            int? tradeId = id % 8 == 0 ? id + 1 : null;
            var start = AcceptedAt(ValueDate).AddMinutes(11);
            yield return new(id, tradeId, AttemptId(1), "Cancelled", [], start, 1, Components: []);
            yield return new(id, tradeId, AttemptId(2), "Rejected", [], start.AddMinutes(1), 1,
                RejectionDetail: "Benchmark rejection", Components: []);
            var fill = Fill(id, start.AddMinutes(2));
            yield return new(id, tradeId, AttemptId(3), "Filled", [fill], start.AddMinutes(2), 1, 1, fill.FilledAtUtc, Components: []);
        }
    }

    sealed record CapturedPage(int Order, Guid Attempt, byte[] Payload);

    static async Task<List<CapturedPage>> CapturePagesAsync(LegacyFundOrderFillsQuery query)
    {
        var expected = ExpectedRows().ToArray();
        var pages = new List<CapturedPage>();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        async Task<(int Order, Guid Attempt)> Check(int offset, (int Order, Guid Attempt) cursor)
        {
            var rows = await query.ReadAsync(Portfolio, Fund, ValueDate, cursor.Order, cursor.Attempt).ConfigureAwait(false);
            var actualBytes = Serializer.Serialize(rows);
            var expectedBytes = Serializer.Serialize(expected.Skip(offset).Take(FundOrderFillsRow.PageSize).ToArray());
            if (!actualBytes.AsSpan().SequenceEqual(expectedBytes))
                throw new InvalidOperationException($"Page at row {offset} differed from the expected ordered payloads.");
            pages.Add(new(cursor.Order, cursor.Attempt, actualBytes));
            hash.AppendData(actualBytes);
            return rows.Length == 0 ? cursor : (rows[^1].OrderId, rows[^1].ExecutionAttemptId);
        }

        var first = await Check(0, (0, Guid.Empty)).ConfigureAwait(false);
        await Check(FundOrderFillsRow.PageSize, first).ConfigureAwait(false);
        var deepOffset = Array.FindIndex(expected, row => row.OrderId == DeepOrder && row.ExecutionAttemptId == AttemptId(1)) + 1;
        var previousOffset = deepOffset - FundOrderFillsRow.PageSize;
        var anchor = expected[previousOffset - 1];
        var preceding = await query.ReadAsync(Portfolio, Fund, ValueDate, anchor.OrderId, anchor.ExecutionAttemptId)
            .ConfigureAwait(false);
        var deepCursor = (preceding[^1].OrderId, preceding[^1].ExecutionAttemptId);
        if (deepCursor != (DeepOrder, AttemptId(1)))
            throw new InvalidOperationException("The legacy query did not reproduce the expected deep cursor.");
        var deep = await Check(deepOffset, deepCursor).ConfigureAwait(false);
        await Check(deepOffset + FundOrderFillsRow.PageSize, deep).ConfigureAwait(false);
        var last = await Check(expected.Length - 1, (LastOrder, AttemptId(2))).ConfigureAwait(false);
        await Check(expected.Length, last).ConfigureAwait(false);
        Console.WriteLine($"Legacy paging correctness passed. First continuation {first}, deep input {deepCursor} at row offset {deepOffset}, deep continuation {deep}, terminal cursor {last}.");
        Console.WriteLine($"Legacy page fingerprint: {Convert.ToHexString(hash.GetHashAndReset())}.");
        return pages;
    }

    static async Task VerifyReplayAsync(TradeDbContext context, List<CapturedPage> pages)
    {
        foreach (var page in pages)
        {
            var replay = await context.GetFundOrderFillsAsync(Portfolio, Fund, ValueDate, page.Order, page.Attempt).ConfigureAwait(false);
            if (!page.Payload.AsSpan().SequenceEqual(Serializer.Serialize(replay)))
                throw new InvalidOperationException($"Production paging differed at cursor ({page.Order}, {page.Attempt}).");
        }
        Console.WriteLine($"Production paging replayed all {pages.Count} legacy requests with identical ordered payloads.");
    }

    static async Task<string> ExplainAsync(NpgsqlConnection connection, string name, int order, Guid attempt, bool useSplit = false)
    {
        var first = useSplit && order == 0 && attempt == Guid.Empty;
        var sql = !useSplit ? LegacyFundOrderFillsQuery.Sql : first
            ? TradeDbSql.ReadFundOrderFillsFirstPage : TradeDbSql.ReadFundOrderFillsContinuationPage;
        object?[] values = first ? [Portfolio, Fund, ValueDate, FundOrderFillsRow.PageSize]
            : [Portfolio, Fund, ValueDate, order, attempt, FundOrderFillsRow.PageSize];
        Console.WriteLine($"Plan for {name}:");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + sql, connection);
        PostgresValueBinding.AddParameters(command, values);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        return plan;
    }

    [GlobalSetup(Targets = new[] { nameof(FirstPage), nameof(DeepPage), nameof(SplitFirstPage), nameof(SplitDeepPage) })]
    public void Setup()
    {
        database = CreateContext();
        legacy = CreateLegacy();
    }

    [GlobalSetup(Target = nameof(InsertOrderRevision))]
    public async Task SetupWrites()
    {
        Setup();
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CHECKPOINT;").ConfigureAwait(false);
        initialWal = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    [Benchmark] public Task<FundOrderFillsRow[]> FirstPage() => legacy.ReadAsync(Portfolio, Fund, ValueDate, 0, Guid.Empty);
    [Benchmark] public Task<FundOrderFillsRow[]> DeepPage() => legacy.ReadAsync(Portfolio, Fund, ValueDate, DeepOrder, AttemptId(1));
    [Benchmark] public Task<FundOrderFillsRow[]> SplitFirstPage() => database.GetFundOrderFillsAsync(Portfolio, Fund, ValueDate, 0, Guid.Empty);
    [Benchmark] public Task<FundOrderFillsRow[]> SplitDeepPage() => database.GetFundOrderFillsAsync(Portfolio, Fund, ValueDate, DeepOrder, AttemptId(1));

    [Benchmark]
    public Task InsertOrderRevision()
    {
        var sequence = writes++;
        var order = Order(new(Portfolio, Fund, FirstWriteOrder + sequence / 3), WriteDate, sequence % 3 + 1);
        return database.UpsertTradeOrderAsync(order, AcceptedAt(WriteDate).AddMinutes(order.Revision - 1));
    }

    [GlobalCleanup(Target = nameof(InsertOrderRevision))]
    public async Task VerifyWrites()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand("""
            SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn),
                (SELECT count(*) FROM trade.trade_order WHERE valuedate = $2);
            """, connection);
        PostgresValueBinding.AddParameters(command, [initialWal, WriteDate]);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await reader.ReadAsync().ConfigureAwait(false);
        var wal = reader.GetDecimal(0);
        var persisted = reader.GetInt64(1);
        if (persisted != writes || writes == 0)
            throw new InvalidOperationException($"Expected {writes} inserted revisions, found {persisted}.");
        Console.WriteLine(FormattableString.Invariant($"Write verification: {writes} revisions, {wal} WAL bytes, {wal / writes:F2} WAL bytes/revision."));
    }

    static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }

    sealed class UnexpectedSequenceUse : ISequenceIdGenerator
    {
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken token = default) =>
            throw new InvalidOperationException("Order fill paging and accepted revision writes use supplied IDs.");

        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken token = default) =>
            throw new InvalidOperationException("Order fill paging and accepted revision writes use supplied IDs.");
    }
}
