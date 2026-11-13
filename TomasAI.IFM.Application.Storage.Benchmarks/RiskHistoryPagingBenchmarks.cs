using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class RiskHistoryPagingBenchmarks
{
    const int Groups = 1_250;
    const int Ties = 128;
    const int Size = 32;
    const int DeepOffset = 139_935;
    const string CurrentSql =
        """
        SELECT payload,evaluated_at_utc,invocation_id
        FROM trade.risk_management_history
        WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3 AND (evaluated_at_utc<$4 OR (evaluated_at_utc=$4
            AND invocation_id>$5))
        ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $6;
        """;
    const string BoundedSql = TradeDbSql.ReadRiskHistoryAfter;
    static readonly DateOnly Day = new(2026, 10, 13);
    static readonly DateTime Head = new(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc);
    static readonly Guid Workflow = new("3db31873-7f7a-4439-8a5e-654caac7594d");
    static readonly string Scope = FormattableString.Invariant($"risk:1:1:{Day.DayNumber}:{Size}");

    [Params(false, true)] public bool Deep { get; set; }

    static string ConnectionString()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    static RiskHistoryRow Row(int index, int fund = 1, DateOnly? date = null)
    {
        var invocation = Guid.ParseExact(FormattableString.Invariant($"01234567-89ab-cdef-0000-{index + 1:x12}"), "D");
        return new RiskHistoryRow(1, fund, date ?? Day, Head.AddSeconds(-(index / Ties)), Workflow,
            invocation, 1, "Approved", 2, $"Fixture risk outcome {index}", "Daily", 123 + index, "LongFuture");
    }

    static byte[] Token(RiskHistoryRow row) =>
        JsonSerializer.SerializeToUtf8Bytes(new RiskReadCursor(Scope, row.EvaluatedAtUtc, row.InvocationId));

    static RiskReadCursor Decode(byte[] token) => JsonSerializer.Deserialize<RiskReadCursor>(token)
        ?? throw new InvalidOperationException("Missing cursor.");

    static object?[] Parameters(RiskReadCursor cursor) => [1, 1, Day, cursor.AtUtc, cursor.Invocation, Size + 1];

    internal static async Task PrepareAsync()
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        await Execute(db, TradeSchemaDb.CreateStatement).ConfigureAwait(false);
        using (var copy = db.BeginBinaryImport("""
            COPY trade.risk_management_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            for (var i = 0; i < Groups * Ties; i++)
                Write(copy, Row(i));
            for (var i = 0; i < 256; i++)
            {
                Write(copy, Row(i, fund: 2));
                Write(copy, Row(i, date: Day.AddDays(1)));
            }
            copy.Complete();
        }
        await Execute(db, "VACUUM (ANALYZE) trade.risk_management_history").ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            ConnectionString(), "System.Data.Postgres");
        var transactions = new PostgresEventTransaction(settings);
        var first = await transactions.ExecuteOnceAsync((tx, ct) =>
            tx.RiskReadHistoryAsync(1, 1, Day, Size, null, ct)).ConfigureAwait(false);
        if (!first.Items.SequenceEqual(Enumerable.Range(0, Size).Select(i => Row(i))) ||
            first.PagingState is null || !first.PagingState.SequenceEqual(Token(Row(Size - 1))))
            throw new InvalidOperationException("Unexpected initial page or cursor from the production reader.");

        foreach (var (label, anchor, token) in new[]
        {
            ("first-continuation", Size - 1, first.PagingState),
            ("deep-inside-tie", DeepOffset, Token(Row(DeepOffset))),
            ("deep-crosses-timestamp", DeepOffset + 90, Token(Row(DeepOffset + 90))),
            ("deep-end-of-tie", DeepOffset + 96, Token(Row(DeepOffset + 96))),
            ("terminal", Groups * Ties - 9, Token(Row(Groups * Ties - 9))),
            ("empty", Groups * Ties - 1, Token(Row(Groups * Ties - 1)))
        })
        {
            var baseline = await Read(db, CurrentSql, Decode(token)).ConfigureAwait(false);
            var candidate = await Read(db, BoundedSql, Decode(token)).ConfigureAwait(false);
            var applied = await transactions.ExecuteOnceAsync((tx, ct) =>
                tx.RiskReadHistoryAsync(1, 1, Day, Size, token, ct)).ConfigureAwait(false);
            var expected = Enumerable.Range(anchor + 1, Math.Min(Size + 1, Groups * Ties - anchor - 1)).Select(i => Row(i)).ToArray();
            if (!candidate.Rows.SequenceEqual(expected) || !baseline.Rows.SequenceEqual(candidate.Rows)
                || !applied.Items.SequenceEqual(baseline.Rows.Take(Size))
                || !(baseline.Token ?? []).SequenceEqual(candidate.Token ?? [])
                || !(baseline.Token ?? []).SequenceEqual(applied.PagingState ?? []))
                throw new InvalidOperationException($"Page or continuation changed for {label}.");
            Console.WriteLine($"ORDERED,{label},rows={applied.Items.Length},lookahead={candidate.Rows.Length},next={applied.PagingState is not null},legacy token replayed");
        }

        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await Execute(db, $"SET plan_cache_mode={mode};").ConfigureAwait(false);
            await Execute(db, $"""
                PREPARE risk_current(int,int,date,timestamptz,uuid,int) AS {CurrentSql}
                PREPARE risk_bounded(int,int,date,timestamptz,uuid,int) AS {BoundedSql}
                """).ConfigureAwait(false);
            foreach (var (label, offset) in new[] { ("early", Size - 1), ("deep", DeepOffset + 90) })
            {
                var row = Row(offset);
                var args = FormattableString.Invariant($"1,1,'{Day:yyyy-MM-dd}','{row.EvaluatedAtUtc:O}','{row.InvocationId}',{Size + 1}");
                await Explain(db, $"{mode},{label},current", $"risk_current({args})").ConfigureAwait(false);
                await Explain(db, $"{mode},{label},applied", $"risk_bounded({args})", verifyDeepSeek: label == "deep").ConfigureAwait(false);
            }
            await Execute(db, "DEALLOCATE risk_current; DEALLOCATE risk_bounded;").ConfigureAwait(false);
        }
    }

    static void Write(NpgsqlBinaryImporter copy, RiskHistoryRow row)
    {
        copy.StartRow();
        copy.Write(row.PortfolioId);
        copy.Write(row.FundId);
        copy.Write(row.ValueDate);
        copy.Write(row.EvaluatedAtUtc, NpgsqlDbType.TimestampTz);
        copy.Write(row.InvocationId);
        copy.Write(row.Revision);
        copy.Write(MessagePackBinarySerializer.Shared.Serialize(row));
    }

    [Benchmark(Baseline = true)] public Task<(RiskHistoryRow[] Rows, byte[]? Token)> Current() => Run(CurrentSql);
    [Benchmark] public Task<(RiskHistoryRow[] Rows, byte[]? Token)> Bounded() => Run(BoundedSql);

    async Task<(RiskHistoryRow[] Rows, byte[]? Token)> Run(string sql)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        return await Read(db, sql, Decode(Token(Row(Deep ? DeepOffset + 90 : Size - 1)))).ConfigureAwait(false);
    }

    static async Task<(RiskHistoryRow[] Rows, byte[]? Token)> Read(NpgsqlConnection db, string sql, RiskReadCursor cursor)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, Parameters(cursor));
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var rows = new List<RiskHistoryRow>(Size + 1);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var (item, at, invocation) = TradeDbContext.MapToRiskReadHistoryRow3(reader);
            if (item.EvaluatedAtUtc != at || item.InvocationId != invocation)
                throw new InvalidOperationException("Payload identity differed from its key.");
            rows.Add(item);
        }
        return (rows.ToArray(), rows.Count > Size ? Token(rows[Size - 1]) : null);
    }

    static async Task Explain(NpgsqlConnection db, string label, string execution, bool verifyDeepSeek = false)
    {
        Console.WriteLine($"PLAN,{label}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) EXECUTE " + execution, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        Console.WriteLine(string.Join(Environment.NewLine, lines));
        if (!verifyDeepSeek)
            return;
        var conditions = lines.Where(line => line.Contains("Index Cond:", StringComparison.Ordinal)).ToArray();
        if (conditions.Length != 2 ||
            !conditions.Any(line => line.Contains("evaluated_at_utc =", StringComparison.Ordinal)
                && line.Contains("invocation_id >", StringComparison.Ordinal)) ||
            !conditions.Any(line => line.Contains("evaluated_at_utc <", StringComparison.Ordinal)) ||
            lines.Any(line => line.Contains("Rows Removed by Filter:", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Expected both cursor bounds in deep-page index conditions for {label}.");
        Console.WriteLine($"VERIFIED-PLAN,{label},both cursor ranges are index conditions");
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
