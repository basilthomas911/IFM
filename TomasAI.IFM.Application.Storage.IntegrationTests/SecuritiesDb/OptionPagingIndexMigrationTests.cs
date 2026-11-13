using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

public sealed class OptionPagingIndexMigrationTests(ITestOutputHelper output)
{
    const string Index = "futures_option_contract_by_symbol_page_ordered";
    const int PageSize = 200;
    const int DeepOffset = 36001;

    [Fact]
    public async Task Securities_schema_adds_the_paging_index_and_preserves_existing_cursors()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("option-paging-index", includeCql: false);
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings()
            .Add(SecuritiesDbContext.SecuritiesDbConnection, infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        var schema = new SecuritiesSchemaDb(settings, logger);
        var factory = Substitute.For<IDbContextFactory>();
        var db = new SecuritiesDbContext(settings, factory, logger);
        factory.SecuritiesDb.Returns(db);

        await using var connection = new PostgresObjectDataRepositoryConnection()
            .As<NpgsqlConnection>(infrastructure.PostgresConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, SecuritiesSchemaSql.CreateBase);
        Assert.Equal(0, await IndexIdAsync(connection));
        await SeedAsync(connection);
        var rowsBefore = await FingerprintsAsync(connection);

        var firstRequest = new QualifiedOptionCatalogPage(new("ES", PageSize));
        var firstBefore = await db.GetFuturesOptionContractsPageAsync(firstRequest);
        var preceding = await db.GetFuturesOptionContractsPageAsync(new(new("ES", PageSize, CursorToken(DeepOffset - PageSize - 1))));
        Assert.Equal(CursorToken(DeepOffset - 1), preceding.ContinuationToken);
        var deepRequest = new QualifiedOptionCatalogPage(new("ES", PageSize, preceding.ContinuationToken));
        var deepBefore = await db.GetFuturesOptionContractsPageAsync(deepRequest);
        AssertPage(firstBefore, 0);
        AssertPage(deepBefore, DeepOffset);
        var beforePlans = await CapturePlansAsync(connection);
        Assert.All(beforePlans.Values, plan => Assert.Contains("Sort Key:", plan, StringComparison.Ordinal));

        await schema.CreateAsync(["securities"]);
        var indexId = await AssertIndexAsync(connection);
        Assert.Equal(rowsBefore, await FingerprintsAsync(connection));
        var afterPlans = await CapturePlansAsync(connection);
        Assert.All(afterPlans.Values, plan =>
        {
            Assert.Contains($"Index Scan using {Index}", plan, StringComparison.Ordinal);
            Assert.DoesNotContain("Sort Key:", plan, StringComparison.Ordinal);
        });
        foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
        {
            var deep = afterPlans[(mode, "deep")];
            Assert.Contains("AND (ROW(contractmonth", deep, StringComparison.Ordinal);
            Assert.DoesNotContain("Rows Removed by Filter:", deep, StringComparison.Ordinal);
        }

        var firstAfter = await db.GetFuturesOptionContractsPageAsync(firstRequest);
        var deepAfter = await db.GetFuturesOptionContractsPageAsync(deepRequest);
        Assert.Equal(firstBefore.Items, firstAfter.Items);
        Assert.Equal(firstBefore.ContinuationToken, firstAfter.ContinuationToken);
        Assert.Equal(deepBefore.Items, deepAfter.Items);
        Assert.Equal(deepBefore.ContinuationToken, deepAfter.ContinuationToken);
        AssertPage(await db.GetFuturesOptionContractsPageAsync(new(new("ES", PageSize, deepBefore.ContinuationToken))),
            DeepOffset + PageSize);

        await schema.CreateAsync(["securities"]);
        Assert.Equal(indexId, await AssertIndexAsync(connection));
        Assert.Equal(rowsBefore, await FingerprintsAsync(connection));

        // Only the disposable fixture schema is recreated for the fresh-install check.
        await schema.RecreateAsync(["securities"]);
        await AssertIndexAsync(connection);
        var empty = await db.GetFuturesOptionContractsPageAsync(firstRequest);
        Assert.Empty(empty.Items);
        Assert.Null(empty.ContinuationToken);
    }

