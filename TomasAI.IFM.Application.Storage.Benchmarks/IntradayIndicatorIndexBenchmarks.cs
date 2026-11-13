using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Compares intraday latest-signal reads, inserts and corrections with the fixture indexes.</summary>
[MemoryDiagnoser]
public class IntradayIndicatorIndexBenchmarks
{
    const string Contract = "ES-INTRADAY-BENCH";
    const TimeFrameType Frame = TimeFrameType.TenSeconds;
    const int Period = 14;
    static readonly DateOnly ReadDay = new(2026, 11, 10);
    static readonly DateOnly NullDay = ReadDay.AddDays(1);
    static readonly DateOnly InsertDay = ReadDay.AddDays(2);
    static readonly DateOnly UpdateDay = ReadDay.AddDays(3);
    static readonly DateTimeOffset Start = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset WinningAsOf = Start.AddHours(23).AddMinutes(59);
    static readonly DateTimeOffset WrittenAsOf = Start.AddDays(10);
    static readonly Guid Observation = new("83be212a-c4b8-4be2-b615-113630a6e836");

    MarketDataDbContext database = null!;
    string connectionString = null!;
    string table = null!;
    string initialWalPosition = null!;
    DateOnly writtenDay;
    int writes;

    [Params(MarketAnalyticsSignalKind.Rsi, MarketAnalyticsSignalKind.Adx,
        MarketAnalyticsSignalKind.Atr, MarketAnalyticsSignalKind.Macd)]
    public MarketAnalyticsSignalKind Indicator { get; set; }

    [Params(128, 8192)]
    public int RowsPerDay { get; set; }

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
        table = $"market_data.futures_{Indicator.ToString().ToLowerInvariant()}_signal";
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        if (await ScalarAsync(connection, """
            SELECT current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on'
                AND current_setting('full_page_writes') = 'on';
            """).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Benchmark requires full WAL durability.");

        // Each benchmark arm provisions its comparison indexes separately.
        await ExecuteAsync(connection, MarketDataSchemaDb.CreateBaseStatement).ConfigureAwait(false);
        await ExecuteAsync(connection, $"TRUNCATE {table};").ConfigureAwait(false);
        await SeedAsync(connection).ConfigureAwait(false);
        await ExecuteAsync(connection, $"VACUUM (ANALYZE) {table};").ConfigureAwait(false);
        await VerifyReadAsync(ReadDay, new TimeOnly(0, 0, 20), WinningAsOf, 2).ConfigureAwait(false);
        await VerifyReadAsync(NullDay, new TimeOnly(0, 0, 10), null, null).ConfigureAwait(false);
        if (await ReadIdentityAsync(ReadDay.AddDays(-1)).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("A missing value date returned an intraday indicator.");

        await ShowPlanAsync(connection).ConfigureAwait(false);
        var candidateBytes = await ScalarAsync(connection,
            $"SELECT COALESCE(pg_relation_size(to_regclass('{table}_intraday_bench')), 0);").ConfigureAwait(false);
        Console.WriteLine($"Read correctness passed for {Indicator} {RowsPerDay}: scope, as-of precedence, timestamp tie-break, null-only fallback, and missing date. Candidate index bytes: {candidateBytes}.");
        writes = 0;
        initialWalPosition = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    async Task SeedAsync(NpgsqlConnection connection)
    {
        var (keys, values, otherValues) = Indicator == MarketAnalyticsSignalKind.Macd
            ? ("signalemaperiod, fastemaperiod, slowemaperiod", "9, 12, 26", "9, 13, 26")
            : ("periodlength", "14", "15");
        var payload = Indicator switch
        {
            MarketAnalyticsSignalKind.Rsi => ("price, rsi, sourceeventtimestamp", "6000, 55, timestamptz '2026-11-10 00:00:00+00'"),
            MarketAnalyticsSignalKind.Adx => ("futuresprice, adxvalue, adx, adxstrength", "6000, 25, 'None', 'None'"),
            MarketAnalyticsSignalKind.Atr => ("futuresprice, atrvalue, atr, atrstrength", "6000, 4, 'None', 'None'"),
            MarketAnalyticsSignalKind.Macd => ("futuresprice, macdline, macd, macdstrength", "6000, 1, 'None', 'None'"),
            _ => throw new InvalidOperationException("Unexpected indicator.")
        };
        await ExecuteAsync(connection, $"""
            INSERT INTO {table} (contractid, timeperiod, {keys}, valuedate, timestamp, marketdataasof,
                configurationid, observationid, sourcesequence, calculationversion, calculationmethod, schemaversion,
                isvalid, iswarm, {payload.Item1})
            SELECT contract, frame, {keys}, day, time '00:00:00' + n * interval '10 seconds',
                CASE WHEN day = date '2026-11-11' THEN NULL
                     WHEN contract <> '{Contract}' OR frame <> 'TenSeconds' OR unrelated THEN timestamptz '2026-11-20 00:00:00+00'
                     WHEN day = date '2026-11-13' THEN timestamptz '2026-11-10 00:00:00+00'
                     WHEN n IN (1,2) THEN timestamptz '2026-11-10 23:59:00+00'
                     WHEN n = {RowsPerDay - 1} THEN NULL
                     ELSE timestamptz '2026-11-10 00:00:00+00' + n * interval '10 seconds' END,
                CASE WHEN day = date '2026-11-11' OR (day = date '2026-11-10' AND n = {RowsPerDay - 1})
                    THEN NULL ELSE 'benchmark' END,
                '{Observation}', n, 'intraday-benchmark', 'ClosedObservation', 1, true, true, {payload.Item2}
            FROM (VALUES
                ('{Contract}', 'TenSeconds', {values}, date '2026-11-10', {RowsPerDay}, false),
                ('OTHER-INTRADAY-BENCH', 'TenSeconds', {values}, date '2026-11-10', {RowsPerDay}, true),
                ('{Contract}', 'OneMinute', {values}, date '2026-11-10', {RowsPerDay}, true),
                ('{Contract}', 'TenSeconds', {otherValues}, date '2026-11-10', {RowsPerDay}, true),
                ('{Contract}', 'TenSeconds', {values}, date '2026-11-11', 2, false),
                ('{Contract}', 'TenSeconds', {values}, date '2026-11-13', 2048, false)
            ) AS scope(contract, frame, {keys}, day, size, unrelated)
            CROSS JOIN LATERAL generate_series(0, size - 1) AS slot(n);
            """).ConfigureAwait(false);
    }

    async Task ShowPlanAsync(NpgsqlConnection connection)
    {
        var sql = Indicator switch
        {
            MarketAnalyticsSignalKind.Rsi => MarketDataDbSql.GetLastFuturesRsiSignal,
            MarketAnalyticsSignalKind.Adx => MarketDataDbSql.GetLastFuturesAdxSignal,
            MarketAnalyticsSignalKind.Atr => MarketDataDbSql.GetLastFuturesAtrSignal,
            MarketAnalyticsSignalKind.Macd => MarketDataDbSql.GetLastFuturesMacdSignal,
            _ => throw new InvalidOperationException("Unexpected indicator.")
        };
        object[] parameters = Indicator == MarketAnalyticsSignalKind.Macd
            ? [Contract, nameof(TimeFrameType.TenSeconds), 9, 12, 26, ReadDay]
            : [Contract, nameof(TimeFrameType.TenSeconds), Period, ReadDay];
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });

        Console.WriteLine($"Plan for {Indicator} {RowsPerDay}:");
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            Console.WriteLine(reader.GetString(0));
    }

