using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Compares the date and ordering index through the recovery-log context.</summary>
[MemoryDiagnoser]
public class RecoveryLogIndexBenchmarks
{
    internal const string Table = "market_data_service.marketdata_recovery_log";
    const string Index = "ix_marketdata_recovery_date_order";
    internal const string Dataset = "GLBX.MDP3";
    const string MissingDataset = "MISSING";
    const int Days = 12;
    const int OperationsPerDay = 2048;
    const int Sequences = 12;
    const int TargetDay = 6;
    const int TargetOperation = 1024 * Days + TargetDay;
    internal const int Limit = 256;
    const int EvidenceLength = 256;
    static readonly DateOnly Start = new(2026, 10, 26);
    internal static readonly DateOnly Date = Start.AddDays(TargetDay);
    static readonly DateOnly WriteDate = Start.AddDays(Days);
    internal static readonly Guid Operation = OperationId(TargetOperation);
    static readonly string Evidence = new('x', EvidenceLength);
    static readonly DateTime WriteTime = WriteDate.ToDateTime(new(15, 0), DateTimeKind.Utc);
    MarketDataServiceDbContext store = null!;
    readonly Random writeIds = new(912);
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

    internal static IDbConnectionSettings Settings(string? planMode = null) => new DbConnectionSettings().Add(
        MarketDataServiceDbContext.MarketDataServiceDbConnection, ConnectionString(planMode), "System.Data.Postgres");

    static string ConnectionString(string? planMode) => planMode is null ? ConnectionString()
        : new NpgsqlConnectionStringBuilder(ConnectionString()) { Options = "-c plan_cache_mode=" + planMode }.ConnectionString;

