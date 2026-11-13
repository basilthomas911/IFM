using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

[MemoryDiagnoser]
public class AvailableCashBenchmarks
{
    const string CurrentSql =
        """
        SELECT (SELECT COALESCE(SUM(b.balance),0) FROM portfolio_financial.ledger_account_balance b
        WHERE b.book_id=$1 AND b.fund_id=$2 AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a
          WHERE a.book_id=b.book_id AND a.account_id=b.account_id AND a.category='Cash')) - (SELECT COALESCE(SUM(amount),
            0) FROM portfolio_financial.financial_encumbrance WHERE book_id=$1 AND fund_id=$2
            AND status='Pending') - (WITH paid AS (
          SELECT f.reservation_id,f.component,SUM(GREATEST(0,f.cash_paid-COALESCE((
            SELECT SUM(e.debit-e.credit) FROM portfolio_financial.ledger_journal reversal
              JOIN portfolio_financial.ledger_entry e ON e.journal_id=reversal.journal_id
            WHERE reversal.reversal_journal_id=j.journal_id AND e.fund_id=$2 AND EXISTS(
              SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id
              AND a.category='Cash')),0))) amount
          FROM portfolio_financial.capacity_funding_receipt f JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
          WHERE j.book_id=$1 AND j.fund_id=$2 GROUP BY f.reservation_id,f.component),
        holds AS (
          SELECT r.reservation_id,(r.remaining_units+r.filled_units-r.closed_units)::numeric/r.strategy_units fraction,
            COALESCE((r.requirements->>'SettlementCash')::numeric,0) settlement,
            COALESCE((r.requirements->>'MarginFunding')::numeric,0) margin,
            COALESCE((r.requirements->>'FeeReserve')::numeric,0) fee,
            COALESCE((r.requirements->>'VariationReserve')::numeric,0) variation
          FROM portfolio_financial.capacity_reservation r WHERE r.book_id=$1 AND r.fund_id=$2 AND r.status NOT IN (8,9))
        SELECT COALESCE(SUM(ceil(100*(GREATEST(0,h.settlement*h.fraction-COALESCE(s.amount,0))+
          GREATEST(0,h.margin*h.fraction-COALESCE(m.amount,0))+GREATEST(0,h.fee*h.fraction-COALESCE(f.amount,
            0))+h.variation*h.fraction))/100),0)
        FROM holds h LEFT JOIN paid s ON s.reservation_id=h.reservation_id AND s.component=1
          LEFT JOIN paid f ON f.reservation_id=h.reservation_id AND f.component=2
          LEFT JOIN paid m ON m.reservation_id=h.reservation_id AND m.component=3);
        """;
    const int ActiveReservations = 8;
    const int SparseReservations = 24;
    const int DenseReservations = 2048;

    // Keep the baseline balance, encumbrance and rounding expressions verbatim.
    static readonly string ReversalsSql = Rewrite(preAggregate: true, activeOnly: false);
    static readonly string ActiveSql = Rewrite(preAggregate: false, activeOnly: true);
    const string CombinedSql = PortfolioDbSql.Financial_GeneralLedgerStore_AvailableCash;

    [Params(1, 2)] public int Fund { get; set; }

