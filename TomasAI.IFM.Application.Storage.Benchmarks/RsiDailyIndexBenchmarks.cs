using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the latest daily RSI read and the cost of persisting a new RSI signal.</summary>
[MemoryDiagnoser]
public class RsiDailyIndexBenchmarks
{
    const string Contract = "ES-RSI-BENCH";
    const int PeriodLength = 14;
    const int SlotsPerDay = 32;
    static readonly DateOnly FirstDay = new(2018, 1, 1);

    MarketDataDbContext database = null!;
    string connectionString = null!;
    FuturesRsiSignalReadModel nextSignal = null!;
    string initialWalPosition = null!;
    int writes;

    [Params(256, 65536)]
    public int RowsPerSeries { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        connectionString = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");

        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            connectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        var factory = new DbContextFactory(new DbContextResolver(type => type == typeof(IObjectRepository<MarketDataDbContext>)
            ? database : throw new InvalidOperationException("Unexpected fixture context " + type.Name)));
        database = new(settings, factory, new UnexpectedSequenceUse(), logger);
        await new MarketDataSchemaDb(settings, logger).CreateAsync(["market_data"]).ConfigureAwait(false);

        await using var connection = await OpenAsync().ConfigureAwait(false);
        var durable = await ScalarAsync(connection,
            "SELECT current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on' AND current_setting('full_page_writes') = 'on';")
            .ConfigureAwait(false);
        if (durable is not true)
            throw new InvalidOperationException("Benchmark requires full WAL durability.");

        await ExecuteAsync(connection, "TRUNCATE market_data.futures_rsi_signal;").ConfigureAwait(false);
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO market_data.futures_rsi_signal
                (contractid, timeperiod, periodlength, valuedate, timestamp, price,
                 pricechange, pricegain, priceloss, averagepricegain, averagepriceloss,
                 rs, rsi, rsiaverage, rsislope, sourcesequence, sourceeventtimestamp, previousrsi, regimeslope, iswarm)
            SELECT series.contract, series.frame, series.period,
                $1::date + slot / $2 + series.day_shift,
                time '12:00:00' + (slot % $2) * interval '1 minute', 6000 + slot % 100,
                1, 1, 0, 2.5, 1.5, 1.5, 40 + slot % 25, 45, 0.5, slot + 1,
                timestamptz '2018-01-01 12:00:00+00' + slot * interval '1 minute',
                40 + slot % 25 - 0.5, 0.5, true
            FROM (VALUES ('ES-RSI-BENCH', 'Daily', 14, 0),
                         ('NQ-RSI-BENCH', 'Daily', 14, 1),
                         ('ES-RSI-BENCH', 'OneMinute', 14, 1),
                         ('ES-RSI-BENCH', 'Daily', 21, 1)) AS series(contract, frame, period, day_shift)
            CROSS JOIN generate_series(0, $3 - 1) AS samples(slot);
            """, connection))
        {
            seed.Parameters.Add(new NpgsqlParameter { Value = FirstDay });
            seed.Parameters.Add(new NpgsqlParameter { Value = SlotsPerDay });
            seed.Parameters.Add(new NpgsqlParameter { Value = RowsPerSeries });
            if (await seed.ExecuteNonQueryAsync().ConfigureAwait(false) != 4 * RowsPerSeries)
                throw new InvalidOperationException("RSI fixture row count is incorrect.");
        }
        await ExecuteAsync(connection, "VACUUM (ANALYZE) market_data.futures_rsi_signal;").ConfigureAwait(false);

        var lastSlot = RowsPerSeries - 1;
        var expected = new FuturesRsiSignalReadModel(Contract, FirstDay.AddDays(lastSlot / SlotsPerDay),
            TimeFrameType.Daily, PeriodLength, new TimeOnly(12, lastSlot % SlotsPerDay),
            6000 + lastSlot % 100, 1, 1, 0, 2.5m, 1.5m, 1.5, 40 + lastSlot % 25, 45, 0.5,
            RowsPerSeries, FirstDay.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc).AddMinutes(lastSlot))
        {
            PreviousRsi = 40 + lastSlot % 25 - 0.5,
            RegimeSlope = 0.5,
            IsWarm = true
        };
        if (await ReadLatestDailySignal().ConfigureAwait(false) != expected
            || await database.GetLastFuturesRsiDailySignalAsync("MISSING-RSI-BENCH", TimeFrameType.Daily, PeriodLength)
                .ConfigureAwait(false) is not null)
            throw new InvalidOperationException("RSI fixture did not return the complete latest row and null for a missing series.");

        await using var plan = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + MarketDataDbSql.GetLastFuturesRsiDailySignal, connection);
        plan.Parameters.Add(new NpgsqlParameter { Value = Contract });
        plan.Parameters.Add(new NpgsqlParameter { Value = nameof(TimeFrameType.Daily) });
        plan.Parameters.Add(new NpgsqlParameter { Value = PeriodLength });
        await using (var reader = await plan.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false))
                Console.WriteLine(reader.GetString(0));

        var indexBytes = await ScalarAsync(connection, "SELECT pg_indexes_size('market_data.futures_rsi_signal');").ConfigureAwait(false);
        var candidateBytes = await ScalarAsync(connection,
            "SELECT COALESCE(pg_relation_size(to_regclass('market_data.futures_rsi_signal_latest_daily_bench')), 0);")
            .ConfigureAwait(false);
        Console.WriteLine($"Correctness passed for {RowsPerSeries} rows per series. Full latest row and missing series matched. Index bytes: {indexBytes} total, {candidateBytes} candidate.");
        nextSignal = expected with { ValueDate = expected.ValueDate.AddDays(2) };
        initialWalPosition = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    [Benchmark]
    public Task<FuturesRsiSignalReadModel?> ReadLatestDailySignal() =>
        database.GetLastFuturesRsiDailySignalAsync(Contract, TimeFrameType.Daily, PeriodLength);

    [Benchmark]
    public Task InsertNewSignal()
    {
        int ordinal = ++writes;
        return database.InsertFuturesRsiSignalAsync(nextSignal with
        {
            Timestamp = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(ordinal)),
            SourceSequence = RowsPerSeries + ordinal,
            SourceEventTimestamp = nextSignal.ValueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddSeconds(ordinal)
        });
    }

    [GlobalCleanup(Target = nameof(InsertNewSignal))]
    public async Task VerifyWrites()
    {
        if (writes == 0)
            return;

        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var wal = new NpgsqlCommand("SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn);", connection);
        wal.Parameters.Add(new NpgsqlParameter { Value = initialWalPosition });
        var bytes = (decimal)(await wal.ExecuteScalarAsync().ConfigureAwait(false))!;
        var latest = await ReadLatestDailySignal().ConfigureAwait(false);
        var savedCount = await ScalarAsync(connection,
            "SELECT count(*) FROM market_data.futures_rsi_signal WHERE valuedate = (SELECT max(valuedate) FROM market_data.futures_rsi_signal);")
            .ConfigureAwait(false);
        if (latest?.ValueDate != nextSignal.ValueDate || latest.SourceSequence != RowsPerSeries + writes
            || savedCount is not long count || count != writes)
            throw new InvalidOperationException("The write benchmark did not persist the expected new signals.");

        Console.WriteLine($"Write correctness passed for {RowsPerSeries} initial rows per series. {writes} committed inserts; {bytes:F0} WAL bytes ({bytes / writes:F1} bytes/insert).");
    }

    async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
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
            throw new InvalidOperationException("The RSI workload must not allocate a sequence.");

        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken token = default) =>
            throw new InvalidOperationException("The RSI workload must not read a sequence.");
    }
}