    static async Task SeedAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, """
            INSERT INTO securities.futures_option_contract_by_symbol
                (contractid, symbol, description, localsymbol, securitytype, currency, exchange, multiplier,
                 contractmonth, optiontype, strikeprice)
            SELECT symbol || '-' || (ARRAY['A','Z','a','z'])[i % 4 + 1] || '-' || lpad(i::text, 5, '0'),
                symbol, 'Fixture option', 'Fixture', 'FOP', 'USD', 'CME', '50',
                date '2026-01-16' + 7 * (i / 1600),
                CASE WHEN i % 1600 < 800 THEN 'Call' ELSE 'Put' END, 4000 + (i % 800) / 4
            FROM (VALUES ('ES', 40037), ('NQ', 4000)) AS scopes(symbol, size)
            CROSS JOIN LATERAL generate_series(0, size - 1) AS series(i);
            INSERT INTO securities.futures_option_contract
                (contractid, symbol, description, localsymbol, securitytype, currency, exchange, multiplier,
                 contractmonth, optiontype, strikeprice)
            SELECT contractid, symbol, description, localsymbol, securitytype, currency, exchange, multiplier,
                   contractmonth, optiontype, strikeprice
            FROM securities.futures_option_contract_by_symbol;
            """);
        await ExecuteAsync(connection, "VACUUM (ANALYZE) securities.futures_option_contract_by_symbol;");
    }

    static SecuritiesDbContext.NativeOptionCursor Cursor(int ordinal) =>
        new("ES", PageSize, new DateOnly(2026, 1, 16).AddDays(7 * (ordinal / 1600)),
            ordinal % 1600 < 800 ? "Call" : "Put", 4000 + ordinal % 800 / 4,
            FormattableString.Invariant($"ES-{"AZaz"[ordinal % 4]}-{ordinal:D5}"));

    static string CursorToken(int ordinal) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Cursor(ordinal)));

    static void AssertPage(FuturesOptionContractPageReadModel page, int offset)
    {
        Assert.Equal(Enumerable.Range(offset, PageSize).Select(i => Cursor(i).ContractId), page.Items.Select(item => item.ContractId));
        Assert.All(page.Items, item => Assert.Equal("ES", item.Symbol));
        Assert.Equal(CursorToken(offset + PageSize - 1), page.ContinuationToken);
    }

    async Task<Dictionary<(string Mode, string Page), string>> CapturePlansAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, "PREPARE option_first_migration(text,integer) AS "
            + SecuritiesDbSql.GetNativeOptionContractsFirstPage + "\n"
            + "PREPARE option_after_migration(text,date,text,double precision,text,integer) AS "
            + SecuritiesDbSql.GetNativeOptionContractsContinuationPage);
        var plans = new Dictionary<(string, string), string>();
        var cursor = Cursor(DeepOffset - 1);
        try
        {
            foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
            {
                await ExecuteAsync(connection, $"SET plan_cache_mode = {mode};");
                await ReadPlanAsync(mode, "first", "option_first_migration", $"'ES',{PageSize + 1}");
                await ReadPlanAsync(mode, "deep", "option_after_migration", FormattableString.Invariant(
                    $"'ES','{cursor.Month:yyyy-MM-dd}','{cursor.OptionType}',{cursor.Strike},'{cursor.ContractId}',{PageSize + 1}"));
            }
        }
        finally
        {
            await ExecuteAsync(connection, "DEALLOCATE option_first_migration; DEALLOCATE option_after_migration; RESET plan_cache_mode;");
        }
        return plans;

        async Task ReadPlanAsync(string mode, string page, string statement, string arguments)
        {
            await using var command = new NpgsqlCommand(
                $"EXPLAIN (ANALYZE, BUFFERS) EXECUTE {statement}({arguments});", connection);
            await using var reader = await command.ExecuteReaderAsync();
            var lines = new List<string>();
            while (await reader.ReadAsync())
                lines.Add(reader.GetString(0));
            var plan = string.Join('\n', lines);
            plans.Add((mode, page), plan);
            output.WriteLine($"{mode} {page}\n{plan}");
        }
    }

    static async Task<long> IndexIdAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT COALESCE(to_regclass('securities.{Index}')::oid::bigint, 0);", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    static async Task<long> AssertIndexAsync(NpgsqlConnection connection)
    {
        var oid = await IndexIdAsync(connection);
        Assert.True(oid > 0);
        await using var command = new NpgsqlCommand("""
            SELECT indisvalid AND indisready, pg_get_indexdef(indexrelid)
            FROM pg_index WHERE indexrelid::bigint = $1;
            """, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = oid });
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Contains("""
            ON securities.futures_option_contract_by_symbol USING btree (symbol, contractmonth, optiontype COLLATE "C", strikeprice, contractid COLLATE "C")
            """, reader.GetString(1), StringComparison.Ordinal);
        return oid;
    }

    static async Task<(string Canonical, string Projection)> FingerprintsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
              (SELECT count(*) || ':' || md5(string_agg(md5(to_jsonb(t)::text), '' ORDER BY contractid COLLATE "C"))
               FROM securities.futures_option_contract t),
              (SELECT count(*) || ':' || md5(string_agg(md5(to_jsonb(t)::text), '' ORDER BY contractid COLLATE "C"))
               FROM securities.futures_option_contract_by_symbol t);
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1));
    }

    static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