    static string Rewrite(bool preAggregate, bool activeOnly)
    {
        var start = CurrentSql.IndexOf("WITH paid AS (", StringComparison.Ordinal);
        var end = CurrentSql.IndexOf("SELECT COALESCE(SUM(ceil", StringComparison.Ordinal);
        var holdsStart = CurrentSql.IndexOf("holds AS (", StringComparison.Ordinal);
        var holds = CurrentSql[holdsStart..end].Trim();
        var filter = activeOnly ? "JOIN holds h ON h.reservation_id=f.reservation_id" : "";
        var funding = $"""
            funding AS MATERIALIZED (
                SELECT j.journal_id,f.reservation_id,f.component,f.cash_paid
                FROM portfolio_financial.capacity_funding_receipt f
                JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
                {filter}
                WHERE j.book_id=$1 AND j.fund_id=$2
            )
            """;
        const string cashEntries = """
            JOIN portfolio_financial.ledger_entry e ON e.journal_id=reversal.journal_id
            """;
        const string eligibleCash = """
            e.fund_id=$2 AND EXISTS (
                SELECT 1 FROM portfolio_financial.ledger_account a
                WHERE a.book_id=e.book_id AND a.account_id=e.account_id AND a.category='Cash')
            """;
        var paid = preAggregate
            ? $"""
                {funding},
                reversed_cash AS (
                    SELECT reversal.reversal_journal_id AS journal_id, SUM(e.debit-e.credit) AS amount
                    FROM funding f
                    JOIN portfolio_financial.ledger_journal reversal ON reversal.reversal_journal_id=f.journal_id
                    {cashEntries}
                    WHERE {eligibleCash}
                    GROUP BY reversal.reversal_journal_id
                ),
                paid AS (
                    SELECT f.reservation_id,f.component,SUM(GREATEST(0,f.cash_paid-COALESCE(r.amount,0))) AS amount
                    FROM funding f LEFT JOIN reversed_cash r ON r.journal_id=f.journal_id
                    GROUP BY f.reservation_id,f.component
                )
                """
            : $"""
                paid AS (
                    SELECT f.reservation_id,f.component,SUM(GREATEST(0,f.cash_paid-COALESCE((
                        SELECT SUM(e.debit-e.credit)
                        FROM portfolio_financial.ledger_journal reversal
                        {cashEntries}
                        WHERE reversal.reversal_journal_id=j.journal_id AND {eligibleCash}
                    ),0))) AS amount
                    FROM portfolio_financial.capacity_funding_receipt f
                    JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
                    {filter}
                    WHERE j.book_id=$1 AND j.fund_id=$2
                    GROUP BY f.reservation_id,f.component
                )
                """;
        return CurrentSql[..start] + $"WITH {holds}, {paid}\n" + CurrentSql[end..];
    }

