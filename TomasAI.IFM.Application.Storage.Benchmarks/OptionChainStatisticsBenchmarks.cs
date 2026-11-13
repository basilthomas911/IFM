using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class OptionChainStatisticsBenchmarks
{
    const string Table = "market_data.futures_option_chain_quote_data";
    const string VolumeIndex = "futures_option_chain_quote_data_volume_latest";
    const string OpenInterestIndex = "futures_option_chain_quote_data_openinterest_latest";
    const string BaselineSql =
        """
        WITH scope AS (
            SELECT contractid, valuedate, volume, volumevaluedate, openinterest, openinterestvaluedate
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3
        ), volumes AS (
            SELECT DISTINCT ON (contractid) contractid, volume, volumevaluedate FROM scope
            WHERE volume IS NOT NULL AND volume >= 0 AND volumevaluedate IS NOT NULL AND volumevaluedate <= $3
            ORDER BY contractid, volumevaluedate DESC, valuedate DESC
        ), interest AS (
            SELECT DISTINCT ON (contractid) contractid, openinterest, openinterestvaluedate FROM scope
            WHERE openinterest IS NOT NULL AND openinterest >= 0 AND openinterestvaluedate IS NOT NULL
            AND openinterestvaluedate <= $3
            ORDER BY contractid, openinterestvaluedate DESC, valuedate DESC
        )
        SELECT COALESCE(v.contractid, i.contractid), v.volume, v.volumevaluedate, i.openinterest, i.openinterestvaluedate
        FROM volumes v full OUTER JOIN interest i ON v.contractid = i.contractid;
        """;
    const string SeekSql =
        """
        SELECT c.contractid, v.volume, v.volumevaluedate, i.openinterest, i.openinterestvaluedate
        FROM (
            SELECT DISTINCT contractid
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3
        ) c
        LEFT JOIN LATERAL (
            SELECT volume, volumevaluedate
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3 AND contractid = c.contractid
                AND volume >= 0 AND volumevaluedate IS NOT NULL AND volumevaluedate <= $3
            ORDER BY volumevaluedate DESC, valuedate DESC
            LIMIT 1
        ) v ON TRUE
        LEFT JOIN LATERAL (
            SELECT openinterest, openinterestvaluedate
            FROM market_data.futures_option_chain_quote_data
            WHERE underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3 AND contractid = c.contractid
                AND openinterest >= 0 AND openinterestvaluedate IS NOT NULL AND openinterestvaluedate <= $3
            ORDER BY openinterestvaluedate DESC, valuedate DESC
            LIMIT 1
        ) i ON TRUE
        WHERE v.volume IS NOT NULL OR i.openinterest IS NOT NULL;
        """;

    static readonly string NextEligibleContractSql = $"""
        WITH RECURSIVE
        {RecursiveWinners("volumes", "volume")},
        {RecursiveWinners("interest", "openinterest")}
        SELECT COALESCE(v.contractid, i.contractid), v.volume, v.volumevaluedate, i.openinterest, i.openinterestvaluedate
        FROM volumes v FULL OUTER JOIN interest i ON v.contractid = i.contractid;
        """;

    const string OrderedMetricScansSql = MarketDataDbSql.GetOptionChainStatistics;

    static string EligibleMetric(string metric) => $"""
        underlyingcontractid = $1 AND expirydate = $2 AND valuedate <= $3
        AND {metric} >= 0 AND {metric}valuedate IS NOT NULL AND {metric}valuedate <= $3
        """;

    static string RecursiveWinners(string name, string metric)
    {
        var referenceDate = metric + "valuedate";
        var eligible = EligibleMetric(metric);
        var order = $"ORDER BY contractid, {referenceDate} DESC, valuedate DESC LIMIT 1";
        return $"""
            {name} AS (
                (SELECT contractid, {metric}, {referenceDate}
                 FROM {Table}
                 WHERE {eligible}
                 {order})
                UNION ALL
                SELECT next.contractid, next.{metric}, next.{referenceDate}
                FROM {name} previous
                CROSS JOIN LATERAL (
                    SELECT contractid, {metric}, {referenceDate}
                    FROM {Table}
                    WHERE {eligible} AND contractid > previous.contractid
                    {order}
                ) next
            )
            """;
    }

    static readonly DateOnly Cut = new(2026, 10, 14);
    static readonly DateOnly Expiry = new(2026, 11, 20);
    [Params("shallow", "deep")] public string Chain { get; set; } = null!;

    static string ConnectionString()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connection() =>
        new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    internal static async Task PrepareAsync(bool withIndexes, bool verifyMigration = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            ConnectionString(), "System.Data.Postgres");
        var schema = new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
        if (verifyMigration)
        {
            await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
            await VerifyIndexes(db).ConfigureAwait(false);
            if ((await Read(db, OrderedMetricScansSql, "deep", Cut).ConfigureAwait(false)).Length != 0)
                throw new InvalidOperationException("Fresh schema returned rows from an empty chain.");
            Console.WriteLine("Verified both indexes and an empty read on a fresh schema.");
            await Execute(db, $"DROP INDEX market_data.{VolumeIndex}, market_data.{OpenInterestIndex};").ConfigureAwait(false);
        }
        else
        {
            await Execute(db, MarketDataSchemaDb.CreateBaseStatement).ConfigureAwait(false);
        }
        await Execute(db, $"""
            INSERT INTO {Table} (underlyingcontractid,expirydate,valuedate,contractid,
                volume,volumevaluedate,openinterest,openinterestvaluedate,optionprice,underlyingprice)
            SELECT chain,$1,$2::date-age,'OPT-' || id,
                CASE WHEN age%5=0 THEN id+age END, $2::date-age-(id%3),
                CASE WHEN age%7=0 THEN id*100+age END, $2::date-age-(id%5),
                3.25,5800
            FROM (VALUES ('shallow',14),('deep',180),('other-underlying',90)) AS chains(chain,days)
            CROSS JOIN generate_series(1,1024) id CROSS JOIN LATERAL generate_series(0,days-1) age;
            """, [Expiry, Cut]).ConfigureAwait(false);
        await Execute(db, $"""
            INSERT INTO {Table}(underlyingcontractid,expirydate,valuedate,contractid,volume,volumevaluedate,openinterest,openinterestvaluedate)
            SELECT underlying,$1::date+expiry_offset,$2::date+obs,id,volume,$2::date+volume_ref,oi,$2::date+oi_ref
            FROM (VALUES
                ('edges',0,'both',-6,901,-4,801,-3),
                ('edges',0,'both',-5,902,-4,802,-4),
                ('edges',0,'both',-4,903,-5,803,-3),
                ('edges',0,'both',-3,-1,0,804,NULL),
                ('edges',0,'both',-2,904,NULL,-1,0),
                ('edges',0,'both',-1,905,1,805,1),
                ('edges',0,'both',1,906,0,806,0),
                ('edges',0,'volume-only',0,0,0,NULL,0),
                ('edges',0,'interest-only',0,NULL,0,0,0),
                ('edges',0,'neither',0,-1,0,NULL,0),
                ('edges',0,'future-only',1,1,0,1,0),
                ('edges',1,'both',0,999,0,999,0),
                ('other-edges',0,'both',0,999,0,999,0)
            ) AS cases(underlying,expiry_offset,id,obs,volume,volume_ref,oi,oi_ref);
            """, [Expiry, Cut]).ConfigureAwait(false);
        if (withIndexes)
        {
            var before = verifyMigration ? await Fingerprint(db).ConfigureAwait(false) : null;
            await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
            if (verifyMigration)
            {
                var indexes = await VerifyIndexes(db).ConfigureAwait(false);
                if (before != await Fingerprint(db).ConfigureAwait(false))
                    throw new InvalidOperationException("Upgrade changed option quote rows.");
                await schema.CreateAsync(["market_data"]).ConfigureAwait(false);
                var reappliedIndexes = await VerifyIndexes(db).ConfigureAwait(false);
                if (!indexes.SequenceEqual(reappliedIndexes)
                    || before != await Fingerprint(db).ConfigureAwait(false))
                    throw new InvalidOperationException("Reapplying the migration changed index identities or option quote rows.");
                Console.WriteLine("Verified populated upgrade and reapplication with unchanged option quote rows and stable index identities.");
            }
        }
        await Execute(db, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
        foreach (var chain in new[] { "shallow", "deep", "edges", "missing" })
        {
            var before = await Read(db, BaselineSql, chain, Cut).ConfigureAwait(false);
            if (!withIndexes && chain == "deep")
            {
                Console.WriteLine($"Verified baseline deep: {before.Length} results.");
                continue;
            }
            var after = await Read(db, SeekSql, chain, Cut).ConfigureAwait(false);
            if (!before.SequenceEqual(after))
                throw new InvalidOperationException($"Statistics differed for {chain}.");
            if (chain is "shallow" or "deep" && (after.Length != 1024
                || after.Any(row => row.Volume is null || row.OpenInterest is null)))
                throw new InvalidOperationException($"Missing seeded winners for {chain}.");
            if (chain == "edges" && !after.SequenceEqual(new OptionChainStatisticsSnapshot[]
            {
                new("both", 902, Cut.AddDays(-4), 803, Cut.AddDays(-3)),
                new("interest-only", null, null, 0, Cut),
                new("volume-only", 0, Cut, null, null)
            }))
                throw new InvalidOperationException("Independent winners, ties, zero values, or eligibility differed.");
            if (chain == "missing" && after.Length != 0)
                throw new InvalidOperationException("A missing scope returned rows.");
            Console.WriteLine($"Verified {chain}: {after.Length} results.");
        }
        if (withIndexes)
        {
            var historicalBefore = await Read(db, BaselineSql, "deep", Cut.AddDays(-45)).ConfigureAwait(false);
            var historicalAfter = await Read(db, SeekSql, "deep", Cut.AddDays(-45)).ConfigureAwait(false);
            if (historicalBefore.Length != 1024 || !historicalBefore.SequenceEqual(historicalAfter))
                throw new InvalidOperationException("Historical cut changed winners.");
            Console.WriteLine("Verified historical cutoff and independent eligibility, reference-date priority, and observation-date tie-breaks.");
        }
        foreach (var chain in new[] { "shallow", "deep" })
        {
            await Explain(db, "baseline", BaselineSql, chain).ConfigureAwait(false);
            if (withIndexes)
                await Explain(db, "seeks", SeekSql, chain, verifySeeks: verifyMigration).ConfigureAwait(false);
            else
                await Explain(db, "unindexed seeks (estimates only)", SeekSql, chain, analyze: false).ConfigureAwait(false);
        }
    }

    static async Task<long[]> VerifyIndexes(NpgsqlConnection db)
    {
        var ids = new List<long>();
        foreach (var (index, metric) in new[] { (VolumeIndex, "volume"), (OpenInterestIndex, "openinterest") })
        {
            await using var command = new NpgsqlCommand("""
                SELECT indexrelid::bigint, pg_get_indexdef(indexrelid)
                FROM pg_index
                WHERE indexrelid = to_regclass($1) AND indisvalid AND indisready
                """, db);
            PostgresValueBinding.AddParameters(command, [$"market_data.{index}"]);
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                throw new InvalidOperationException($"Missing valid index {index}.");
            var definition = reader.GetString(1);
            if (!definition.Contains($"USING btree (underlyingcontractid, expirydate, contractid, {metric}valuedate DESC, valuedate DESC) INCLUDE ({metric}) WHERE",
                    StringComparison.Ordinal)
                || !definition.Contains($"{metric} >= 0", StringComparison.Ordinal)
                || !definition.Contains($"{metric}valuedate IS NOT NULL", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected definition for {index}: {definition}");
            ids.Add(reader.GetInt64(0));
            Console.WriteLine(definition);
        }
        return ids.ToArray();
    }

    static async Task<string> Fingerprint(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) || ':' || sum(hashtextextended(row_to_json(q)::text, 0)::numeric) FROM {Table} q
            """, db);
        return (string)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    [Benchmark(Baseline = true)] public Task<OptionChainStatisticsSnapshot[]> Baseline() => RunAsync(BaselineSql);
    [Benchmark] public Task<OptionChainStatisticsSnapshot[]> Seeks() => RunAsync(SeekSql);
    [Benchmark] public Task<OptionChainStatisticsSnapshot[]> NextEligibleContract() => RunAsync(NextEligibleContractSql);
    [Benchmark] public Task<OptionChainStatisticsSnapshot[]> OrderedMetricScans() => RunAsync(OrderedMetricScansSql);

    internal static async Task VerifyContractDiscoveryAsync(bool verifyApplied = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        // Keep these extra cases off the timed chains.
        await Execute(db, $"""
            INSERT INTO {Table}(underlyingcontractid,expirydate,valuedate,contractid,volume,volumevaluedate,openinterest,openinterestvaluedate)
            SELECT 'changing',$1,$2::date+obs,id,volume,$2::date+volume_ref,interest,$2::date+interest_ref
            FROM (VALUES
                ('A', -50, 11, -50, NULL, NULL),
                ('A',  -1, 12,   1, NULL, NULL),
                ('B', -50, NULL, NULL, 22, -51),
                ('B', -46, NULL, NULL, 23, -51),
                ('C',  -1, 31,  -1, 32, -1),
                ('D',   1, 41,  -1, 42, -1),
                ('E',  -1, -1,  -1, 51,  1),
                ('F',  -1, 61,  -1, 62, -1)
            ) AS cases(id,obs,volume,volume_ref,interest,interest_ref);
            """, [Expiry, Cut]).ConfigureAwait(false);
        await Execute(db, $"VACUUM (ANALYZE) {Table};").ConfigureAwait(false);
        foreach (var (chain, cutoff, expected) in new[]
        {
            ("shallow", Cut, 1024), ("deep", Cut, 1024), ("deep", Cut.AddDays(-45), 1024),
            ("deep", Cut.AddDays(-170), 1024), ("edges", Cut, 3), ("missing", Cut, 0),
            ("changing", Cut, 4), ("changing", Cut.AddDays(-45), 2), ("changing", Cut.AddDays(-60), 0)
        })
        {
            var current = await Read(db, SeekSql, chain, cutoff).ConfigureAwait(false);
            var recursive = await Read(db, NextEligibleContractSql, chain, cutoff).ConfigureAwait(false);
            var ordered = await Read(db, OrderedMetricScansSql, chain, cutoff).ConfigureAwait(false);
            if (current.Length != expected || !current.SequenceEqual(recursive) || !current.SequenceEqual(ordered))
                throw new InvalidOperationException($"Contract discovery changed results for {chain} at {cutoff:yyyy-MM-dd}.");
            Console.WriteLine($"Verified both discovery candidates for {chain} at {cutoff:yyyy-MM-dd}: {current.Length} matching rows, both values and reference dates.");
        }

        foreach (var chain in new[] { "shallow", "deep" })
        {
            await Explain(db, "current distinct discovery", SeekSql, chain).ConfigureAwait(false);
            await Explain(db, "next eligible contract", NextEligibleContractSql, chain).ConfigureAwait(false);
            await Explain(db, "production ordered metric scans", OrderedMetricScansSql, chain,
                verifyOrderedScans: verifyApplied).ConfigureAwait(false);
        }
    }

    async Task<OptionChainStatisticsSnapshot[]> RunAsync(string sql)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        return await Read(db, sql, Chain, Cut).ConfigureAwait(false);
    }

    static async Task<OptionChainStatisticsSnapshot[]> Read(NpgsqlConnection db, string sql, string chain, DateOnly cut)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, [chain, Expiry, cut]);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var rows = new List<OptionChainStatisticsSnapshot>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            rows.Add(new(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4)));
        return rows.OrderBy(row => row.ContractId, StringComparer.Ordinal).ToArray();
    }

    static async Task Explain(NpgsqlConnection db, string label, string sql, string chain, bool analyze = true,
        bool verifySeeks = false, bool verifyOrderedScans = false)
    {
        Console.WriteLine($"Plan for {label} {chain}:");
        await using var command = new NpgsqlCommand((analyze ? "EXPLAIN (ANALYZE, BUFFERS) " : "EXPLAIN ") + sql, db);
        PostgresValueBinding.AddParameters(command, [chain, Expiry, Cut]);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var plan = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            plan.Add(reader.GetString(0));
        Console.WriteLine(string.Join(Environment.NewLine, plan));
        if ((verifySeeks || verifyOrderedScans) &&
            (!plan.Any(line => line.Contains($"Index Only Scan using {VolumeIndex}", StringComparison.Ordinal))
             || !plan.Any(line => line.Contains($"Index Only Scan using {OpenInterestIndex}", StringComparison.Ordinal))
             || plan.Any(line => line.Contains("Sort Key:", StringComparison.Ordinal))))
            throw new InvalidOperationException($"Expected both covering indexes without sorts for {chain}.");
        if (verifyOrderedScans &&
            (plan.Count(line => line.Contains("->  Unique ", StringComparison.Ordinal)) != 2
             || !plan[0].StartsWith("Merge Full Join ", StringComparison.Ordinal)
             || plan.Any(line => line.Contains("HashAggregate", StringComparison.Ordinal)
                 || line.Contains("Seq Scan", StringComparison.Ordinal)
                 || line.Contains("futures_option_chain_quote_data_pkey", StringComparison.Ordinal))
             || plan.Count(line => line.Trim() == "Heap Fetches: 0") != 2))
            throw new InvalidOperationException($"Expected two ordered index-only scans feeding a merge full join for {chain}.");
    }

    static async Task Execute(NpgsqlConnection db, string sql, object?[]? parameters = null)
    {
        await using var command = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(command, parameters ?? []);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
