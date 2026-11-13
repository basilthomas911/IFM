using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

public enum OptionQuoteWrite
{
    QuoteInsert,
    QuoteUpdate,
    StatisticsInsert,
    StatisticsUpdate
}

public class OptionChainStatisticsWriteBenchmarks
{
    const string Table = "market_data.futures_option_chain_quote_data";
    const string VolumeIndex = "market_data.futures_option_chain_quote_data_volume_latest";
    const string OpenInterestIndex = "market_data.futures_option_chain_quote_data_openinterest_latest";
    const int QuotesPerBatch = 64;
    const int ContractsPerDate = 1024;
    const int HistoryDays = 180;
    const int EligibleHistoryDays = 64;
    const int WalBatches = 128;
    const long QuoteTickBase = 1_000_000;
    const long CycleTickStride = 1_024_000;
    const string Underlying = "ES";
    static readonly DateOnly LastDate = new(2026, 10, 14);
    static readonly DateOnly Expiry = new(2026, 12, 18);
    static readonly string StatisticsUpsertSql = MarketDataDbSql.UpsertFuturesOptionChainQuoteData
        .Replace("gamma, vega, theta, rho )", "gamma, vega, theta, rho, volume, volumevaluedate, openinterest, openinterestvaluedate )",
            StringComparison.Ordinal)
        .Replace("$15, $16, $17, $18 )", "$15, $16, $17, $18, $19, $20, $21, $22 )", StringComparison.Ordinal)
        .Replace("rho=EXCLUDED.rho;", """
            rho=EXCLUDED.rho, volume=EXCLUDED.volume, volumevaluedate=EXCLUDED.volumevaluedate,
            openinterest=EXCLUDED.openinterest, openinterestvaluedate=EXCLUDED.openinterestvaluedate;
            """, StringComparison.Ordinal);

    NpgsqlConnection _db = null!;
    int _nextQuote;
    int _updateCycle;
    int _quoteDayOffset;
    long FirstTickId => QuoteTickBase + _updateCycle * CycleTickStride;
    double UpdatedOptionPrice => 3.5 + _updateCycle * 0.125;
    bool IsInsert => Workload is OptionQuoteWrite.QuoteInsert or OptionQuoteWrite.StatisticsInsert;
    bool WritesStatistics => Workload is OptionQuoteWrite.StatisticsInsert or OptionQuoteWrite.StatisticsUpdate;

    [Params(false, true)] public bool WithIndexes { get; set; }
    [ParamsAllValues] public OptionQuoteWrite Workload { get; set; }
    protected virtual int HeapFillfactor => 100;
    protected virtual bool MeasureHotUpdates => false;

    [GlobalSetup]
    public async Task Setup()
    {
        var schema = await OpenFixture().ConfigureAwait(false);
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        if (!WithIndexes)
            await Execute($"DROP INDEX {VolumeIndex}, {OpenInterestIndex};").ConfigureAwait(false);
        await Execute($"ALTER TABLE {Table} SET (fillfactor = {HeapFillfactor});").ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
        await MeasureWal(MeasureHotUpdates, $"FILLFACTOR,{Workload},{HeapFillfactor}").ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
    }