    static string ConnectionString()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION"));
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Provision the disposable qualification database first.");
        return settings.ConnectionString;
    }

    static NpgsqlConnection Connection() => new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(ConnectionString());

    internal static async Task PrepareAsync(bool verifyApplied = false)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            ConnectionString(), "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync().ConfigureAwait(false);
        await Execute(db, PortfolioSchemaDb.FinancialCreateStatement).ConfigureAwait(false);
        // The single seed transaction also exercises the schema's deferred balance and new-journal checks.
        await Execute(db, SeedSql).ConfigureAwait(false);
        foreach (var table in new[] { "ledger_account", "ledger_account_balance", "financial_encumbrance",
                     "capacity_reservation", "capacity_funding_receipt", "ledger_journal", "ledger_entry" })
            await Execute(db, $"VACUUM (ANALYZE) portfolio_financial.{table}").ConfigureAwait(false);

        (string Name, string Sql)[] variants = verifyApplied
            ? [("Previous", CurrentSql), ("Applied", CombinedSql)]
            : [("Previous", CurrentSql), ("ReversalAggregate", ReversalsSql), ("ActiveOnly", ActiveSql), ("Combined", CombinedSql)];
        foreach (var (name, sql) in variants)
        {
            foreach (var (book, fund, expected) in new[] { (1, 1, ExpectedCash()), (1, 2, ExpectedCash()),
                         (1, 3, -5.17m), (1, 4, 0m), (2, 1, 77.77m) })
            {
                var actual = await Read(db, sql, book, fund).ConfigureAwait(false);
                if (actual != expected)
                    throw new InvalidOperationException($"{name} book {book} fund {fund}: {actual} != {expected}.");
                Console.WriteLine(FormattableString.Invariant($"EXACT,{name},{book},{fund},{actual:F2}"));
            }
            foreach (var fund in new[] { 1, 2 })
                await Explain(db, name, sql, fund, verifyApplied && name == "Applied").ConfigureAwait(false);
        }
    }

    static decimal ExpectedCash()
    {
        var cash = 10_000.11m - 17.23m;
        for (var reservation = 1; reservation <= ActiveReservations; reservation++)
        {
            var fraction = reservation % 2 == 0 ? 0.5m : 0.75m;
            var settled = 20m - (reservation % 3 == 0 ? 25m : reservation % 3 == 1 ? 8m : 0m);
            var margin = 5m - (reservation % 3 == 0 ? 25m : reservation % 3 == 1 ? 8m : 0m);
            var fees = reservation % 2 == 0 ? 2m : 0m;
            var held = Math.Max(0m, 51.01m * fraction - Math.Max(0m, settled))
                + Math.Max(0m, 14.03m * fraction - Math.Max(0m, margin))
                + Math.Max(0m, 4.07m * fraction - fees) + 1.11m * fraction;
            cash -= Math.Ceiling(100m * held) / 100m;
        }
        return cash;
    }

    [Benchmark(Baseline = true)] public Task<decimal> Current() => Run(CurrentSql);
    [Benchmark] public Task<decimal> ReversalAggregate() => Run(ReversalsSql);
    [Benchmark] public Task<decimal> ActiveOnly() => Run(ActiveSql);
    [Benchmark] public Task<decimal> Combined() => Run(CombinedSql);

    async Task<decimal> Run(string sql)
    {
        await using var db = Connection();
        await db.OpenAsync().ConfigureAwait(false);
        return await Read(db, sql, 1, Fund).ConfigureAwait(false);
    }

    static async Task<decimal> Read(NpgsqlConnection db, string sql, int book, int fund)
    {
        await using var cmd = new NpgsqlCommand(sql, db);
        PostgresValueBinding.AddParameters(cmd, [book, fund]);
        return (decimal)(await cmd.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    static async Task Explain(NpgsqlConnection db, string name, string sql, int fund, bool verifyApplied)
    {
        Console.WriteLine($"PLAN,{name},{fund}");
        await using var cmd = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, TIMING OFF) " + sql, db);
        PostgresValueBinding.AddParameters(cmd, [1, fund]);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        var plan = new List<string>();
        while (await reader.ReadAsync().ConfigureAwait(false))
            plan.Add(reader.GetString(0));
        Console.WriteLine(string.Join(Environment.NewLine, plan));
        if (!verifyApplied)
            return;
        var reversalScans = plan.Where(line => line.Contains("Scan on ledger_journal reversal ", StringComparison.Ordinal)).ToArray();
        if (reversalScans.Length != 1 || !reversalScans[0].Contains("loops=1)", StringComparison.Ordinal)
            || !plan.Any(line => line.Contains("Group Key: reversal.reversal_journal_id", StringComparison.Ordinal))
            || !plan.Any(line => line.Contains("CTE Scan on funding ", StringComparison.Ordinal)
                && line.Contains("actual rows=20 loops=1", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Fund {fund} did not use the expected set-based reversal and active funding plan.");
        Console.WriteLine($"VERIFIED-PLAN,{fund},20 active funding receipts,one reversal scan");
    }

    static async Task Execute(NpgsqlConnection db, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, db) { CommandTimeout = 180 };
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    static readonly string SeedSql = $"""
        BEGIN;
        INSERT INTO portfolio_financial.ledger_book VALUES
            (1,md5('entity1')::uuid,1,'USD','account1','test',1,'Active'),
            (2,md5('entity2')::uuid,2,'USD','account2','test',1,'Active');
        INSERT INTO portfolio_financial.ledger_account
        SELECT b,a,v,CASE a WHEN 1 THEN 'Cash' ELSE 'Expense' END,1,'USD',true,'Active','fixture'
        FROM generate_series(1,2) b CROSS JOIN generate_series(1,2) a CROSS JOIN generate_series(1,2) v;
        INSERT INTO portfolio_financial.ledger_posting_rule
        SELECT b,md5('rule')::uuid,1,1,jsonb_build_object(),'fixture','Active',date '2026-01-01',NULL FROM generate_series(1,2) b;
        INSERT INTO portfolio_financial.ledger_account_balance VALUES
            (1,1,1,'USD',10000.11,0,10000.11,1),(1,1,2,'USD',10000.11,0,10000.11,1),
            (1,2,2,'USD',9000,0,9000,1),(2,1,1,'USD',77.77,0,77.77,1);
        INSERT INTO portfolio_financial.financial_encumbrance
        SELECT md5(fund||status)::uuid,1,1,fund,'fixture','fixture',
            CASE WHEN fund=3 THEN 5.17 ELSE 17.23 END,'USD',status,1,jsonb_build_object()
        FROM generate_series(1,3) fund CROSS JOIN (VALUES ('Pending'),('Settled'),('Cancelled')) s(status);

        CREATE TEMP TABLE seed_reservation ON COMMIT DROP AS
        SELECT fund,n,md5('reservation:'||fund||':'||n)::uuid AS id
        FROM (VALUES (1,{SparseReservations}),(2,{DenseReservations}),(3,1)) v(fund,count)
        CROSS JOIN LATERAL generate_series(1,count) n;
        INSERT INTO portfolio_financial.capacity_reservation
            (reservation_id,portfolio_id,operation_id,fund_id,book_id,order_id,candidate_hash,risk_hash,sized_order_hash,
             strategy_units,requirements,environment,expires_at_utc,status,version,completion_event_id,request,receipt,
             input_hash,filled_units,cancelled_units,remaining_units,closed_units)
        SELECT id,1,id,fund,1,fund*10000+n,'fixture','fixture','fixture',4,
            jsonb_build_object('SettlementCash',51.01,'MarginFunding',14.03,'FeeReserve',4.07,'VariationReserve',1.11),
            'test',timestamptz '2026-10-14 12:00+00',
            CASE WHEN fund<>3 AND n<={ActiveReservations} THEN 2 WHEN n%2=0 THEN 8 ELSE 9 END,
            1,id,jsonb_build_object(),jsonb_build_object(),'fixture',2,1,1,CASE WHEN n%2=0 THEN 1 ELSE 0 END
        FROM seed_reservation;

        CREATE TEMP TABLE seed_funding ON COMMIT DROP AS
        SELECT r.*,c,1000000::bigint*fund+10*n+c AS tx,
            CASE c WHEN 1 THEN 20 WHEN 2 THEN 2 ELSE 5 END AS paid
        FROM seed_reservation r CROSS JOIN generate_series(1,3) c WHERE c<>2 OR n%2=0;
        CREATE TEMP TABLE seed_journal ON COMMIT DROP AS
        SELECT tx,fund,0::bigint AS original,paid AS amount FROM seed_funding
        UNION ALL
        SELECT tx+10000000*k,fund,tx,CASE WHEN n%3=0 THEN (ARRAY[12,13])[k] ELSE (ARRAY[3,5])[k] END
        FROM seed_funding CROSS JOIN generate_series(1,2) k WHERE n%3<>2 AND c<>2;
        INSERT INTO portfolio_financial.ledger_transaction
            (transaction_id,book_id,portfolio_id,fund_id,operation_id,item_ordinal,kind,source_system,source_event_key,
             source_hash,amount,currency,accounting_date,value_date,payload)
        SELECT tx,1,1,fund,md5('tx:'||tx)::uuid,1,1,'fixture',tx::text,'fixture',amount,'USD',
            date '2026-10-13',date '2026-10-13',jsonb_build_object() FROM seed_journal;
        INSERT INTO portfolio_financial.ledger_journal
            (journal_id,transaction_id,book_id,portfolio_id,fund_id,operation_id,accounting_date,value_date,kind,
             source_hash,rule_id,rule_version,reversal_journal_id,committed_at_utc,financial_revision,journal_hash)
        SELECT tx,tx,1,1,fund,md5('tx:'||tx)::uuid,date '2026-10-13',date '2026-10-13',1,'fixture',
            md5('rule')::uuid,1,NULLIF(original,0),timestamptz '2026-10-13 12:00+00',tx,'fixture' FROM seed_journal;
        INSERT INTO portfolio_financial.ledger_entry
            (journal_id,ordinal,book_id,account_id,account_version,fund_id,debit,credit,currency,source_line_reference)
        SELECT tx,line,1,CASE WHEN line IN (1,3) THEN 1 ELSE 2 END,1,
            CASE WHEN line IN (3,4) THEN 99 ELSE fund END,
            CASE WHEN (line=1 AND original>0) OR (line=2 AND original=0) THEN amount WHEN line=3 THEN 1 ELSE 0 END,
            CASE WHEN (line=2 AND original>0) OR (line=1 AND original=0) THEN amount WHEN line=4 THEN 1 ELSE 0 END,
            'USD','fixture'
        FROM seed_journal CROSS JOIN generate_series(1,4) line;
        INSERT INTO portfolio_financial.capacity_funding_receipt
        SELECT tx,id,c,paid FROM seed_funding;
        COMMIT;
        """;
}
