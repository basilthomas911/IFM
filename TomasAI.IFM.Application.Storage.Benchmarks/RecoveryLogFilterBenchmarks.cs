using BenchmarkDotNet.Attributes;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;
using Bind = TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class RecoveryLogFilterBenchmarks : IAsyncDisposable
{
    internal const string DatasetIndex = "ix_marketdata_recovery_dataset_date_order";
    internal const string CombinedSql = """
        SELECT payload FROM market_data_service.marketdata_recovery_log
        WHERE value_date=$1 AND ($2::text IS NULL OR dataset=$2)
          AND ($3::uuid IS NULL OR operation_id=$3)
        ORDER BY operation_id,sequence LIMIT $4;
        """;
    NpgsqlConnection? connection;

    [Params("Broad", "Dataset", "Operation", "Both")]
    public string Scope { get; set; } = null!;

    [Params("force_custom_plan", "force_generic_plan")]
    public string PlanMode { get; set; } = null!;

    NpgsqlCommand combined = null!;
    NpgsqlCommand split = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        await OpenAsync(PlanMode).ConfigureAwait(false);
        var (dataset, operation) = Filters(Scope);
        combined = Command(Date, dataset, operation, Limit, useSplit: false);
        split = Command(Date, dataset, operation, Limit, useSplit: true);
        await combined.PrepareAsync().ConfigureAwait(false);
        await split.PrepareAsync().ConfigureAwait(false);
    }

    internal async Task OpenAsync(string planMode)
    {
        connection = Connection(planMode);
        await connection.OpenAsync().ConfigureAwait(false);
    }

    static (string? Dataset, Guid? Operation) Filters(string scope) => scope switch
    {
        "Broad" => (null, null),
        "Dataset" => (Dataset, null),
        "Operation" => (null, Operation),
        "Both" => (Dataset, Operation),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    NpgsqlCommand Command(DateOnly date, string? dataset, Guid? operation, int limit, bool useSplit)
    {
        var selection = new RecoveryLogReadParameter(date, dataset, operation, limit);
        var sql = useSplit ? selection.Statement : CombinedSql;
        var values = useSplit ? (object?[])selection.Bind() : Bind.Values(date, Bind.Text(dataset), Bind.Uuid(operation), limit);
        var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        PostgresValueBinding.AddParameters(command, values);
        return command;
    }

    internal async Task<IReadOnlyList<string>> ReadAsync(DateOnly date, string? dataset, Guid? operation, int limit)
    {
        await using var command = Command(date, dataset, operation, limit, useSplit: true);
        await command.PrepareAsync().ConfigureAwait(false);
        return await ReadAsync(command).ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<string>> ReadCombinedAsync(DateOnly date, string? dataset, Guid? operation, int limit)
    {
        await using var command = Command(date, dataset, operation, limit, useSplit: false);
        return await ReadAsync(command).ConfigureAwait(false);
    }

    static async Task<IReadOnlyList<string>> ReadAsync(NpgsqlCommand command)
    {
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            result.Add(MarketDataServiceDbContext.MapToRecoveryLogPayload(reader));
        return result;
    }

    [Benchmark(Baseline = true)] public Task<IReadOnlyList<string>> Combined() => ReadAsync(combined);
    [Benchmark] public Task<IReadOnlyList<string>> Split() => ReadAsync(split);

    [GlobalCleanup]
    public async ValueTask DisposeAsync()
    {
        if (combined is not null) await combined.DisposeAsync().ConfigureAwait(false);
        if (split is not null) await split.DisposeAsync().ConfigureAwait(false);
        if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task PrintPlansAsync(bool verifyApplied = false)
    {
        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await using var db = Connection(mode);
            await db.OpenAsync().ConfigureAwait(false);
            await using (var prepare = new NpgsqlCommand($"""
                PREPARE recovery_combined(date,text,uuid,integer) AS {CombinedSql}
                PREPARE recovery_broad(date,integer) AS {MarketDataServiceDbSql.ReadRecoveryLogByDate};
                PREPARE recovery_dataset(date,text,integer) AS {MarketDataServiceDbSql.ReadRecoveryLogByDataset};
                PREPARE recovery_operation(date,uuid,integer) AS {MarketDataServiceDbSql.ReadRecoveryLogByOperation};
                PREPARE recovery_both(date,text,uuid,integer) AS {MarketDataServiceDbSql.ReadRecoveryLogByDatasetAndOperation};
                """, db))
                await prepare.ExecuteNonQueryAsync().ConfigureAwait(false);

            var date = FormattableString.Invariant($"'{Date:yyyy-MM-dd}'");
            var cases = new (string Name, string Dataset, string Operation, string SplitArgs)[]
            {
                ("broad", "NULL", "NULL", $"{date},{Limit}"),
                ("dataset", $"'{Dataset}'", "NULL", $"{date},'{Dataset}',{Limit}"),
                ("operation", "NULL", $"'{Operation}'", $"{date},'{Operation}',{Limit}"),
                ("both", $"'{Dataset}'", $"'{Operation}'", $"{date},'{Dataset}','{Operation}',{Limit}")
            };
            foreach (var item in cases)
            {
                await ExplainAsync(db, $"{mode} {item.Name} combined",
                    $"recovery_combined({date},{item.Dataset},{item.Operation},{Limit})").ConfigureAwait(false);
                await ExplainAsync(db, $"{mode} {item.Name} split",
                    $"recovery_{item.Name}({item.SplitArgs})", verifyApplied ? item.Name : null).ConfigureAwait(false);
            }
            await using var deallocate = new NpgsqlCommand("DEALLOCATE ALL;", db);
            await deallocate.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static async Task ExplainAsync(NpgsqlConnection db, string label, string execution, string? verifyShape = null)
    {
        Console.WriteLine($"Plan for {label}:");
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) EXECUTE " + execution, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var lines = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            lines.Add(reader.GetString(0));
        var plan = string.Join(Environment.NewLine, lines);
        Console.WriteLine(plan);
        if (verifyShape is null)
            return;
        var valid = verifyShape switch
        {
            "broad" => plan.Contains("Index Scan using ix_marketdata_recovery_date_order ", StringComparison.Ordinal),
            "dataset" => plan.Contains($"Index Scan using {DatasetIndex} ", StringComparison.Ordinal)
                && lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal)
                    && line.Contains("dataset =", StringComparison.Ordinal) && line.Contains("value_date =", StringComparison.Ordinal)),
            _ => lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal)
                && line.Contains("operation_id =", StringComparison.Ordinal))
        };
        if (!valid || plan.Contains("Sort Key:", StringComparison.Ordinal))
            throw new InvalidOperationException($"The applied plan did not use the expected ordered seek for {label}.");
    }
}
