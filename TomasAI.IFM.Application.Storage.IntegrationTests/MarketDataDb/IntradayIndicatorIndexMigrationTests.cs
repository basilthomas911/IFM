using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class IntradayIndicatorIndexMigrationTests(ITestOutputHelper output)
{
    static readonly DateOnly Day = new(2026, 11, 10);
    static readonly DateTime WinningAsOf = new(2026, 11, 10, 23, 59, 0, DateTimeKind.Utc);
    static readonly Indicator[] Indicators =
    [
        new("rsi", "periodlength", "14", MarketDataDbSql.GetLastFuturesRsiSignal),
        new("adx", "periodlength", "14", MarketDataDbSql.GetLastFuturesAdxSignal),
        new("atr", "periodlength", "14", MarketDataDbSql.GetLastFuturesAtrSignal),
        new("macd", "signalemaperiod, fastemaperiod, slowemaperiod", "9, 12, 26", MarketDataDbSql.GetLastFuturesMacdSignal)
    ];

    [Fact]
    public async Task Market_data_schema_adds_intraday_indexes_to_populated_and_fresh_databases()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("intraday-indicator-indexes", includeCql: false);
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var schema = new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await using var connection = new PostgresObjectDataRepositoryConnection()
            .As<NpgsqlConnection>(infrastructure.PostgresConnectionString);
        await connection.OpenAsync();

        await ExecuteAsync(connection, MarketDataSchemaDb.CreateBaseStatement);
        var fingerprints = new Dictionary<string, string>();
        foreach (var indicator in Indicators)
        {
            Assert.Equal(0, await IndexIdAsync(connection, indicator));
            await SeedAsync(connection, indicator);
            fingerprints.Add(indicator.Name, await FingerprintAsync(connection, indicator));
            await AssertLatestResultsAsync(connection, indicator);
            var before = await ExplainAsync(connection, indicator);
            Assert.Contains("Sort Key: marketdataasof DESC NULLS LAST", before, StringComparison.Ordinal);
            output.WriteLine($"{indicator.Name} before migration\n{before}");
        }

        await schema.CreateAsync(["market_data"]);
        var indexes = new Dictionary<string, long>();
        foreach (var indicator in Indicators)
        {
            indexes.Add(indicator.Name, await AssertIndexAsync(connection, indicator));
            Assert.Equal(fingerprints[indicator.Name], await FingerprintAsync(connection, indicator));
            await AssertLatestResultsAsync(connection, indicator);
            var after = await ExplainAsync(connection, indicator);
            Assert.Contains($"Index Scan using {indicator.Index}", after, StringComparison.Ordinal);
            Assert.DoesNotContain("Sort Key:", after, StringComparison.Ordinal);
            Assert.Contains("rows=1 loops=1", after, StringComparison.Ordinal);
            output.WriteLine($"{indicator.Name} after migration\n{after}");
        }

        await schema.CreateAsync(["market_data"]);
        foreach (var indicator in Indicators)
        {
            Assert.Equal(indexes[indicator.Name], await AssertIndexAsync(connection, indicator));
            Assert.Equal(fingerprints[indicator.Name], await FingerprintAsync(connection, indicator));
        }

        // Recreate only the fixture schema to exercise the clean install path independently of the upgrade.
        await schema.RecreateAsync(["market_data"]);
        foreach (var indicator in Indicators)
        {
            await AssertIndexAsync(connection, indicator);
            await using var command = ReadCommand(connection, indicator, Day);
            Assert.Null(await command.ExecuteScalarAsync());
        }
    }

    static async Task SeedAsync(NpgsqlConnection connection, Indicator indicator)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO market_data.{indicator.Table}
                (contractid, timeperiod, {indicator.PeriodColumns}, valuedate, timestamp, marketdataasof, sourcesequence)
            SELECT 'ES', 'TenSeconds', {indicator.PeriodValues}, date '2026-11-10', time '00:00:00' + n * interval '10 seconds',
                CASE WHEN n IN (1, 2) THEN timestamptz '2026-11-10 23:59:00+00'
                     WHEN n = 8191 THEN NULL
                     ELSE timestamptz '2026-11-10 00:00:00+00' + n * interval '10 seconds' END, n
            FROM generate_series(0, 8191) AS rows(n);
            INSERT INTO market_data.{indicator.Table}
                (contractid, timeperiod, {indicator.PeriodColumns}, valuedate, timestamp, marketdataasof, sourcesequence)
            VALUES ('ES', 'TenSeconds', {indicator.PeriodValues}, date '2026-11-11', time '00:00:00', NULL, 0),
                   ('ES', 'TenSeconds', {indicator.PeriodValues}, date '2026-11-11', time '00:00:10', NULL, 1),
                   ('NQ', 'TenSeconds', {indicator.PeriodValues}, date '2026-11-10', time '23:59:00',
                    timestamptz '2026-11-11 00:00:00+00', 9000);
            """);
        await ExecuteAsync(connection, $"VACUUM (ANALYZE) market_data.{indicator.Table};");
    }

    static async Task AssertLatestResultsAsync(NpgsqlConnection connection, Indicator indicator)
    {
        await AssertWinnerAsync(connection, indicator, Day, new TimeOnly(0, 0, 20), WinningAsOf, 2);
        await AssertWinnerAsync(connection, indicator, Day.AddDays(1), new TimeOnly(0, 0, 10), null, 1);
        await using var missing = ReadCommand(connection, indicator, Day.AddDays(-1));
        Assert.Null(await missing.ExecuteScalarAsync());
    }

    static async Task AssertWinnerAsync(NpgsqlConnection connection, Indicator indicator, DateOnly day,
        TimeOnly time, DateTime? asOf, long sequence)
    {
        await using var command = ReadCommand(connection, indicator, day);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("ES", reader.GetString(reader.GetOrdinal("ContractId")));
        Assert.Equal(day, reader.GetFieldValue<DateOnly>(reader.GetOrdinal("ValueDate")));
        Assert.Equal(time, reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("Timestamp")));
        var asOfColumn = reader.GetOrdinal("MarketDataAsOf");
        Assert.Equal(asOf, reader.IsDBNull(asOfColumn) ? null : reader.GetDateTime(asOfColumn));
        Assert.Equal(sequence, reader.GetInt64(reader.GetOrdinal("SourceSequence")));
        Assert.False(await reader.ReadAsync());
    }

    static NpgsqlCommand ReadCommand(NpgsqlConnection connection, Indicator indicator, DateOnly day, string prefix = "")
    {
        var command = new NpgsqlCommand(prefix + indicator.Query, connection);
        object[] parameters = indicator.Name == "macd" ? ["ES", "TenSeconds", 9, 12, 26, day] : ["ES", "TenSeconds", 14, day];
        foreach (var value in parameters)
            command.Parameters.Add(new NpgsqlParameter { Value = value });
        return command;
    }

    static async Task<string> ExplainAsync(NpgsqlConnection connection, Indicator indicator)
    {
        await using var command = ReadCommand(connection, indicator, Day, "EXPLAIN (ANALYZE, BUFFERS) ");
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync())
            lines.Add(reader.GetString(0));
        return string.Join('\n', lines);
    }

    static async Task<long> IndexIdAsync(NpgsqlConnection connection, Indicator indicator)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT COALESCE(to_regclass('market_data.{indicator.Index}')::oid::bigint, 0);", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    static async Task<long> AssertIndexAsync(NpgsqlConnection connection, Indicator indicator)
    {
        var oid = await IndexIdAsync(connection, indicator);
        Assert.True(oid > 0);
        await using var command = new NpgsqlCommand("""
            SELECT indisvalid AND indisready, pg_get_indexdef(indexrelid)
            FROM pg_index WHERE indexrelid::bigint = $1;
            """, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = oid });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Contains($"ON market_data.{indicator.Table} USING btree (contractid, timeperiod, " +
            $"{indicator.PeriodColumns}, valuedate, marketdataasof DESC NULLS LAST, \"timestamp\" DESC)",
            reader.GetString(1), StringComparison.Ordinal);
        return oid;
    }

    static async Task<string> FingerprintAsync(NpgsqlConnection connection, Indicator indicator)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) || ':' || md5(string_agg(md5(to_jsonb(t)::text), '' ORDER BY contractid, valuedate, timestamp))
            FROM market_data.{indicator.Table} t;
            """, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    sealed record Indicator(string Name, string PeriodColumns, string PeriodValues, string Query)
    {
        public string Table => $"futures_{Name}_signal";
        public string Index => $"{Table}_latest_intraday";
    }
}
