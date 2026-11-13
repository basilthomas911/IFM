using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class ScheduledTaskHistoryBenchmarks
{
    const int Rows = 160_000;
    const int Ties = 128;
    const int Size = 32;
    const int DeepAnchor = 140_025;
    const string Env = "fixture";
    const string Host = "fixture-host";
    const string CurrentSql =
        """
        SELECT payload
        FROM scheduled_task.scheduled_task_run
        WHERE environment=$1 AND host_id=$2 AND schedule_id=$3 AND (intended_fire < $4 OR (intended_fire = $4 AND run_id > $5))
        ORDER BY intended_fire DESC,run_id ASC LIMIT $6;
        """;
    const string SplitSql = ScheduledTaskDbSql.HistoryAfter;
    static readonly ScheduledTaskId Schedule = new(new Guid("39256026-6450-49ae-a8ac-8e9ab7bcf48b"));
    static readonly DateTimeOffset Head = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);
    static readonly string Scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { environment = Env, host = Host, schedule = Schedule.Value })));
    static readonly string OutputTail = new('o', 256);
    static readonly string ErrorTail = new('e', 128);
    NpgsqlConnection db = null!;
    NpgsqlCommand current = null!;
    NpgsqlCommand split = null!;

    [Params(false, true)] public bool Deep { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        db = Connection("force_generic_plan");
        await db.OpenAsync().ConfigureAwait(false);
        var cursor = Decode(Token(Row(Deep ? DeepAnchor : Size - 1)));
        current = Command(db, CurrentSql, cursor);
        split = Command(db, SplitSql, cursor);
        await current.PrepareAsync().ConfigureAwait(false);
        await split.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<ScheduledTaskRunPage> Current() => ReadPage(current);
    [Benchmark] public Task<ScheduledTaskRunPage> SplitAtTimestamp() => ReadPage(split);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await current.DisposeAsync().ConfigureAwait(false);
        await split.DisposeAsync().ConfigureAwait(false);
        await db.DisposeAsync().ConfigureAwait(false);
    }

    static ScheduledTaskRun Row(int index) => new()
    {
        Id = new(Guid.Parse(FormattableString.Invariant($"01234567-89ab-cdef-0000-{index + 1:x12}"))),
        ScheduleId = Schedule, Environment = Env, HostId = Host, Revision = 1,
        IntendedFireTimeUtc = Head.AddMinutes(-(index / Ties)), Status = ScheduledTaskRunStatus.Succeeded,
        Stage = "Completed", Detail = FormattableString.Invariant($"Scheduled run {index}"),
        StandardOutputTail = OutputTail, StandardErrorTail = ErrorTail
    };

    static byte[] Token(ScheduledTaskRun run) =>
        JsonSerializer.SerializeToUtf8Bytes(new ScheduledTaskHistoryCursor(Scope, run.IntendedFireTimeUtc.UtcDateTime, run.Id.Value));

    static ScheduledTaskHistoryCursor Decode(byte[] bytes) =>
        JsonSerializer.Deserialize<ScheduledTaskHistoryCursor>(bytes) ?? throw new InvalidOperationException("Missing cursor.");

    static NpgsqlCommand Command(NpgsqlConnection connection, string sql, ScheduledTaskHistoryCursor cursor)
    {
        var command = new NpgsqlCommand(sql, connection);
        PostgresValueBinding.AddParameters(command, [Env, Host, Schedule.Value, cursor.Fire, cursor.Run, Size + 1]);
        return command;
    }

    static async Task<ScheduledTaskRunPage> ReadPage(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(reader);
        var runs = new List<ScheduledTaskRun>(Size + 1);
        while (await reader.ReadAsync().ConfigureAwait(false))
            runs.Add(ScheduledTaskDbContext.MapToSnapshot<ScheduledTaskRun>(record));
        return new()
        {
            Runs = runs.Take(Size).Select(StripTails).ToArray(),
            PagingState = runs.Count > Size ? Token(runs[Size - 1]) : null
        };
    }

    static ScheduledTaskRun StripTails(ScheduledTaskRun run) =>
        run with { StandardOutputTail = "", StandardErrorTail = "" };

    internal static async Task PrepareAsync()
    {
        await using var connection = Connection();
        var settings = new DbConnectionSettings().Add(ScheduledTaskDbContext.ConnectionName,
            new NpgsqlConnectionStringBuilder(System.Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")).ConnectionString,
            "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new ScheduledTaskSchemaDb(settings, logger).CreateAllAsync().ConfigureAwait(false);
        var store = new ScheduledTaskDbContext(settings, logger);
        await connection.OpenAsync().ConfigureAwait(false);
        using (var copy = connection.BeginBinaryImport("""
            COPY scheduled_task.scheduled_task_run(environment,host_id,schedule_id,intended_fire,run_id,revision,payload)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            for (var n = 0; n < Rows; n++)
                Write(copy, Row(n));
            for (var n = 0; n < 128; n++)
            {
                Write(copy, Row(n) with { HostId = "other-host" });
                Write(copy, Row(n) with { Environment = "other-env" });
                Write(copy, Row(n) with { ScheduleId = new(Guid.Empty) });
            }
            copy.Complete();
        }
        await Execute(connection, "VACUUM (ANALYZE) scheduled_task.scheduled_task_run").ConfigureAwait(false);
        var first = await store.GetRunHistoryAsync(Env, Host, Schedule, Size, null).ConfigureAwait(false);
        if (!first.Runs.SequenceEqual(Enumerable.Range(0, Size).Select(n => StripTails(Row(n)))) ||
            first.PagingState is null || !first.PagingState.SequenceEqual(Token(Row(Size - 1))))
            throw new InvalidOperationException("Production first-page rows or cursor changed.");

        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await Execute(connection, $"SET plan_cache_mode={mode};").ConfigureAwait(false);
            foreach (var (name, anchor, token) in new[]
            {
                ("early", Size - 1, first.PagingState),
                ("deep-inside-tie", DeepAnchor - 90, Token(Row(DeepAnchor - 90))),
                ("deep-crosses-time", DeepAnchor, Token(Row(DeepAnchor))),
                ("deep-end-of-tie", DeepAnchor + 6, Token(Row(DeepAnchor + 6))),
                ("terminal", Rows - 9, Token(Row(Rows - 9))),
                ("empty", Rows - 1, Token(Row(Rows - 1)))
            })
            {
                var cursor = Decode(token);
                await using var baselineCommand = Command(connection, CurrentSql, cursor);
                await baselineCommand.PrepareAsync().ConfigureAwait(false);
                var baseline = await ReadPage(baselineCommand).ConfigureAwait(false);
                var applied = await store.GetRunHistoryAsync(Env, Host, Schedule, Size, token).ConfigureAwait(false);
                await using var candidateCommand = Command(connection, SplitSql, cursor);
                await candidateCommand.PrepareAsync().ConfigureAwait(false);
                var candidate = await ReadPage(candidateCommand).ConfigureAwait(false);
                var expected = Enumerable.Range(anchor + 1, Math.Min(Size, Rows - anchor - 1)).Select(n => StripTails(Row(n)));
                if (!candidate.Runs.SequenceEqual(expected) || !baseline.Runs.SequenceEqual(candidate.Runs)
                    || !applied.Runs.SequenceEqual(baseline.Runs)
                    || !(baseline.PagingState ?? []).SequenceEqual(candidate.PagingState ?? [])
                    || !(baseline.PagingState ?? []).SequenceEqual(applied.PagingState ?? []))
                    throw new InvalidOperationException($"Results or continuation differed for {mode} {name}.");
                Console.WriteLine($"ORDERED,{mode},{name},{candidate.Runs.Length},next={candidate.PagingState is not null}");
            }
            await Execute(connection, $"""
                PREPARE scheduled_current(text,text,uuid,timestamptz,uuid,int) AS {CurrentSql}
                PREPARE scheduled_split(text,text,uuid,timestamptz,uuid,int) AS {SplitSql}
                """).ConfigureAwait(false);
            foreach (var (label, anchor) in new[] { ("early", Size - 1), ("deep", DeepAnchor) })
            {
                var cursor = Decode(Token(Row(anchor)));
                var args = FormattableString.Invariant($"'{Env}','{Host}','{Schedule.Value}','{cursor.Fire:O}','{cursor.Run}',{Size + 1}");
                await Explain(connection, $"{mode},{label},current", $"scheduled_current({args})").ConfigureAwait(false);
                await Explain(connection, $"{mode},{label},applied", $"scheduled_split({args})", verifySeek: label == "deep").ConfigureAwait(false);
            }
            await Execute(connection, "DEALLOCATE scheduled_current; DEALLOCATE scheduled_split;").ConfigureAwait(false);
        }
    }

    static void Write(NpgsqlBinaryImporter copy, ScheduledTaskRun row)
    {
        copy.StartRow();
        copy.Write(row.Environment);
        copy.Write(row.HostId);
        copy.Write(row.ScheduleId.Value);
        copy.Write(row.IntendedFireTimeUtc.UtcDateTime, NpgsqlDbType.TimestampTz);
        copy.Write(row.Id.Value);
        copy.Write(row.Revision);
        copy.Write(MessagePackSerializer.Serialize(row));
    }

    static async Task Explain(NpgsqlConnection connection, string label, string execution, bool verifySeek = false)
    {
        Console.WriteLine($"PLAN,{label}");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) EXECUTE " + execution, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        Console.WriteLine(string.Join(Environment.NewLine, lines));
        if (!verifySeek)
            return;
        var conditions = lines.Where(line => line.Contains("Index Cond:", StringComparison.Ordinal)).ToArray();
        if (conditions.Length != 2 ||
            !conditions.Any(line => line.Contains("intended_fire =", StringComparison.Ordinal)
                && line.Contains("run_id >", StringComparison.Ordinal)) ||
            !conditions.Any(line => line.Contains("intended_fire <", StringComparison.Ordinal)) ||
            lines.Any(line => line.Contains("Rows Removed by Filter:", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Expected both cursor ranges in deep-page index conditions for {label}.");
        Console.WriteLine($"VERIFIED-PLAN,{label},both cursor ranges are index conditions");
    }

    static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