    Task ReadAsync(DateOnly day) => Indicator switch
    {
        MarketAnalyticsSignalKind.Rsi => database.GetLastFuturesRsiSignalAsync(Contract, day, Frame, Period),
        MarketAnalyticsSignalKind.Adx => database.GetLastFuturesAdxSignalAsync(Contract, day, Frame, Period),
        MarketAnalyticsSignalKind.Atr => database.GetLastFuturesAtrSignalAsync(Contract, day, Frame, Period),
        MarketAnalyticsSignalKind.Macd => database.GetLastFuturesMacdSignalAsync(Contract, day, Frame, 9, 12, 26),
        _ => throw new InvalidOperationException("Unexpected indicator.")
    };

    async Task<(TimeOnly Timestamp, MarketAnalyticsSignalMetadata? Metadata)?> ReadIdentityAsync(DateOnly day)
    {
        object? signal = ReadAsync(day) switch
        {
            Task<FuturesRsiSignalReadModel?> rsi => await rsi.ConfigureAwait(false),
            Task<FuturesAdxSignalReadModel?> adx => await adx.ConfigureAwait(false),
            Task<FuturesAtrSignalReadModel?> atr => await atr.ConfigureAwait(false),
            Task<FuturesMacdSignalReadModel?> macd => await macd.ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unexpected indicator result.")
        };
        return signal switch
        {
            FuturesRsiSignalReadModel rsi => (rsi.Timestamp, rsi.Metadata),
            FuturesAdxSignalReadModel adx => (adx.Timestamp, adx.Metadata),
            FuturesAtrSignalReadModel atr => (atr.Timestamp, atr.Metadata),
            FuturesMacdSignalReadModel macd => (macd.Timestamp, macd.Metadata),
            _ => null
        };
    }

    async Task VerifyReadAsync(DateOnly day, TimeOnly timestamp, DateTimeOffset? asOf, long? sequence)
    {
        var winner = await ReadIdentityAsync(day).ConfigureAwait(false);
        if (winner?.Timestamp != timestamp || winner.Value.Metadata?.MarketDataAsOfUtc != asOf
            || winner.Value.Metadata?.SourceSequence != sequence)
            throw new InvalidOperationException($"Unexpected {Indicator} winner for {day}: {winner}.");
    }