    async Task<MarketDataSchemaDb> OpenFixture()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (connectionString.Host != "127.0.0.1" || connectionString.Port is not (58231 or 58232) ||
            connectionString.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");

        _db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString.ConnectionString);
        await _db.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            connectionString.ConnectionString, "System.Data.Postgres");
        return new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
    }

    async Task MeasureWal(bool measureHotUpdates, string label)
    {
        var heapBytes = measureHotUpdates
            ? (long)await Scalar($"SELECT pg_relation_size('{Table}')").ConfigureAwait(false)
            : 0;
        var beforeUpdates = measureHotUpdates ? await ReadUpdateCounters().ConfigureAwait(false) : default;
        var walStart = await ReadWalPosition().ConfigureAwait(false);
        for (var batch = 0; batch < WalBatches; batch++)
            await WriteQuotes().ConfigureAwait(false);
        var walBytes = await ReadWalBytesSince(walStart).ConfigureAwait(false);
        var rows = QuotesPerBatch * WalBatches;
        await VerifyWrites(rows).ConfigureAwait(false);
        Console.WriteLine(FormattableString.Invariant(
            $"WAL,{Workload},{WithIndexes},{rows},{walBytes},{walBytes / rows:F2}"));
        if (measureHotUpdates)
        {
            var afterUpdates = await ReadUpdateCounters().ConfigureAwait(false);
            var updates = afterUpdates.Updates - beforeUpdates.Updates;
            var hot = afterUpdates.Hot - beforeUpdates.Hot;
            if (updates != rows || hot < 0 || hot > updates || (WritesStatistics && hot != 0))
                throw new InvalidOperationException($"Unexpected update counters for {Workload}: {updates} updates, {hot} HOT.");
            Console.WriteLine(FormattableString.Invariant(
                $"{label},{updates},{hot},{100m * hot / updates:F2},{walBytes},{walBytes / updates:F2},{heapBytes}"));
        }
    }

    async Task<string> ReadWalPosition() =>
        (string)await Scalar("SELECT pg_current_wal_insert_lsn()::text").ConfigureAwait(false);

    async Task<decimal> ReadWalBytesSince(string position)
    {
        await using var command = new NpgsqlCommand("SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn)", _db);
        PostgresValueBinding.AddParameters(command, [position]);
        return (decimal)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    internal static async Task MeasureRewriteAsync()
    {
        var fixture = new OptionChainStatisticsWriteBenchmarks();
        var schema = await fixture.OpenFixture().ConfigureAwait(false);
        try
        {
            await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
            var indexes = await fixture.ReadIndexIds().ConfigureAwait(false);
            await using var observer = new NpgsqlConnection(fixture._db.ConnectionString);
            await observer.OpenAsync().ConfigureAwait(false);
            for (var run = 1; run <= 5; run++)
                await fixture.MeasureRewrite(schema, observer, indexes, run).ConfigureAwait(false);
        }
        finally
        {
            await fixture.Cleanup().ConfigureAwait(false);
        }
    }

    async Task MeasureRewrite(MarketDataSchemaDb schema, NpgsqlConnection observer, string indexes, int run)
    {
        await Execute($"ALTER TABLE {Table} RESET (fillfactor);").ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
        var packed = await ReadHeapLayout().ConfigureAwait(false);
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        await AssertFillfactor(80).ConfigureAwait(false);
        if (packed != await ReadHeapLayout().ConfigureAwait(false))
            throw new InvalidOperationException("The schema upgrade changed the packed heap.");
        await Execute("CHECKPOINT;").ConfigureAwait(false);

        var walStart = await ReadWalPosition().ConfigureAwait(false);
        using var sampler = new NpgsqlCommand($"""
            SELECT EXISTS (
                SELECT 1 FROM pg_locks
                WHERE pid = $1 AND relation = '{Table}'::regclass
                    AND locktype = 'relation' AND mode = 'AccessExclusiveLock' AND granted
            )
            """, observer);
        PostgresValueBinding.AddParameters(sampler, [_db.ProcessID]);
        await sampler.PrepareAsync().ConfigureAwait(false);
        if ((bool)(await sampler.ExecuteScalarAsync().ConfigureAwait(false))!)
            throw new InvalidOperationException("The rewrite lock was held before VACUUM FULL.");

        var clock = Stopwatch.StartNew();
        async Task<double> RunVacuum()
        {
            await Execute($"VACUUM (FULL, ANALYZE) {Table};").ConfigureAwait(false);
            return clock.Elapsed.TotalMilliseconds;
        }
        var vacuum = RunVacuum();
        var lastUnlockedBefore = 0d;
        var firstLocked = 0d;
        var lastLocked = 0d;
        double? firstUnlockedAfter = null;
        do
        {
            var sampleStart = clock.Elapsed.TotalMilliseconds;
            var locked = (bool)(await sampler.ExecuteScalarAsync().ConfigureAwait(false))!;
            var sampleEnd = clock.Elapsed.TotalMilliseconds;
            if (locked)
            {
                if (firstLocked == 0)
                    firstLocked = sampleEnd;
                lastLocked = sampleStart;
            }
            else if (firstLocked == 0)
            {
                lastUnlockedBefore = sampleStart;
            }
            else
            {
                firstUnlockedAfter = sampleEnd;
            }

            if (!vacuum.IsCompleted)
                await Task.Delay(5).ConfigureAwait(false);
        }
        while (!vacuum.IsCompleted && firstUnlockedAfter is null);

        var commandMs = await vacuum.ConfigureAwait(false);
        var walBytes = await ReadWalBytesSince(walStart).ConfigureAwait(false);
        if (firstLocked == 0 || lastLocked <= firstLocked ||
            (bool)(await sampler.ExecuteScalarAsync().ConfigureAwait(false))!)
            throw new InvalidOperationException("Unable to bracket the exclusive lock duration.");
        var minLockMs = lastLocked - firstLocked;
        var maxLockMs = (firstUnlockedAfter ?? commandMs) - lastUnlockedBefore;

        var rewritten = await ReadHeapLayout().ConfigureAwait(false);
        if (rewritten.File == packed.File || rewritten.Bytes <= packed.Bytes ||
            rewritten.Fingerprint != packed.Fingerprint || indexes != await ReadIndexIds().ConfigureAwait(false))
            throw new InvalidOperationException("The rewrite did not preserve the seeded data and both indexes.");
        Console.WriteLine(FormattableString.Invariant(
            $"REWRITE,{run},{walBytes},{minLockMs:F2},{maxLockMs:F2},{commandMs:F2},{packed.Bytes},{rewritten.Bytes}"));
    }

    internal static async Task VerifyVacuumCyclesAsync(bool rotating = false, int cycles = 6, bool? normalRewrite = null,
        TimeSpan cycleInterval = default)
    {
        var fixture = new OptionChainStatisticsWriteBenchmarks { WithIndexes = true, Workload = OptionQuoteWrite.QuoteUpdate };
        var schema = await fixture.OpenFixture().ConfigureAwait(false);
        try
        {
            await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
            var indexes = await fixture.ReadIndexIds().ConfigureAwait(false);
            bool[] arms = normalRewrite is { } normal ? [normal] : [false, true];
            foreach (var rewrite in arms)
                await fixture.RunVacuumCycles(schema, indexes, rewrite, rotating, cycles, normalRewrite.HasValue,
                    cycleInterval).ConfigureAwait(false);
        }
        finally
        {
            await fixture.Cleanup().ConfigureAwait(false);
        }
    }

    async Task RunVacuumCycles(MarketDataSchemaDb schema, string indexes, bool rewrite, bool rotating, int cycles,
        bool normalMaintenance, TimeSpan cycleInterval)
    {
        const int daysPerCycle = QuotesPerBatch * WalBatches / ContractsPerDate;
        const int groups = EligibleHistoryDays / daysPerCycle;
        var arm = rewrite ? "rewritten" : "packed";
        await Execute($"ALTER TABLE {Table} RESET (fillfactor);").ConfigureAwait(false);
        await Reset(checkpoint: !normalMaintenance).ConfigureAwait(false);
        var packed = await ReadHeapLayout().ConfigureAwait(false);
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        await AssertFillfactor(80).ConfigureAwait(false);
        if (packed != await ReadHeapLayout().ConfigureAwait(false))
            throw new InvalidOperationException("The schema upgrade changed the packed heap.");

        var armStart = await ReadWalPosition().ConfigureAwait(false);
        if (rewrite)
        {
            if (!normalMaintenance)
                await Execute("CHECKPOINT;").ConfigureAwait(false);
            var rewriteStart = await ReadWalPosition().ConfigureAwait(false);
            await Execute($"VACUUM (FULL, ANALYZE) {Table};").ConfigureAwait(false);
            var rewriteWal = await ReadWalBytesSince(rewriteStart).ConfigureAwait(false);
            Console.WriteLine(FormattableString.Invariant($"REWRITE-WAL,{rewriteWal}"));
        }
        var start = await ReadHeapLayout().ConfigureAwait(false);
        if (start.Fingerprint != packed.Fingerprint || (start.File != packed.File) != rewrite)
            throw new InvalidOperationException("The fixture heap did not start in the expected state.");
        Console.WriteLine($"Starting {arm} with {(rotating ? "rotating" : "repeated")} quotes: heap file {start.File}, {start.Bytes} bytes, rows {start.Fingerprint}.");

        var previousWal = 0m;
        var workloadClock = Stopwatch.StartNew();
        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            _nextQuote = 0;
            _updateCycle = cycle;
            _quoteDayOffset = rotating ? (cycle - 1) % groups * daysPerCycle : 0;
            Console.WriteLine($"Cycle {arm} {cycle}: quote dates {LastDate.AddDays(-_quoteDayOffset - daysPerCycle + 1):yyyy-MM-dd} through {LastDate.AddDays(-_quoteDayOffset):yyyy-MM-dd}.");
            if (!normalMaintenance)
                await Execute("CHECKPOINT;").ConfigureAwait(false);
            await MeasureWal(true, $"CYCLE,{arm},{cycle}").ConfigureAwait(false);
            if (normalMaintenance)
            {
                var remaining = cycleInterval * cycle - workloadClock.Elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining).ConfigureAwait(false);
                await Execute("SELECT pg_stat_clear_snapshot();").ConfigureAwait(false);
                var maintenance = (string)await Scalar($"""
                    SELECT concat_ws(',', t.autovacuum_count, c.num_timed, c.num_requested, t.n_dead_tup,
                        pg_relation_size(t.relid))
                    FROM pg_stat_user_tables t CROSS JOIN pg_stat_checkpointer c
                    WHERE t.relid = '{Table}'::regclass
                    """).ConfigureAwait(false);
                var totalWal = await ReadWalBytesSince(armStart).ConfigureAwait(false);
                Console.WriteLine(FormattableString.Invariant($"NORMAL,{arm},{cycle},{totalWal - previousWal},{totalWal},{maintenance}"));
                previousWal = totalWal;
                continue;
            }
            var vacuumStart = await ReadWalPosition().ConfigureAwait(false);
            await Execute($"VACUUM {Table};").ConfigureAwait(false);
            var vacuumWal = await ReadWalBytesSince(vacuumStart).ConfigureAwait(false);
            var heapFile = (long)await Scalar($"SELECT pg_relation_filenode('{Table}')::bigint").ConfigureAwait(false);
            var heapBytes = (long)await Scalar($"SELECT pg_relation_size('{Table}')").ConfigureAwait(false);
            if (heapFile != start.File)
                throw new InvalidOperationException("Ordinary vacuum unexpectedly changed the heap file.");
            Console.WriteLine(FormattableString.Invariant($"VACUUM,{arm},{cycle},{vacuumWal},{heapBytes}"));
        }

        var distinctQuotes = (long)await Scalar($"SELECT count(*) FROM {Table} WHERE tickid >= {QuoteTickBase + CycleTickStride}")
            .ConfigureAwait(false);
        var expectedQuotes = (rotating ? Math.Min(cycles, groups) : 1) * QuotesPerBatch * WalBatches;
        if (distinctQuotes != expectedQuotes)
            throw new InvalidOperationException($"Expected {expectedQuotes} distinct updated quotes for {arm}, found {distinctQuotes}.");
        if (indexes != await ReadIndexIds().ConfigureAwait(false))
            throw new InvalidOperationException("The partial index identities changed.");
        Console.WriteLine(FormattableString.Invariant($"WORKLOAD-SECONDS,{arm},{workloadClock.Elapsed.TotalSeconds:F2}"));
        Console.WriteLine($"Verified {distinctQuotes} distinct updated quotes for {arm} and both original partial indexes.");
    }

    internal static async Task VerifyFillfactorMigrationAsync()
    {
        var fixture = new OptionChainStatisticsWriteBenchmarks { WithIndexes = true, Workload = OptionQuoteWrite.QuoteUpdate };
        var schema = await fixture.OpenFixture().ConfigureAwait(false);
        try
        {
            await fixture.VerifyFillfactorMigration(schema).ConfigureAwait(false);
        }
        finally
        {
            await fixture.Cleanup().ConfigureAwait(false);
        }
    }

    async Task VerifyFillfactorMigration(MarketDataSchemaDb schema)
    {
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        await AssertFillfactor(80).ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
        var fresh = await ReadHeapLayout().ConfigureAwait(false);
        var indexes = await ReadIndexIds().ConfigureAwait(false);
        Console.WriteLine($"Verified fresh schema at fillfactor 80 with both indexes and {fresh.Bytes} heap bytes.");
        await MeasureWal(true, "MIGRATION,fresh-80").ConfigureAwait(false);

        await Execute($"ALTER TABLE {Table} RESET (fillfactor);").ConfigureAwait(false);
        await AssertFillfactor(100).ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
        var legacy = await ReadHeapLayout().ConfigureAwait(false);
        if (legacy.Bytes >= fresh.Bytes || legacy.Fingerprint != fresh.Fingerprint)
            throw new InvalidOperationException("Expected the same seed rows in a denser heap at fillfactor 100.");
        await MeasureWal(true, "MIGRATION,legacy-100").ConfigureAwait(false);

        await Reset().ConfigureAwait(false);
        var packed = await ReadHeapLayout().ConfigureAwait(false);
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        await AssertFillfactor(80).ConfigureAwait(false);
        if (packed != await ReadHeapLayout().ConfigureAwait(false) || indexes != await ReadIndexIds().ConfigureAwait(false))
            throw new InvalidOperationException("The populated upgrade changed the heap layout, rows, or index identities.");
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        await AssertFillfactor(80).ConfigureAwait(false);
        if (packed != await ReadHeapLayout().ConfigureAwait(false) || indexes != await ReadIndexIds().ConfigureAwait(false))
            throw new InvalidOperationException("Schema reapplication changed the heap layout, rows, or index identities.");
        Console.WriteLine($"Verified populated upgrade and reapplication preserved data, index identities, and heap file {packed.File} ({packed.Bytes} bytes).");
        await MeasureWal(true, "MIGRATION,upgraded-80-packed").ConfigureAwait(false);

        // Restore the same packed input before applying the schema and rewriting it in this fixture.
        await Execute($"ALTER TABLE {Table} RESET (fillfactor);").ConfigureAwait(false);
        await Reset().ConfigureAwait(false);
        await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
        var beforeRewrite = await ReadHeapLayout().ConfigureAwait(false);
        await Execute($"VACUUM (FULL, ANALYZE) {Table};").ConfigureAwait(false);
        await Execute("CHECKPOINT;").ConfigureAwait(false);
        var rewritten = await ReadHeapLayout().ConfigureAwait(false);
        if (rewritten.File == beforeRewrite.File || rewritten.Bytes <= beforeRewrite.Bytes ||
            rewritten.Fingerprint != beforeRewrite.Fingerprint || indexes != await ReadIndexIds().ConfigureAwait(false))
            throw new InvalidOperationException("Expected a repacked heap with the same data and indexes.");
        Console.WriteLine($"Verified fixture rewrite changed heap file {beforeRewrite.File} to {rewritten.File} and size {beforeRewrite.Bytes} to {rewritten.Bytes} bytes.");
        await MeasureWal(true, "MIGRATION,upgraded-80-rewritten").ConfigureAwait(false);
    }

    async Task AssertFillfactor(int expected)
    {
        var fillfactor = (int)await Scalar($"""
            SELECT COALESCE((SELECT option_value::int FROM pg_options_to_table(reloptions)
                             WHERE option_name = 'fillfactor'), 100)
            FROM pg_class WHERE oid = '{Table}'::regclass
            """).ConfigureAwait(false);
        if (fillfactor != expected)
            throw new InvalidOperationException($"Expected fillfactor {expected}, found {fillfactor}.");
    }

    async Task<string> ReadIndexIds()
    {
        var indexIds = (string)await Scalar($"""
            SELECT COALESCE(string_agg(indexrelid::text, ',' ORDER BY indexrelid), '')
            FROM pg_index WHERE indexrelid IN ('{VolumeIndex}'::regclass, '{OpenInterestIndex}'::regclass)
                AND indrelid = '{Table}'::regclass AND indisvalid AND indisready
            """).ConfigureAwait(false);
        if (indexIds.Split(',').Length != 2)
            throw new InvalidOperationException("Expected both valid partial indexes.");
        return indexIds;
    }

    async Task<(long File, long Bytes, string Fingerprint, string RowLocations)> ReadHeapLayout()
    {
        await using var command = new NpgsqlCommand($"""
            SELECT pg_relation_filenode('{Table}')::bigint, pg_relation_size('{Table}'),
                count(*) || ':' || sum(hashtextextended(row_to_json(q)::text, 0)::numeric),
                sum(hashtextextended(q.ctid::text || ':' || q.tickid, 0)::numeric)::text
            FROM {Table} q
            """, _db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            throw new InvalidOperationException("The quote table layout was unavailable.");
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3));
    }

    async Task<(long Updates, long Hot)> ReadUpdateCounters()
    {
        // Flush this writer's statistics before reading the counters outside any write transaction.
        await Execute("SELECT pg_stat_force_next_flush(); SELECT pg_stat_clear_snapshot();").ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT n_tup_upd, n_tup_hot_upd
            FROM pg_stat_user_tables
            WHERE relid = '{Table}'::regclass
            """, _db);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            throw new InvalidOperationException("The quote table statistics row was missing.");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    async Task Reset(bool checkpoint = true)
    {
        _nextQuote = 0;
        _updateCycle = 0;
        _quoteDayOffset = 0;
        await Execute($"TRUNCATE TABLE {Table};").ConfigureAwait(false);
        await using var seed = new NpgsqlCommand($"""
            INSERT INTO {Table} (underlyingcontractid, expirydate, valuedate, contractid, tickid, ticktime,
                optionprice, bidprice, askprice, bidsize, asksize, impliedvolatility, underlyingprice,
                delta, gamma, vega, theta, rho, volume, volumevaluedate, openinterest, openinterestvaluedate)
            SELECT $1, $2, $3::date - age, 'OPT-' || id, id, time '14:30', 3.25, 3.20, 3.30, 10, 12,
                0.20, 5800, 0.5, 0.01, 0.5, -0.01, 0.01,
                CASE WHEN age < {EligibleHistoryDays} OR age % 5 = 0 THEN id END, $3::date - age,
                CASE WHEN age < {EligibleHistoryDays} OR age % 7 = 0 THEN id * 100 END, $3::date - age
            FROM generate_series(0, {HistoryDays - 1}) age
            CROSS JOIN generate_series(0, {ContractsPerDate - 1}) id
            """, _db);
        PostgresValueBinding.AddParameters(seed, [Underlying, Expiry, LastDate]);
        if (await seed.ExecuteNonQueryAsync().ConfigureAwait(false) != ContractsPerDate * HistoryDays)
            throw new InvalidOperationException("Unexpected seed row count.");
        await Execute($"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
        if (checkpoint)
            await Execute("CHECKPOINT;").ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = QuotesPerBatch)]
    public async Task WriteQuotes()
    {
        var sql = WritesStatistics ? StatisticsUpsertSql : MarketDataDbSql.UpsertFuturesOptionChainQuoteData;
        await using var batch = new NpgsqlBatch(_db);
        for (var i = 0; i < QuotesPerBatch; i++)
        {
            var number = _nextQuote++;
            var day = _quoteDayOffset + number / ContractsPerDate;
            if (!IsInsert && day >= EligibleHistoryDays)
                throw new InvalidOperationException("The timed updates exceeded the seeded eligible range.");
            var date = LastDate.AddDays(IsInsert ? day + 1 : -day);
            var command = new NpgsqlBatchCommand(sql);
            object?[] values = [Underlying, Expiry, date, $"OPT-{number % ContractsPerDate}", FirstTickId + number,
                new TimeOnly(14, 31), UpdatedOptionPrice, 3.45, 3.55, 11, 13, 0.21, 5810d, 0.51, 0.02, 0.6, -0.02, 0.02];
            PostgresValueBinding.AddParameters(command, WritesStatistics
                ? [..values, 1_000L + number, date.AddDays(-1), 2_000L + number, date.AddDays(-2)]
                : values);
            batch.BatchCommands.Add(command);
        }
        if (await batch.ExecuteNonQueryAsync().ConfigureAwait(false) != QuotesPerBatch)
            throw new InvalidOperationException("A quote batch affected an unexpected number of rows.");
    }

    async Task VerifyWrites(int expected)
    {
        var total = (long)await Scalar($"SELECT count(*) FROM {Table}").ConfigureAwait(false);
        var expectedTotal = ContractsPerDate * HistoryDays + (IsInsert ? expected : 0);
        var statistics = WritesStatistics
            ? "volume = tickid - $1 + 1000 AND openinterest = tickid - $1 + 2000 AND volumevaluedate = valuedate - 1 AND openinterestvaluedate = valuedate - 2"
            : IsInsert ? "volume IS NULL AND openinterest IS NULL"
            : $"volume = (tickid - $1) % {ContractsPerDate} AND openinterest = volume * 100 AND volumevaluedate = valuedate AND openinterestvaluedate = valuedate";
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) FROM {Table}
            WHERE tickid >= $1 AND tickid < $2 AND optionprice = $3 AND {statistics}
            """, _db);
        PostgresValueBinding.AddParameters(command, [FirstTickId, FirstTickId + expected, UpdatedOptionPrice]);
        var saved = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
        if (total != expectedTotal || saved != expected)
            throw new InvalidOperationException($"Unexpected persisted rows for {Workload}: {total} total, {saved} matching.");
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _db.DisposeAsync().ConfigureAwait(false);

    async Task Execute(string sql)
    {
        await using var command = new NpgsqlCommand(sql, _db);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    async Task<object> Scalar(string sql)
    {
        await using var command = new NpgsqlCommand(sql, _db);
        return (await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }
}

public class OptionChainQuoteFillfactorBenchmarks : OptionChainStatisticsWriteBenchmarks
{
    [Params(100, 80)] public int Fillfactor { get; set; }

    protected override int HeapFillfactor => Fillfactor;
    protected override bool MeasureHotUpdates => true;
}