    internal static NpgsqlConnection Connection(string? planMode = null) =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString(planMode));

    internal static async Task PrepareAsync(bool withIndex, bool verifyMigration = false, bool splitExperiment = false, bool withDatasetIndex = false)
    {
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        if (await ScalarAsync(connection, """
            SELECT current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on'
                AND current_setting('full_page_writes') = 'on';
            """).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Benchmark requires full WAL durability.");
        if (await ScalarAsync(connection, $"SELECT to_regclass('{Table}') IS NULL;").ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Run each arm on a fresh disposable database.");

        var settings = Settings();
        var logger = NullLogger<DbProvider>.Instance;
        var schema = new MarketDataServiceSchemaDb(settings, logger);
        await schema.CreateAsync(["market_data_service", "marketdata_recovery_log"]).ConfigureAwait(false);
        var verifySplitMigration = verifyMigration && withDatasetIndex;
        if (verifyMigration)
            Console.WriteLine($"Fresh schema index verified. OID: {await ReadIndexOidAsync(connection).ConfigureAwait(false)}.");
        if (verifySplitMigration)
            Console.WriteLine($"Fresh dataset index verified. OID: {await ReadDatasetIndexOidAsync(connection).ConfigureAwait(false)}.");

        // Load the disposable fixture with the pre-index layout for the upgrade or baseline run.
        await ExecuteAsync(connection, $"DROP INDEX market_data_service.{RecoveryLogFilterBenchmarks.DatasetIndex};").ConfigureAwait(false);
        if (!verifySplitMigration)
            await ExecuteAsync(connection, $"DROP INDEX market_data_service.{Index};").ConfigureAwait(false);
        await ExecuteAsync(connection, FormattableString.Invariant($$"""
            INSERT INTO {{Table}}
              (record_id, operation_id, sequence, value_date, role, dataset, kind,
               occurred_at_utc, received_at_utc, payload_digest, payload, source_event_id)
            SELECT (lpad(to_hex(id * {{Sequences}} + seq), 8, '0') || '-0000-4000-8000-000000000001')::uuid,
                   (lpad(to_hex(id), 8, '0') || '-0000-4000-8000-000000000000')::uuid,
                   seq, date '{{Start:yyyy-MM-dd}}' + day, (ARRAY['Options','Futures','Index'])[op % 3 + 1],
                   CASE WHEN op % 8 = 0 THEN '{{Dataset}}' ELSE 'BENCH-' || (op % 8) END,
                   CASE WHEN seq = 1 THEN 'Started' ELSE 'Progress' END,
                   timestamptz '{{Start:yyyy-MM-dd}} 14:00:00+00' + day * interval '1 day' + seq * interval '1 second',
                   timestamptz '{{Start:yyyy-MM-dd}} 14:00:01+00' + day * interval '1 day' + seq * interval '1 second',
                   encode(sha256(convert_to(payload, 'UTF8')), 'hex'), payload, id * {{Sequences}} + seq
            FROM generate_series(0, {{Days - 1}}) AS days(day)
            CROSS JOIN generate_series(0, {{OperationsPerDay - 1}}) AS operations(op)
            CROSS JOIN generate_series(1, {{Sequences}}) AS sequences(seq)
            CROSS JOIN LATERAL (SELECT op * {{Days}} + day AS id) AS key
            CROSS JOIN LATERAL
              (SELECT format('{"operation":%s,"sequence":%s,"evidence":"%s"}', id, seq, repeat('x', {{EvidenceLength}})) AS payload) AS record
            ORDER BY day, op, seq;
            """)).ConfigureAwait(false);
        var context = new MarketDataServiceDbContext(settings, logger);
        string? before = null;
        if (verifyMigration)
        {
            var missingIndex = verifySplitMigration ? RecoveryLogFilterBenchmarks.DatasetIndex : Index;
            if (await ScalarAsync(connection, $"SELECT to_regclass('market_data_service.{missingIndex}') IS NULL;")
                .ConfigureAwait(false) is not true)
                throw new InvalidOperationException("The pre-index fixture still has the candidate index.");
            await ExecuteAsync(connection, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
            Console.WriteLine("Pre-migration reads and plans:");
            await using var legacy = new RecoveryLogFilterBenchmarks();
            await legacy.OpenAsync("force_custom_plan").ConfigureAwait(false);
            before = await VerifyReadsAsync(legacy.ReadCombinedAsync).ConfigureAwait(false);
            await PrintPlansAsync(connection).ConfigureAwait(false);
        }
        var existingDateIndex = verifySplitMigration ? await ReadIndexOidAsync(connection).ConfigureAwait(false) : 0;
        if (withIndex)
            await schema.CreateAsync(["marketdata_recovery_log"]).ConfigureAwait(false);
        if (withIndex && !withDatasetIndex)
            await ExecuteAsync(connection, $"DROP INDEX market_data_service.{RecoveryLogFilterBenchmarks.DatasetIndex};").ConfigureAwait(false);
        if (verifyMigration)
        {
            var oid = await ReadIndexOidAsync(connection).ConfigureAwait(false);
            var datasetOid = verifySplitMigration ? await ReadDatasetIndexOidAsync(connection).ConfigureAwait(false) : 0;
            await schema.CreateAsync(["marketdata_recovery_log"]).ConfigureAwait(false);
            if (!withDatasetIndex)
                await ExecuteAsync(connection, $"DROP INDEX market_data_service.{RecoveryLogFilterBenchmarks.DatasetIndex};").ConfigureAwait(false);
            var count = (long)(await ScalarAsync(connection, $"SELECT count(*) FROM {Table};").ConfigureAwait(false))!;
            if (oid != await ReadIndexOidAsync(connection).ConfigureAwait(false) || count != Days * OperationsPerDay * Sequences)
                throw new InvalidOperationException("Migration replay changed the index identity or fixture row count.");
            if (verifySplitMigration && (existingDateIndex != oid || datasetOid != await ReadDatasetIndexOidAsync(connection).ConfigureAwait(false)))
                throw new InvalidOperationException("The dataset migration changed an index identity on reapplication.");
            Console.WriteLine($"Populated upgrade and reapplication verified. Index OID: {oid}. Rows: {count}.");
            Console.WriteLine("Post-migration reads and plans:");
        }
        else
            await ExecuteAsync(connection, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);

        var after = await VerifyReadsAsync(context).ConfigureAwait(false);
        if (verifyMigration && before != after)
            throw new InvalidOperationException("The ordered payload fingerprint changed during migration.");
        var bytes = await ScalarAsync(connection,
            $"SELECT COALESCE(pg_relation_size(to_regclass('market_data_service.{Index}')),0);").ConfigureAwait(false);
        Console.WriteLine($"Candidate index bytes: {bytes}.");
        if (splitExperiment)
        {
            foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
            {
                await using var candidate = new RecoveryLogFilterBenchmarks();
                await candidate.OpenAsync(mode).ConfigureAwait(false);
                Console.WriteLine($"Prepared production statement result checks for {mode}:");
                var splitFingerprint = await VerifyReadsAsync(candidate.ReadAsync).ConfigureAwait(false);
                if (after != splitFingerprint)
                    throw new InvalidOperationException("The split changed the ordered payload fingerprint.");
            }
            await RecoveryLogFilterBenchmarks.PrintPlansAsync(verifySplitMigration).ConfigureAwait(false);
            var datasetBytes = await ScalarAsync(connection, $"SELECT COALESCE(pg_relation_size(to_regclass('market_data_service.{RecoveryLogFilterBenchmarks.DatasetIndex}')),0);").ConfigureAwait(false);
            Console.WriteLine($"Dataset index bytes: {datasetBytes}.");
        }
        else
            await PrintPlansAsync(connection, verifyMigration).ConfigureAwait(false);
        if (verifyMigration)
            Console.WriteLine("Migration, ordered payload equality, and scoped and broad plans verified.");
    }

    static async Task<long> ReadDatasetIndexOidAsync(NpgsqlConnection connection)
    {
        const string name = RecoveryLogFilterBenchmarks.DatasetIndex;
        var oid = await ScalarAsync(connection, $"""
            SELECT indexrelid::bigint FROM pg_index
            WHERE indexrelid = to_regclass('market_data_service.{name}')
                AND indisvalid AND indisready
                AND pg_get_indexdef(indexrelid) =
                    'CREATE INDEX {name} ON {Table} USING btree (dataset, value_date, operation_id, sequence)';
            """).ConfigureAwait(false);
        return oid is long value ? value : throw new InvalidOperationException("The dataset-leading index definition differs.");
    }

    static async Task<long> ReadIndexOidAsync(NpgsqlConnection connection)
    {
        var oid = await ScalarAsync(connection, $"""
            SELECT indexrelid::bigint FROM pg_index
            WHERE indexrelid = to_regclass('market_data_service.{Index}')
                AND indisvalid AND indisready
                AND pg_get_indexdef(indexrelid) =
                    'CREATE INDEX {Index} ON {Table} USING btree (value_date, operation_id, sequence)';
            """).ConfigureAwait(false);
        return oid is long value ? value : throw new InvalidOperationException("Recovery-log index is missing, invalid, or has different keys.");
    }

    static Task<string> VerifyReadsAsync(MarketDataServiceDbContext context) =>
        VerifyReadsAsync((date, dataset, operation, limit) => context.ReadRecoveryLogAsync(date, dataset, operation, limit));

    static async Task<string> VerifyReadsAsync(Func<DateOnly, string?, Guid?, int, Task<IReadOnlyList<string>>> read)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        async Task Verify(string label, DateOnly date, string? dataset, Guid? operation, int limit)
        {
            var day = date.DayNumber - Start.DayNumber;
            var expected = Enumerable.Range(0, OperationsPerDay)
                .Where(op => day >= 0 && day < Days && (dataset is null || dataset == DatasetName(op)))
                .Select(op => op * Days + day)
                .Where(id => operation is null || operation == OperationId(id))
                .SelectMany(id => Enumerable.Range(1, Sequences).Select(seq => Payload(id, seq)))
                .Take(limit);
            var actual = await read(date, dataset, operation, limit).ConfigureAwait(false);
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidOperationException($"Recovery log rows or order differed for {label}.");
            foreach (var payload in actual)
                hash.AppendData(Encoding.UTF8.GetBytes(payload + "\n"));
            Console.WriteLine($"Verified {label}: {actual.Count} payloads.");
        }

        await Verify("broad", Date, null, null, Limit).ConfigureAwait(false);
        await Verify("dataset only", Date, Dataset, null, Limit).ConfigureAwait(false);
        await Verify("operation only", Date, null, Operation, Limit).ConfigureAwait(false);
        await Verify("dataset and operation", Date, Dataset, Operation, Limit).ConfigureAwait(false);
        await Verify("small limit", Date, Dataset, Operation, 3).ConfigureAwait(false);
        await Verify("missing dataset", Date, MissingDataset, null, Limit).ConfigureAwait(false);
        await Verify("mismatched dataset", Date, "BENCH-1", Operation, Limit).ConfigureAwait(false);
        await Verify("operation on another date", Date.AddDays(1), Dataset, Operation, Limit).ConfigureAwait(false);
        await Verify("missing date", Start.AddDays(-1), null, null, Limit).ConfigureAwait(false);
        await Verify("empty dataset", Date, "", null, Limit).ConfigureAwait(false);
        await Verify("missing operation", Date, null, Guid.Empty, Limit).ConfigureAwait(false);
        await Verify("zero limit", Date, Dataset, Operation, 0).ConfigureAwait(false);
        var fingerprint = Convert.ToHexString(hash.GetHashAndReset());
        Console.WriteLine($"Ordered payload fingerprint: {fingerprint}.");
        return fingerprint;
    }

    static async Task PrintPlansAsync(NpgsqlConnection connection, bool verifyIndex = false)
    {
        await ExecuteAsync(connection, "PREPARE recovery_log_bench(date,text,uuid,integer) AS "
            + RecoveryLogFilterBenchmarks.CombinedSql).ConfigureAwait(false);
        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await ExecuteAsync(connection, $"SET plan_cache_mode = {mode};").ConfigureAwait(false);
            foreach (var (label, dataset, operation) in new (string, string?, Guid?)[]
            {
                ("broad", null, null), ("dataset only", Dataset, null),
                ("operation only", null, Operation), ("dataset and operation", Dataset, Operation)
            })
            {
                Console.WriteLine($"Plan for {mode} {label}:");
                var dateSql = FormattableString.Invariant($"'{Date:yyyy-MM-dd}'");
                var datasetSql = dataset is null ? "NULL" : $"'{dataset}'";
                var operationSql = operation is null ? "NULL" : $"'{operation}'";
                await using var command = new NpgsqlCommand(
                    $"EXPLAIN (ANALYZE, BUFFERS) EXECUTE recovery_log_bench({dateSql},{datasetSql},{operationSql},{Limit});",
                    connection);
                await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                var lines = new List<string>();
                while (await reader.ReadAsync().ConfigureAwait(false))
                    lines.Add(reader.GetString(0));
                var plan = string.Join(Environment.NewLine, lines);
                Console.WriteLine(plan);
                if (verifyIndex)
                {
                    var useDateIndex = operation is null || mode == "force_generic_plan";
                    var expectedCondition = useDateIndex ? "value_date =" : "operation_id =";
                    if (!lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal)
                            && line.Contains(expectedCondition, StringComparison.Ordinal))
                        || (useDateIndex && (!plan.Contains($"Index Scan using {Index}", StringComparison.Ordinal)
                            || plan.Contains("Sort Key:", StringComparison.Ordinal))))
                        throw new InvalidOperationException($"Unexpected recovery-log plan for {mode} {label}.");
                }
            }
        }
        await ExecuteAsync(connection, "DEALLOCATE recovery_log_bench; RESET plan_cache_mode;").ConfigureAwait(false);
    }

    static Guid OperationId(int ordinal) => Guid.Parse(FormattableString.Invariant($"{ordinal:x8}-0000-4000-8000-000000000000"));
    static string DatasetName(int operation) => operation % 8 == 0 ? Dataset : FormattableString.Invariant($"BENCH-{operation % 8}");
    static string Payload(int operation, int sequence) =>
        FormattableString.Invariant($$"""{"operation":{{operation}},"sequence":{{sequence}},"evidence":"{{Evidence}}"}""");

    [GlobalSetup(Targets = new[] { nameof(ReadBroad), nameof(ReadDataset), nameof(ReadOperation), nameof(ReadDatasetAndOperation) })]
    public void Setup() => store = new(Settings(), NullLogger<DbProvider>.Instance);

    [GlobalSetup(Target = nameof(AppendRecovery))]
    public async Task SetupWrites()
    {
        Setup();
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CHECKPOINT;").ConfigureAwait(false);
        initialWal = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    [Benchmark] public Task<IReadOnlyList<string>> ReadBroad() => store.ReadRecoveryLogAsync(Date, null, null, Limit);
    [Benchmark] public Task<IReadOnlyList<string>> ReadDataset() => store.ReadRecoveryLogAsync(Date, Dataset, null, Limit);
    [Benchmark] public Task<IReadOnlyList<string>> ReadOperation() => store.ReadRecoveryLogAsync(Date, null, Operation, Limit);
    [Benchmark] public Task<IReadOnlyList<string>> ReadDatasetAndOperation() => store.ReadRecoveryLogAsync(Date, Dataset, Operation, Limit);

    [Benchmark]
    public Task AppendRecovery()
    {
        Span<byte> ids = stackalloc byte[32];
        writeIds.NextBytes(ids);
        var ordinal = ++writes;
        var payload = Payload(ordinal, 1);
        var row = new RecoveryLogRow(new(ids[..16]), new(ids[16..]), 1, WriteDate, "Options", Dataset, "Started",
            new DateTimeOffset(WriteTime), WriteTime, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
            payload, ordinal);
        return store.AppendRecoveryLogAsync(row);
    }

    [GlobalCleanup(Target = nameof(AppendRecovery))]
    public async Task VerifyWrites()
    {
        await using var connection = Connection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn),
                (SELECT count(*) FROM {Table} WHERE value_date = $2);
            """, connection);
        PostgresValueBinding.AddParameters(command, [initialWal, WriteDate]);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await reader.ReadAsync().ConfigureAwait(false);
        var wal = reader.GetDecimal(0);
        var persisted = reader.GetInt64(1);
        if (persisted != writes || writes == 0)
            throw new InvalidOperationException($"Expected {writes} inserted recovery rows, found {persisted}.");
        Console.WriteLine(FormattableString.Invariant($"Write verification: {writes} rows, {wal} WAL bytes, {wal / writes:F2} WAL bytes/row."));
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
}
