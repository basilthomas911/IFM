using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.SystemAdminDb;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.Benchmarks.RecoveryLogIndexBenchmarks;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class SystemAdminOperationPagingBenchmarks
{
    const string CurrentSql =
        """
        SELECT operation_id, backup_set_id, protection_set_id, source, operation_kind, phase, outcome,
        progress_percent, state_revision, created_utc, completed_utc, safe_diagnostic_reference,
        restore_point_id, restore_class, fresh_target_profile, validation_revision, cutover_state,
        backup_lineage_json,
        COALESCE((SELECT engine FROM system_admin.database_recovery_run_stats stats
                  WHERE stats.operation_id = database_recovery_operation.operation_id ORDER BY statistics_revision DESC LIMIT 1), 0) AS engine
        FROM system_admin.database_recovery_operation
        WHERE ($1 = 0 OR source = $1)
          AND ($2 IS NULL OR protection_set_id = $2)
          AND ($3 IS NULL OR created_utc >= $3)
          AND ($4 IS NULL OR created_utc <= $4)
          AND ($5 IS NULL OR (created_utc, operation_id) <
              (SELECT created_utc, operation_id FROM system_admin.database_recovery_operation WHERE operation_id = $5))
        ORDER BY created_utc DESC, operation_id DESC
        LIMIT $6;
        """;
    const string Table = "system_admin.database_recovery_operation";
    const string GlobalIndex = "ix_database_recovery_operation_global_page";
    const string ScopedIndex = "ix_database_recovery_operation_scoped_page";
    const int History = 192_000;
    const int Limit = 32;
    const string Target = "set-0";
    static readonly DateTime Start = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    static readonly Guid Deep = Id(48_025);
    NpgsqlConnection connection = null!;
    NpgsqlCommand current = null!;
    NpgsqlCommand split = null!;

    [Params("GlobalFirst", "GlobalDeep", "ScopedFirst", "ScopedDeep")]
    public string Page { get; set; } = null!;

    static Guid Id(int value) => Guid.Parse(FormattableString.Invariant($"12345678-0000-0000-0000-{value:000000000000}"));
    static DateTime At(int value) => Start.AddSeconds((value - 1) / 48);
    static short Source(int value) => (short)(1 + (value - 1) % 2);
    static string Protection(int value) => FormattableString.Invariant($"set-{((value - 1) / 2) % 12}");

    static string SplitSql(short source, string? protection, Guid? cursor) =>
        SystemAdminDbSql.ListOperations(source != 0, protection is not null, cursor.HasValue);

    static (short Source, string? Protection, Guid? Cursor) Filters(string page) =>
        (page.StartsWith("Scoped", StringComparison.Ordinal) ? (short)1 : (short)0,
         page.StartsWith("Scoped", StringComparison.Ordinal) ? Target : null,
         page.EndsWith("Deep", StringComparison.Ordinal) ? Deep : null);

    static NpgsqlCommand Command(NpgsqlConnection db, bool explicitFilters, short source, string? protection, Guid? cursor,
        DateTime? from = null, DateTime? to = null)
    {
        var command = new NpgsqlCommand(explicitFilters ? SplitSql(source, protection, cursor) : CurrentSql, db);
        PostgresValueBinding.AddParameters(command, Values(source, Text(protection), Timestamp(from), Timestamp(to), Uuid(cursor), Limit));
        command.Parameters[2].NpgsqlDbType = NpgsqlDbType.TimestampTz;
        command.Parameters[3].NpgsqlDbType = NpgsqlDbType.TimestampTz;
        return command;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        connection = Connection("force_generic_plan");
        await connection.OpenAsync().ConfigureAwait(false);
        var (source, protection, cursor) = Filters(Page);
        current = Command(connection, false, source, protection, cursor);
        split = Command(connection, true, source, protection, cursor);
        await current.PrepareAsync().ConfigureAwait(false);
        await split.PrepareAsync().ConfigureAwait(false);
    }

    [Benchmark(Baseline = true)] public Task<OperationPage> Current() => Read(current);
    [Benchmark] public Task<OperationPage> Split() => Read(split);

    static async Task<OperationPage> Read(NpgsqlCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(reader);
        var rows = new List<OperationRow>(Limit);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(ToRow(SystemAdminDbContext.MapToOperation(record)));
        }
        return new(rows);
    }

    static OperationRow ToRow(DatabaseBackupOperationReadModel item) =>
        new(item.OperationId.Value, item.CreatedUtc.UtcDateTime, (short)item.Source, item.ProtectionSetId.Value,
            (short)item.Engine, item.ProgressPercent, item.StateRevision, item.SafeDiagnosticReference);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await current.DisposeAsync().ConfigureAwait(false);
        await split.DisposeAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    internal static async Task PrepareAsync(bool indexed, bool verifyApplied = false)
    {
        var settings = new DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection,
            new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")).ConnectionString,
            "System.Data.Postgres");
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        var logger = NullLogger<DbProvider>.Instance;
        var schema = new SystemAdminSchemaDb(settings, logger);
        await schema.CreateAsync(["system_admin", "database_recovery_operation", "database_recovery_run_stats"]).ConfigureAwait(false);
        if (verifyApplied)
        {
            await VerifyIndexes(db).ConfigureAwait(false);
            await using var empty = Command(db, true, 0, null, null);
            if ((await Read(empty).ConfigureAwait(false)).Rows.Count != 0)
                throw new InvalidOperationException("Fresh schema returned operations.");
            Console.WriteLine("Verified fresh schema, both indexes, and empty first page.");
        }
        await Execute(db, $"DROP INDEX system_admin.{GlobalIndex}, system_admin.{ScopedIndex};").ConfigureAwait(false);
        await Execute(db, $"""
            INSERT INTO {Table}(operation_id,protection_set_id,source,operation_kind,phase,outcome,
                progress_percent,state_revision,created_utc,safe_diagnostic_reference,last_event_id,last_source_event_id)
            SELECT ('12345678-0000-0000-0000-'||lpad(n::text,12,'0'))::uuid,
                'set-'||((n-1)/2)%12,1+(n-1)%2,1,1,0,n%101,n,
                timestamptz '2026-10-13 12:00Z'+((n-1)/48)*interval '1 second','run-'||n,n,md5(n::text)::uuid
            FROM generate_series(1,{History}) n;
            INSERT INTO system_admin.database_recovery_run_stats(operation_id,source,phase,engine,statistics_revision,
                policy_revision,last_event_id,last_source_event_id)
            SELECT operation_id,source,1,revision,revision,1,last_event_id,last_source_event_id
            FROM {Table} CROSS JOIN generate_series(1,2) revision WHERE last_event_id%3=0;
            """).ConfigureAwait(false);
        if (indexed)
        {
            var fingerprint = verifyApplied ? await Fingerprint(db).ConfigureAwait(false) : null;
            await schema.CreateAsync(["database_recovery_operation"]).ConfigureAwait(false);
            if (verifyApplied)
            {
                var indexIds = await VerifyIndexes(db).ConfigureAwait(false);
                await schema.CreateAsync(["database_recovery_operation"]).ConfigureAwait(false);
                if (indexIds != await VerifyIndexes(db).ConfigureAwait(false) || fingerprint != await Fingerprint(db).ConfigureAwait(false))
                    throw new InvalidOperationException("The upgrade or reapplication changed operations or index identities.");
                Console.WriteLine($"Verified populated upgrade and reapplication. Operation fingerprint {fingerprint}, index OIDs {indexIds}.");
            }
        }
        await Execute(db, $"VACUUM (ANALYZE) {Table}").ConfigureAwait(false);
        await Execute(db, "VACUUM (ANALYZE) system_admin.database_recovery_run_stats").ConfigureAwait(false);

        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            await Execute(db, $"SET plan_cache_mode={mode}").ConfigureAwait(false);
            var context = verifyApplied ? new SystemAdminDbContext(new DbConnectionSettings().Add(
                SystemAdminDbContext.SystemAdminDbConnection,
                new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"))
                    { Options = "-c plan_cache_mode=" + mode }.ConnectionString, "System.Data.Postgres"), logger) : null;
            foreach (var page in new[] { "GlobalFirst", "GlobalDeep", "ScopedFirst", "ScopedDeep" })
            {
                var (source, protection, cursor) = Filters(page);
                await Explain(db, mode, page, false, source, protection, cursor).ConfigureAwait(false);
                await Explain(db, mode, page, true, source, protection, cursor, verifyApplied).ConfigureAwait(false);
                var result = await Verify(db, page, source, protection, cursor, context: context).ConfigureAwait(false);
                await Verify(db, page + "-replay", source, protection, result.Rows[^1].Id, context: context).ConfigureAwait(false);
            }
            await Verify(db, "source-only", 2, null, Deep, context: context).ConfigureAwait(false);
            await Verify(db, "protection-only", 0, Target, Deep, context: context).ConfigureAwait(false);
            await Verify(db, "other-scope-anchor", 2, Target, Deep, context: context).ConfigureAwait(false);
            await Verify(db, "missing-anchor", 0, null, Id(History + 1), context: context).ConfigureAwait(false);
            await Verify(db, "terminal", 0, null, Id(1), context: context).ConfigureAwait(false);
            await Verify(db, "inclusive-date-bounds", 1, Target, null, At(48_001), At(48_097), context).ConfigureAwait(false);
        }
    }

    static async Task<OperationPage> Verify(NpgsqlConnection db, string label, short source, string? protection,
        Guid? cursor, DateTime? from = null, DateTime? to = null, SystemAdminDbContext? context = null)
    {
        await using var before = Command(db, false, source, protection, cursor, from, to);
        await using var after = Command(db, true, source, protection, cursor, from, to);
        await before.PrepareAsync().ConfigureAwait(false);
        await after.PrepareAsync().ConfigureAwait(false);
        var baseline = await Read(before).ConfigureAwait(false);
        var candidate = await Read(after).ConfigureAwait(false);
        var start = cursor is null ? History : int.Parse(cursor.Value.ToString("D").AsSpan(24),
            System.Globalization.CultureInfo.InvariantCulture) - 1;
        var expected = start >= History && cursor.HasValue ? [] : Enumerable.Range(1, start).Reverse()
            .Where(n => (source == 0 || source == Source(n)) && (protection is null || protection == Protection(n))
                && (from is null || At(n) >= from) && (to is null || At(n) <= to))
            .Take(Limit).Select(n => new OperationRow(Id(n), At(n), Source(n), Protection(n),
                (short)(n % 3 == 0 ? 2 : 0), n % 101, n, FormattableString.Invariant($"run-{n}"))).ToArray();
        if (!baseline.Rows.SequenceEqual(candidate.Rows) || !candidate.Rows.SequenceEqual(expected))
            throw new InvalidOperationException($"Page or cursor differed for {label}.");
        if (context is not null)
        {
            var applied = await context.ListBackupOperationsAsync(new ListDatabaseBackupOperationsQuery
            {
                Source = (BackupSource)source,
                ProtectionSetId = protection is null ? null : new DatabaseProtectionSetId(protection),
                ContinuationIdentity = cursor?.ToString() ?? "",
                FromUtc = from,
                ToUtc = to,
                PageSize = Limit
            }, default).ConfigureAwait(false);
            if (!applied.Select(ToRow).SequenceEqual(expected))
                throw new InvalidOperationException($"Production page differed for {label}.");
        }
        Console.WriteLine($"ORDERED,{label},{candidate.Rows.Count},{candidate.Rows.FirstOrDefault()?.Id},{candidate.Rows.LastOrDefault()?.Id}");
        return baseline;
    }

    static async Task Explain(NpgsqlConnection db, string mode, string page, bool split, short source, string? protection,
        Guid? cursor, bool verifyPlan = false)
    {
        var sql = split ? SplitSql(source, protection, cursor) : CurrentSql;
        await Execute(db, $"PREPARE operation_page(smallint,text,timestamptz,timestamptz,uuid,int) AS {sql}").ConfigureAwait(false);
        var set = protection is null ? "NULL" : $"'{protection}'";
        var anchor = cursor is null ? "NULL" : $"'{cursor}'";
        Console.WriteLine($"PLAN,{mode},{page},{(split ? "split" : "current")}");
        var lines = new List<string>();
        await using (var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS) EXECUTE operation_page({source},{set},NULL,NULL,{anchor},{Limit})", db))
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false))
                lines.Add(reader.GetString(0));
        await Execute(db, "DEALLOCATE operation_page").ConfigureAwait(false);
        Console.WriteLine(string.Join(Environment.NewLine, lines));
        if (!verifyPlan)
            return;
        var index = source == 0 ? GlobalIndex : ScopedIndex;
        if (!lines.Any(line => line.Contains($"Index Scan using {index} ", StringComparison.Ordinal)) ||
            lines.Any(line => line.Contains("Sort Key:", StringComparison.Ordinal) &&
                line.Contains("created_utc", StringComparison.Ordinal)) ||
            (cursor.HasValue && !lines.Any(line => line.Contains("Index Cond:", StringComparison.Ordinal) &&
                line.Contains("ROW(created_utc, operation_id) <", StringComparison.Ordinal))))
            throw new InvalidOperationException($"Unexpected applied plan for {mode} {page}.");
        Console.WriteLine($"VERIFIED-PLAN,{mode},{page},{index}");
    }

    static async Task<string> VerifyIndexes(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT indexrelid::bigint,pg_get_indexdef(indexrelid)
            FROM pg_index
            WHERE indrelid='{Table}'::regclass AND indisvalid AND indisready
                AND indexrelid IN ('system_admin.{GlobalIndex}'::regclass,'system_admin.{ScopedIndex}'::regclass)
            ORDER BY indexrelid
            """, db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var ids = new List<long>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var definition = reader.GetString(1);
            var keys = definition.Contains(GlobalIndex, StringComparison.Ordinal)
                ? "(created_utc DESC, operation_id DESC)"
                : "(source, protection_set_id, created_utc DESC, operation_id DESC)";
            if (!definition.EndsWith($"USING btree {keys}", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected index definition: {definition}");
            ids.Add(reader.GetInt64(0));
        }
        return ids.Count == 2 ? string.Join(',', ids) : throw new InvalidOperationException("Missing paging index.");
    }

    static async Task<string> Fingerprint(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) || ':' || sum(hashtextextended(row_to_json(o)::text,0)::numeric) FROM {Table} o
            """, db);
        return (string)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var command = new NpgsqlCommand(sql, db) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public sealed record OperationRow(Guid Id, DateTime Created, short Source, string Protection, short Engine,
        int Progress, long Revision, string Diagnostic);
    public sealed record OperationPage(List<OperationRow> Rows);
}
