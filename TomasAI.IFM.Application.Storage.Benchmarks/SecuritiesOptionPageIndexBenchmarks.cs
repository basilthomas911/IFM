using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures contract paging and writes using the normal SecuritiesDb entry points.</summary>
[MemoryDiagnoser]
public class SecuritiesOptionPageIndexBenchmarks
{
    const string Symbol = "ES";
    const int RowCount = 120037;
    const int PageSize = 200;
    const int DeepOffset = 108201;
    const int RowsPerExpiry = 1600;
    const string Variants = "AZaz";
    static readonly DateOnly FirstExpiry = new(2026, 1, 16);
    static readonly DateOnly WriteExpiry = new(2029, 3, 16);

    string connectionString = null!;
    string initialWalPosition = null!;
    SecuritiesDbContext database = null!;
    SecuritiesDbContext customDatabase = null!;
    SecuritiesDbContext genericDatabase = null!;
    QualifiedOptionCatalogPage first = null!;
    QualifiedOptionCatalogPage deep = null!;
    int writes;

    [GlobalSetup]
    public async Task Setup()
    {
        connectionString = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");

        database = CreateContext(connectionString);
        builder.Options = "-c plan_cache_mode=force_custom_plan";
        customDatabase = CreateContext(builder.ConnectionString);
        builder.Options = "-c plan_cache_mode=force_generic_plan";
        genericDatabase = CreateContext(builder.ConnectionString);
        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        if (await ScalarAsync(connection, """
            SELECT current_setting('fsync') = 'on' AND current_setting('synchronous_commit') = 'on'
                AND current_setting('full_page_writes') = 'on';
            """).ConfigureAwait(false) is not true)
            throw new InvalidOperationException("Benchmark requires full WAL durability.");

        // The two benchmark arms provision the comparison index separately.
        await ExecuteAsync(connection, SecuritiesSchemaSql.CreateBase).ConfigureAwait(false);
        await SeedAsync(connection).ConfigureAwait(false);
        first = new(new(Symbol, PageSize));
        var firstPage = await ReadFirstPage().ConfigureAwait(false);
        VerifyPage(firstPage, 0);
        var existingFirst = Environment.GetEnvironmentVariable("IFM_OPTION_PAGE_EXISTING_FIRST_CURSOR");
        VerifyPage(await database.GetFuturesOptionContractsPageAsync(new(new(Symbol, PageSize, existingFirst ?? firstPage.ContinuationToken)))
            .ConfigureAwait(false), PageSize);

        var preceding = await database.GetFuturesOptionContractsPageAsync(new(new(Symbol, PageSize, CursorAt(DeepOffset - PageSize - 1))))
            .ConfigureAwait(false);
        VerifyPage(preceding, DeepOffset - PageSize);
        var existingDeep = Environment.GetEnvironmentVariable("IFM_OPTION_PAGE_EXISTING_DEEP_CURSOR");
        deep = new(new(Symbol, PageSize, existingDeep ?? preceding.ContinuationToken));
        var deepPage = await ReadDeepPage().ConfigureAwait(false);
        VerifyPage(deepPage, DeepOffset);
        VerifyPage(await ReadDeepPageCustom().ConfigureAwait(false), DeepOffset);
        VerifyPage(await ReadDeepPageGeneric().ConfigureAwait(false), DeepOffset);
        VerifyPage(await database.GetFuturesOptionContractsPageAsync(new(new(Symbol, PageSize, deepPage.ContinuationToken)))
            .ConfigureAwait(false), DeepOffset + PageSize);
        VerifyPage(await ReadDeepPage().ConfigureAwait(false), DeepOffset);
        VerifyPage(await database.GetFuturesOptionContractsPageAsync(new(new(Symbol, PageSize, CursorAt(RowCount - 38))))
            .ConfigureAwait(false), RowCount - 37);
        if ((await database.GetFuturesOptionContractsPageAsync(new(new("MISSING", PageSize))).ConfigureAwait(false)).Items.Length != 0)
            throw new InvalidOperationException("A missing symbol returned option contracts.");
        await VerifyRejectedCursorAsync(new(new("NQ", PageSize, deep.ContinuationToken))).ConfigureAwait(false);
        await VerifyRejectedCursorAsync(new(new(Symbol, PageSize - 1, deep.ContinuationToken))).ConfigureAwait(false);
        await VerifyRejectedCursorAsync(new(new(Symbol, PageSize, "tampered"))).ConfigureAwait(false);

        Console.WriteLine($"First-page cursor: {firstPage.ContinuationToken}");
        Console.WriteLine($"Deep-page cursor: {preceding.ContinuationToken}");
        if (existingFirst is not null && existingDeep is not null)
        {
            if (existingFirst != firstPage.ContinuationToken || existingDeep != preceding.ContinuationToken)
                throw new InvalidOperationException("The existing paging tokens changed.");
            Console.WriteLine("Baseline first-page and deep-page cursors replayed successfully.");
        }
        await ShowPlansAsync(connection).ConfigureAwait(false);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(',', firstPage.Items.Concat(deepPage.Items).Select(item => item.ContractId))
            + deep.ContinuationToken + deepPage.ContinuationToken)));
        var candidateBytes = await ScalarAsync(connection, """
            SELECT COALESCE(pg_relation_size(COALESCE(
                to_regclass('securities.futures_option_contract_by_symbol_page_bench'),
                to_regclass('securities.futures_option_contract_by_symbol_page_ordered'))), 0);
            """).ConfigureAwait(false);
        Console.WriteLine($"Paging correctness passed. Fingerprint {fingerprint}. Candidate index bytes: {candidateBytes}.");
        writes = 0;
        initialWalPosition = (string)(await ScalarAsync(connection, "SELECT pg_current_wal_insert_lsn()::text;").ConfigureAwait(false))!;
    }

    static async Task SeedAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, $"""
            TRUNCATE securities.futures_option_contract, securities.futures_option_contract_by_symbol;
            INSERT INTO securities.futures_option_contract_by_symbol
                (contractid, description, symbol, localsymbol, securitytype, currency, exchange, multiplier,
                 contractmonth, strikeprice, optiontype)
            SELECT symbol || '-BENCH-' || (ARRAY['A','Z','a','z'])[i % 4 + 1] || '-' || lpad(i::text, 6, '0'),
                'Synthetic futures option', symbol, symbol || ' ' || i, 'FOP', 'USD', 'CME', '50',
                date '2026-01-16' + 7 * (i / {RowsPerExpiry}), 4000 + (i % 800) / 4,
                CASE WHEN i % {RowsPerExpiry} < 800 THEN 'Call' ELSE 'Put' END
            FROM (VALUES ('ES', {RowCount}), ('NQ', 12000)) AS scopes(symbol, size)
            CROSS JOIN LATERAL generate_series(0, size - 1) AS series(i);
            INSERT INTO securities.futures_option_contract
                (contractid, description, symbol, localsymbol, securitytype, currency, exchange, multiplier,
                 contractmonth, strikeprice, optiontype)
            SELECT contractid, description, symbol, localsymbol, securitytype, currency, exchange, multiplier,
                   contractmonth, strikeprice, optiontype
            FROM securities.futures_option_contract_by_symbol;
            """).ConfigureAwait(false);
        await ExecuteAsync(connection, "VACUUM (ANALYZE) securities.futures_option_contract_by_symbol;").ConfigureAwait(false);
        await ExecuteAsync(connection, "VACUUM (ANALYZE) securities.futures_option_contract;").ConfigureAwait(false);
    }

    static string ContractId(int index) => FormattableString.Invariant($"{Symbol}-BENCH-{Variants[index % Variants.Length]}-{index:D6}");
    static DateOnly Expiry(int index) => FirstExpiry.AddDays(7 * (index / RowsPerExpiry));
    static double Strike(int index) => 4000 + (index % 800) / 4;
    static string OptionType(int index) => index % RowsPerExpiry < 800 ? "Call" : "Put";

    static string CursorAt(int index) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
        new SecuritiesDbContext.NativeOptionCursor(Symbol, PageSize, Expiry(index), OptionType(index), Strike(index), ContractId(index))));

    static void VerifyPage(FuturesOptionContractPageReadModel page, int offset)
    {
        var count = Math.Min(PageSize, RowCount - offset);
        if (page.Items.Length != count || page.HasMore != (offset + count < RowCount))
            throw new InvalidOperationException($"Unexpected page length or continuation at offset {offset}.");
        for (var i = 0; i < count; i++)
        {
            var row = page.Items[i];
            var ordinal = offset + i;
            if (row.ContractId != ContractId(ordinal) || row.Symbol != Symbol || row.ContractMonth != Expiry(ordinal)
                || row.StrikePrice != Strike(ordinal) || row.OptionType != OptionType(ordinal))
                throw new InvalidOperationException($"Incorrect paging key at ordinal {ordinal}: {row.ContractId}.");
        }
        if (page.HasMore && page.ContinuationToken != CursorAt(offset + count - 1))
            throw new InvalidOperationException($"The cursor at offset {offset} did not encode the last returned row.");
    }

    async Task VerifyRejectedCursorAsync(QualifiedOptionCatalogPage page)
    {
        try
        {
            await database.GetFuturesOptionContractsPageAsync(page).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("An invalid option cursor was accepted.");
    }

    static async Task ShowPlansAsync(NpgsqlConnection connection)
    {
        var locale = await ScalarAsync(connection, "SELECT datcollate FROM pg_database WHERE datname=current_database();").ConfigureAwait(false);
        Console.WriteLine($"Database collation: {locale}.");
        await ExecuteAsync(connection, "PREPARE option_first_bench(text,integer) AS "
            + SecuritiesDbSql.GetNativeOptionContractsFirstPage + "\n"
            + "PREPARE option_after_bench(text,date,text,double precision,text,integer) AS "
            + SecuritiesDbSql.GetNativeOptionContractsContinuationPage).ConfigureAwait(false);
        var anchor = DeepOffset - 1;
        try
        {
            foreach (var mode in new[] { "force_custom_plan", "force_generic_plan" })
            {
                await ExecuteAsync(connection, $"SET plan_cache_mode = {mode};").ConfigureAwait(false);
                await PrintPlanAsync(connection, mode, "first", "option_first_bench", $"'ES',{PageSize + 1}").ConfigureAwait(false);
                await PrintPlanAsync(connection, mode, "deep", "option_after_bench",
                    FormattableString.Invariant($"'ES','{Expiry(anchor):yyyy-MM-dd}','{OptionType(anchor)}',{Strike(anchor)},'{ContractId(anchor)}',{PageSize + 1}"))
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await ExecuteAsync(connection, "DEALLOCATE option_first_bench; DEALLOCATE option_after_bench; RESET plan_cache_mode;").ConfigureAwait(false);
        }
    }

    static async Task PrintPlanAsync(NpgsqlConnection connection, string mode, string page, string statement, string arguments)
    {
        Console.WriteLine($"Plan for {mode} {page}:");
        await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS) EXECUTE {statement}({arguments});", connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            Console.WriteLine(reader.GetString(0));
    }

    [Benchmark] public Task<FuturesOptionContractPageReadModel> ReadFirstPage() => database.GetFuturesOptionContractsPageAsync(first);
    [Benchmark] public Task<FuturesOptionContractPageReadModel> ReadDeepPage() => database.GetFuturesOptionContractsPageAsync(deep);
    [Benchmark] public Task<FuturesOptionContractPageReadModel> ReadDeepPageCustom() => customDatabase.GetFuturesOptionContractsPageAsync(deep);
    [Benchmark] public Task<FuturesOptionContractPageReadModel> ReadDeepPageGeneric() => genericDatabase.GetFuturesOptionContractsPageAsync(deep);

    [Benchmark]
    public Task InsertContract()
    {
        var strike = 4000 + ++writes;
        return database.InsertFuturesOptionContractAsync(new(
            $"ES20290316C{strike}", "Benchmark contract", Symbol, $"ES {strike}", "FOP", "USD", "CME", "50",
            WriteExpiry, strike, "Call"));
    }

    [GlobalCleanup(Target = nameof(InsertContract))]
    public async Task VerifyWrites()
    {
        if (writes == 0)
            return;

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var wal = new NpgsqlCommand("SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn);", connection);
        wal.Parameters.Add(new NpgsqlParameter { Value = initialWalPosition });
        var bytes = (decimal)(await wal.ExecuteScalarAsync().ConfigureAwait(false))!;
        await using var count = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM securities.futures_option_contract WHERE symbol='ES' AND contractmonth=$1),
                   (SELECT count(*) FROM securities.futures_option_contract_by_symbol WHERE symbol='ES' AND contractmonth=$1);
            """, connection);
        count.Parameters.Add(new NpgsqlParameter { Value = WriteExpiry });
        await using var reader = await count.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false) || reader.GetInt64(0) != writes || reader.GetInt64(1) != writes)
            throw new InvalidOperationException("The write benchmark did not persist the expected contracts in both tables.");
        Console.WriteLine($"Write correctness passed: {writes} committed contracts in both tables, {bytes:F0} WAL bytes ({bytes / writes:F1} bytes/contract).");
    }

    static SecuritiesDbContext CreateContext(string connection)
    {
        var settings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection, connection, "System.Data.Postgres");
        SecuritiesDbContext result = null!;
        var factory = new DbContextFactory(new DbContextResolver(type => type == typeof(IObjectRepository<SecuritiesDbContext>)
            ? result : throw new InvalidOperationException("Unexpected fixture context " + type.Name)));
        result = new(settings, factory, NullLogger<DbProvider>.Instance);
        return result;
    }

    NpgsqlConnection CreateConnection() => new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);

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