    [Benchmark]
    public Task ReadLatest() => ReadAsync(ReadDay);

    [Benchmark]
    public Task InsertSignal() => WriteAsync(InsertDay);

    [Benchmark]
    public Task CorrectSignalAsOf() => WriteAsync(UpdateDay);

    Task WriteAsync(DateOnly day)
    {
        writtenDay = day;
        int ordinal = ++writes;
        var timestamp = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(10 * (ordinal - 1)));
        var asOf = WrittenAsOf.AddSeconds(ordinal);
        var metadata = new MarketAnalyticsSignalMetadata
        {
            SignalKey = new(MarketSeriesIdentity.ForContract(Contract), Indicator, Frame, "benchmark"),
            ContractId = Contract, ValueDate = day, ObservationId = new(Observation), MarketDataAsOfUtc = asOf,
            CalculatedAtUtc = asOf, SourceSequence = ordinal, SchemaVersion = 1, CalculationVersion = "intraday-benchmark",
            CalculationMethod = MarketSignalCalculationMethod.ClosedObservation, IsValid = true
        };
        return Indicator switch
        {
            MarketAnalyticsSignalKind.Rsi => database.InsertFuturesRsiSignalAsync(new()
            {
                ContractId = Contract, ValueDate = day, TimePeriod = Frame, PeriodLength = Period, Timestamp = timestamp,
                Price = 6000m, RSI = 55, SourceSequence = ordinal, SourceEventTimestamp = asOf.UtcDateTime, Metadata = metadata, IsWarm = true
            }),
            MarketAnalyticsSignalKind.Adx => database.InsertFuturesAdxSignalAsync(new()
            {
                ContractId = Contract, ValueDate = day, TimePeriod = Frame, PeriodLength = Period, Timestamp = timestamp,
                FuturesPrice = 6000m, AdxValue = 25, Metadata = metadata, IsWarm = true
            }),
            MarketAnalyticsSignalKind.Atr => database.InsertFuturesAtrSignalAsync(new()
            {
                ContractId = Contract, ValueDate = day, TimePeriod = Frame, PeriodLength = Period, Timestamp = timestamp,
                FuturesPrice = 6000m, AtrValue = 4, Metadata = metadata, IsWarm = true
            }),
            MarketAnalyticsSignalKind.Macd => database.InsertFuturesMacdSignalAsync(new()
            {
                ContractId = Contract, ValueDate = day, TimePeriod = Frame, SignalEmaPeriod = 9, FastEmaPeriod = 12, SlowEmaPeriod = 26,
                Timestamp = timestamp, FuturesPrice = 6000m, MacdLine = 1, Metadata = metadata, IsWarm = true
            }),
            _ => throw new InvalidOperationException("Unexpected indicator.")
        };
    }

    [GlobalCleanup(Targets = [nameof(InsertSignal), nameof(CorrectSignalAsOf)])]
    public async Task VerifyWrites()
    {
        if (writes == 0)
            return;

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var wal = new NpgsqlCommand("SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn);", connection);
        wal.Parameters.Add(new NpgsqlParameter { Value = initialWalPosition });
        var bytes = (decimal)(await wal.ExecuteScalarAsync().ConfigureAwait(false))!;
        await using var count = new NpgsqlCommand(
            $"SELECT count(*), count(*) FILTER (WHERE marketdataasof >= $2) FROM {table} WHERE valuedate = $1;", connection);
        count.Parameters.Add(new NpgsqlParameter { Value = writtenDay });
        count.Parameters.Add(new NpgsqlParameter { Value = WrittenAsOf });
        var corrected = writtenDay == UpdateDay;
        await using (var reader = await count.ExecuteReaderAsync().ConfigureAwait(false))
            if (!await reader.ReadAsync().ConfigureAwait(false) || reader.GetInt64(0) != (corrected ? 2048 : writes)
                || reader.GetInt64(1) != writes)
                throw new InvalidOperationException("Unexpected committed insert or correction count.");

        await VerifyReadAsync(writtenDay, TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(10 * (writes - 1))),
            WrittenAsOf.AddSeconds(writes), writes).ConfigureAwait(false);
        Console.WriteLine($"Write correctness passed for {Indicator} {RowsPerDay} {(corrected ? "correct" : "insert")}: {writes} committed, {bytes:F0} WAL bytes ({bytes / writes:F1} bytes/write).");
    }

    NpgsqlConnection CreateConnection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);

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
            throw new InvalidOperationException("The indicator workload must not allocate a sequence.");

        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken token = default) =>
            throw new InvalidOperationException("The indicator workload must not read a sequence.");
    }
}
