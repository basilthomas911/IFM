using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class DuplicateIndexBenchmarks
{
    const int SeedRows = 65536;
    const int WriteRows = 64;
    static readonly DateOnly Date = new(2026, 11, 12);
    static readonly Guid Group = Guid.Parse("00000001-0000-4000-8000-000000000000");
    static readonly Guid Generation = Guid.Parse("00000002-0000-4000-8000-000000000000");

    // Inserts isolate index maintenance while preserving the real table, keys and payload columns.
    sealed record Workload(string Table, string Columns, string Values, string Read, object?[] ReadParameters);
    static readonly Dictionary<string, Workload> Workloads = new(StringComparer.Ordinal)
    {
        ["CountryCode"] = new("market_data.economic_calendar_country_code", "lookupid,countrycode",
            "grp, 'CC-' || lpad(slot::text,4,'0')", MarketDataDbSql.GetEconomicCalendarCountryCodes, [1]),
        ["OptionTick"] = new("market_data.futures_option_tick_price_data", "contractid,valuedate,tickid,ticktime,optionprice",
            "'OPT-' || grp,date '2026-11-12',slot,time '14:00',slot/10.0", MarketDataDbSql.GetLastFuturesOptionTickPriceData, ["OPT-1", Date]),
        ["VixContract"] = new("market_data.vix_futures_contract_index", "bucket,contractid",
            "grp,'VX-' || lpad(slot::text,4,'0')",
            "SELECT contractid FROM market_data.vix_futures_contract_index WHERE bucket=$1 ORDER BY contractid", [1]),
        ["MarketHoliday"] = new("market_data.market_holiday", "currencytype,holidaydate,description",
            "'C-' || grp,date '2026-11-12' + slot,'Holiday ' || slot",
            MarketDataDbSql.GetMarketHolidaysByDateRange, ["C-1", Date.AddDays(10), Date.AddDays(29)]),
        ["OptionExpiry"] = new("securities.option_contract_expiry_calendar",
            "symbol,generation,expirydate,providerroot,contractid,underlyingcontractid,optionfamily,definitionpayload,refreshedatutc",
            "'ES-' || grp,uuid '00000002-0000-4000-8000-000000000000',date '2026-11-12','ES','OPT-' || lpad(slot::text,4,'0'), 'ESZ6','FOP',decode(repeat('ab',128),'hex'),timestamptz '2026-11-12 14:00Z'",
            SecuritiesDbSql.GetCachedOptionContractDefinitions, ["ES-1", Generation, Date]),
        ["ExecutionFill"] = new("trade.order_execution_fill", "executionattemptid,executionfillid,filledatutc,contractid,payload",
            "group_uuid,(lpad(to_hex(slot),8,'0') || '-0000-4000-8000-000000000000')::uuid,timestamptz '2026-11-12 14:00Z' + slot * interval '1 second','ES',decode(repeat('ab',128),'hex')",
            "SELECT executionfillid,payload FROM trade.order_execution_fill WHERE executionattemptid=$1 ORDER BY filledatutc,executionfillid", [Group]),
        ["WorkflowTimeline"] = new("trade.intrinsic_time_strategy_workflow_timeline",
            "workflowid,eventid,workflowentityid,workflowrevision,stage,eventname,eventschemaversion,eventpayload,occurredatutc",
            "group_uuid,slot,'ES',slot,'Processing','Test',1,decode(repeat('ab',128),'hex'),timestamptz '2026-11-12 14:00Z'",
            TradeDbSql.NativeGetIntrinsicTimeStrategyWorkflowTimeline, [Group, 0L, 32]),
        ["TradeLimit"] = new("trade.trade_limit", "tradeid,tradetype,riskmargin,maxlosslimit",
            "grp,'TYPE-' || lpad(slot::text,4,'0'),slot,slot/10.0",
            "SELECT tradeid,tradetype,riskmargin,maxlosslimit FROM trade.trade_limit WHERE tradeid=$1 ORDER BY tradetype", [1]),
        ["TradeTypeLimit"] = new("trade.trade_type_limit", "tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit",
            "grp,'TYPE-' || lpad(slot::text,4,'0'),slot,slot/10.0,slot*2",
            TradeDbSql.ReadSpreadLimitsByTrade, [1])
    };

    [ParamsSource(nameof(Cases))]
    public string Case { get; set; } = null!;
    public static IEnumerable<string> Cases => Workloads.Keys;
    NpgsqlConnection connection = null!;
    int rowsWritten;
    string initialWal = null!;

    static string ConnectionString()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connect() => new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    internal static async Task PrepareAsync(bool remove, bool verifyMigration = false)
    {
        await using var db = Connect();
        await db.OpenAsync().ConfigureAwait(false);
        if (await Scalar(db, """
            SELECT to_regnamespace('market_data') IS NULL AND to_regnamespace('trade') IS NULL
                AND to_regnamespace('securities') IS NULL AND current_setting('fsync') = 'on'
                AND current_setting('synchronous_commit') = 'on' AND current_setting('full_page_writes') = 'on';
            """).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Use a fresh database with full WAL durability.");
        await ApplySchemasAsync().ConfigureAwait(false);
        var freshKeys = await VerifyRemovedIndexesAsync(db).ConfigureAwait(false);
        Console.WriteLine($"Fresh schema verified: {freshKeys.Length} primary keys and no duplicate indexes.");
        var beforeReads = new Dictionary<string, string>(StringComparer.Ordinal);
        var beforeRows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, workload) in Workloads)
        {
            await Execute(db, Insert(workload), 1, SeedRows).ConfigureAwait(false);
            await Execute(db, $"VACUUM (ANALYZE) {workload.Table}").ConfigureAwait(false);
            var index = workload.Table + "_ordered";
            var (schema, table) = (workload.Table.Split('.')[0], workload.Table.Split('.')[1]);
            var keys = (string)(await Scalar(db, """
                SELECT string_agg(pg_get_indexdef(indexrelid,n,true), ', ' ORDER BY n)
                FROM pg_index CROSS JOIN LATERAL generate_series(1,indnkeyatts) n
                WHERE indrelid=to_regclass($1) AND indisprimary
                """, workload.Table).ConfigureAwait(false))!;
            // Recreate the previous layout in the fixture. Production schema removes these indexes.
            await Execute(db, $"CREATE INDEX {table}_ordered ON {schema}.{table} ({keys})").ConfigureAwait(false);
            var ddl = (string)(await Scalar(db, """
                SELECT pg_get_indexdef(i.indexrelid)
                FROM pg_index i JOIN pg_index p ON p.indrelid=i.indrelid AND p.indisprimary
                JOIN pg_class a ON a.oid=i.indexrelid JOIN pg_class b ON b.oid=p.indexrelid
                WHERE i.indexrelid=to_regclass($1) AND i.indisvalid AND i.indisready AND p.indisvalid AND p.indisready
                    AND a.relam=b.relam AND i.indkey=p.indkey AND i.indclass=p.indclass
                    AND i.indcollation=p.indcollation AND i.indoption=p.indoption
                    AND i.indnkeyatts=p.indnkeyatts AND i.indnatts=p.indnatts
                    AND i.indpred IS NULL AND i.indexprs IS NULL AND NOT i.indisunique
                    AND NOT i.indisreplident AND NOT i.indisclustered
                    AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conindid=i.indexrelid)
                    AND NOT EXISTS (SELECT 1 FROM pg_depend WHERE refclassid='pg_class'::regclass AND refobjid=i.indexrelid)
                """, index).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unsafe candidate {index}."));
            var bytes = await Scalar(db, "SELECT pg_relation_size(to_regclass($1))", index).ConfigureAwait(false);
            Console.WriteLine($"Candidate {name}, {bytes} bytes. Recreate: {ddl};");
            beforeReads.Add(name, await Fingerprint(db, workload).ConfigureAwait(false));
            if (verifyMigration)
            {
                beforeRows.Add(name, await TableFingerprintAsync(db, workload).ConfigureAwait(false));
                await VerifyReadPlanAsync(db, "before migration", name, workload, $"{table}_ordered", requireIndex: false).ConfigureAwait(false);
            }
        }
        if (remove)
            await ApplySchemasAsync().ConfigureAwait(false);
        if (verifyMigration)
        {
            await VerifyUpgradeAsync("Populated upgrade").ConfigureAwait(false);
            await ApplySchemasAsync().ConfigureAwait(false);
            await VerifyUpgradeAsync("Reapplication").ConfigureAwait(false);
        }

        foreach (var (name, workload) in Workloads)
        {
            var before = beforeReads[name];
            if (before != await Fingerprint(db, workload).ConfigureAwait(false))
                throw new InvalidOperationException($"Read results changed for {name}.");
            var primary = workload.Table.Split('.')[1] + "_pkey";
            try
            {
                await Execute(db, Insert(workload), 1, 1).ConfigureAwait(false);
                throw new InvalidOperationException($"Duplicate key accepted for {name}.");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                && exception.ConstraintName == primary)
            {
                Console.WriteLine($"Primary key rejected duplicate for {name}. Read fingerprint: {before}.");
            }
            await VerifyReadPlanAsync(db, "final", name, workload, primary, verifyMigration).ConfigureAwait(false);
        }
        await Execute(db, "SELECT pg_stat_force_next_flush(); SELECT pg_stat_clear_snapshot();").ConfigureAwait(false);
        foreach (var workload in Workloads.Values)
        {
            await using var usage = Command(db, """
                SELECT indexrelname, idx_scan FROM pg_stat_user_indexes
                WHERE relid=to_regclass($1) ORDER BY indexrelname
                """, [workload.Table]);
            await using var reader = await usage.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                Console.WriteLine($"Fixture index usage {reader.GetString(0)}: {reader.GetInt64(1)} scans.");
        }

        async Task VerifyUpgradeAsync(string phase)
        {
            var currentKeys = await VerifyRemovedIndexesAsync(db).ConfigureAwait(false);
            if (!freshKeys.SequenceEqual(currentKeys))
                throw new InvalidOperationException($"{phase} changed primary-key index identities.");
            foreach (var (name, workload) in Workloads)
            {
                if (beforeRows[name] != await TableFingerprintAsync(db, workload).ConfigureAwait(false)
                    || beforeReads[name] != await Fingerprint(db, workload).ConfigureAwait(false))
                    throw new InvalidOperationException($"{phase} changed rows or read results for {name}.");
            }
            Console.WriteLine($"{phase} verified: all nine duplicates absent, primary-key identities stable, table and ordered read fingerprints match.");
        }
    }

    static async Task ApplySchemasAsync()
    {
        var connectionString = ConnectionString();
        var settings = new DbConnectionSettings()
            .Add(MarketDataDbContext.MarketDataDbConnection, connectionString, "System.Data.Postgres")
            .Add(SecuritiesDbContext.SecuritiesDbConnection, connectionString, "System.Data.Postgres")
            .Add(TradeDbContext.TradeDbConnection, connectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new MarketDataSchemaDb(settings, logger).CreateAsync(["market_data"]).ConfigureAwait(false);
        await new SecuritiesSchemaDb(settings, logger).CreateAsync(["securities"]).ConfigureAwait(false);
        await new TradeSchemaDb(settings, logger).CreateAsync(["trade"]).ConfigureAwait(false);
    }

    static async Task<long[]> VerifyRemovedIndexesAsync(NpgsqlConnection db)
    {
        var identities = new List<long>();
        foreach (var workload in Workloads.Values)
        {
            var primary = await Scalar(db, """
                SELECT p.indexrelid::bigint FROM pg_index p
                JOIN pg_constraint c ON c.conindid=p.indexrelid AND c.contype='p'
                WHERE p.indrelid=to_regclass($1) AND p.indisprimary AND p.indisvalid AND p.indisready
                    AND to_regclass($2) IS NULL
                """, workload.Table, workload.Table + "_ordered").ConfigureAwait(false);
            if (primary is not long oid)
                throw new InvalidOperationException($"Unexpected index or primary key definition for {workload.Table}.");
            identities.Add(oid);
        }
        return identities.ToArray();
    }

    static async Task<string> TableFingerprintAsync(NpgsqlConnection db, Workload workload) =>
        (string)(await Scalar(db, $"""
            SELECT count(*) || ':' || sum(hashtextextended(row_to_json(t)::text,0)::numeric)
            FROM {workload.Table} t
            """).ConfigureAwait(false))!;

    static async Task VerifyReadPlanAsync(NpgsqlConnection db, string phase, string name, Workload workload, string index, bool requireIndex)
    {
        await using var explain = Command(db, "EXPLAIN (ANALYZE, BUFFERS) " + workload.Read, workload.ReadParameters);
        await using var reader = await explain.ExecuteReaderAsync().ConfigureAwait(false);
        Console.WriteLine($"Read plan for {name} ({phase}):");
        var usesIndex = false;
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var line = reader.GetString(0);
            usesIndex |= line.Contains($"using {index} ", StringComparison.Ordinal);
            Console.WriteLine(line);
        }
        if (requireIndex && !usesIndex)
            throw new InvalidOperationException($"The read plan for {name} did not use {index}.");
    }

    static string Insert(Workload workload) => $"""
        INSERT INTO {workload.Table} ({workload.Columns})
        SELECT {workload.Values}
        FROM generate_series($1::int,$2::int) AS rows(i)
        CROSS JOIN LATERAL (SELECT (i-1)/64+1 AS grp, (i-1)%64+1 AS slot) AS parts
        CROSS JOIN LATERAL (SELECT (lpad(to_hex(grp),8,'0') || '-0000-4000-8000-000000000000')::uuid AS group_uuid) AS keys
        """;

    static async Task<string> Fingerprint(NpgsqlConnection db, Workload workload)
    {
        await using var command = Command(db, $"SELECT row_to_json(r)::text FROM ({workload.Read.TrimEnd(';')}) r", workload.ReadParameters);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var count = 0;
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(reader.GetString(0) + "\n"));
            count++;
        }
        return count > 0 ? $"{count}:{Convert.ToHexString(hash.GetHashAndReset())}"
            : throw new InvalidOperationException("Read returned no fixture rows.");
    }

    [GlobalSetup(Target = nameof(Read))]
    public async Task Setup()
    {
        connection = Connect();
        await connection.OpenAsync().ConfigureAwait(false);
    }

    [GlobalSetup(Target = nameof(InsertBatch))]
    public async Task SetupWrites()
    {
        await Setup().ConfigureAwait(false);
        await Execute(connection, "CHECKPOINT").ConfigureAwait(false);
        initialWal = (string)(await Scalar(connection, "SELECT pg_current_wal_insert_lsn()::text").ConfigureAwait(false))!;
    }

    [Benchmark]
    public async Task<int> Read()
    {
        var workload = Workloads[Case];
        await using var command = Command(connection, workload.Read, workload.ReadParameters);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var count = 0;
        var values = new object[reader.FieldCount];
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            reader.GetValues(values);
            count++;
        }
        return count;
    }

    [Benchmark]
    public async Task InsertBatch()
    {
        var start = SeedRows + rowsWritten + 1;
        rowsWritten += WriteRows;
        await Execute(connection, Insert(Workloads[Case]), start, SeedRows + rowsWritten).ConfigureAwait(false);
    }

    [GlobalCleanup(Target = nameof(Read))]
    public ValueTask Cleanup() => connection.DisposeAsync();

    [GlobalCleanup(Target = nameof(InsertBatch))]
    public async Task VerifyWrites()
    {
        await using var db = connection;
        var wal = (decimal)(await Scalar(db, "SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(),$1::pg_lsn)", initialWal)
            .ConfigureAwait(false))!;
        if (!Equals(await Scalar(db, $"SELECT count(*) FROM {Workloads[Case].Table}").ConfigureAwait(false), (long)SeedRows + rowsWritten)
            || rowsWritten == 0)
            throw new InvalidOperationException($"Unexpected committed row count for {Case}.");
        Console.WriteLine(FormattableString.Invariant($"WAL {Case}: {rowsWritten} rows, {wal} bytes, {wal / rowsWritten:F2} bytes/row."));
    }

    static NpgsqlCommand Command(NpgsqlConnection db, string sql, object?[] values)
    {
        var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, values);
        return command;
    }

    static async Task Execute(NpgsqlConnection db, string sql, params object?[] values)
    {
        await using var command = Command(db, sql, values);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    static async Task<object?> Scalar(NpgsqlConnection db, string sql, params object?[] values)
    {
        await using var command = Command(db, sql, values);
        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }
}
