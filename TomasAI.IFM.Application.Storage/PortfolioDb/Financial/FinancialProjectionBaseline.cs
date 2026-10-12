using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Explicit offline migration baseline. Never dispatches commands, clears tables or fabricates missing journal facts.</summary>
public sealed class FinancialProjectionBaseline(IPostgresEventTransaction transactions)
{
    // Dependency order. Receipts retain their original event-log references; that log must be copied before reconstruction.
    static readonly string[] Tables = ["ledger_book","ledger_account","ledger_posting_rule","financial_authority",
        "broker_accounting_intent","broker_closing_basis_claim","ledger_transaction","ledger_journal","ledger_entry",
        "ledger_account_balance","ledger_posting_receipt","financial_source_receipt","financial_encumbrance",
        "capacity_reservation","capacity_funding_receipt","capacity_usage","capacity_expiry_dispatch","emulator_order",
        "capacity_lifecycle","ledger_period","ledger_valuation","ledger_reconciliation","accounting_export",
        "accounting_export_source","accounting_export_attempt","financial_operation_receipt","financial_history_receipt"];

    static readonly string[] CompositionTables=["portfolio.order_composition_decision","portfolio.order_composition_fund_decision",
        "portfolio.accepted_trade_order","portfolio.accepted_trade_order_leg","portfolio.accepted_trade_order_capacity",
        "portfolio.accepted_position_close","portfolio.financial_operation_by_portfolio_month"];
    static string[] Scope(bool includeComposition)=>includeComposition ? [..Tables,..CompositionTables] : Tables;
    static string Qualified(string table)=>table.Contains('.',System.StringComparison.Ordinal) ? table : "portfolio_financial."+table;

    /// <summary>Captures a verified full financial database baseline while all application source writers are stopped.
    /// Table locks prevent changes during validation and capture. This is an explicit migration operation, not a runtime snapshot task.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FinancialProjectionBaselineEvent> CaptureAsync(bool sourceWritesQuiesced,CancellationToken token=default,bool includeComposition=false)
    {
        if(!sourceWritesQuiesced) throw new ArgumentException("Stop source writers before financial baseline capture.",nameof(sourceWritesQuiesced));
        return transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await (LockAsync(db,Scope(includeComposition),ct)).ConfigureAwait(false);
            await (VerifyAsync(db,ct)).ConfigureAwait(false);
            var cut=Convert.ToInt64(await (db.ScalarAsync("SELECT COALESCE(max(eventversion),0) FROM event_log;",[],ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
            var schema=Convert.ToInt32(await (db.ScalarAsync("SELECT version FROM portfolio_financial.schema_version WHERE singleton;",[],ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
            var images=new List<FinancialBaselineTable>();
            foreach(var table in Scope(includeComposition))
            {
                var columns=await (ColumnsAsync(db,table,ct)).ConfigureAwait(false);
                var rows=(string)(await (db.ScalarAsync($"SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t)-'created_transaction_id' AS j FROM {Qualified(table)} t) q;",[],ct)).ConfigureAwait(false))!;
                images.Add(new(table,columns,rows,Hash(rows)));
            }
            var source=new FinancialProjectionBaselineEvent { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),ReceivedOn=DateTime.UtcNow,
                SourceCut=cut,FinancialSchemaVersion=schema,Tables=images.ToArray(),SchemaVersion=includeComposition?2:1 };
            await (db.AppendAsync("FinancialProjectionBaseline",source.CommandId,source,
                Convert.ToInt64(await (db.ScalarAsync("SELECT COALESCE(max(e.streamversion),0) FROM event_log e JOIN event_stream_id s ON s.eventstreamid=e.eventstreamid WHERE s.eventstream=$1;",["FinancialProjectionBaseline"],ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture),ct)).ConfigureAwait(false);
            await (FinancialOperationalEventPersistence.ClaimAsync(db,source,0,ct)).ConfigureAwait(false);
            return source;
        },token);
    }

    /// <summary>Restores exact verified baseline facts into empty financial tables, with original source events already available.
    /// Repeated application is idempotent. The immutable journal guards remain enabled.</summary>
    public Task ReplayAsync(FinancialProjectionBaselineEvent source,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.SchemaVersion is not(1 or 2) || source.EventId<=source.SourceCut || source.Id==Guid.Empty ||
            !source.Tables.Select(x=>x.Name).SequenceEqual(Scope(source.SchemaVersion==2), StringComparer.Ordinal) || source.Tables.Any(x=>!string.Equals(Hash(x.RowsJson),x.Sha256,StringComparison.Ordinal)))
            throw new InvalidDataException("Committed complete financial baseline and matching hashes required.");
        return transactions.ExecuteOnceAsync(async(db,ct)=>
        {
            await (LockAsync(db,Scope(source.SchemaVersion==2),ct)).ConfigureAwait(false);
            if(!(await (FinancialOperationalEventPersistence.ClaimAsync(db,source,0,ct)).ConfigureAwait(false))) return false;
            if(Convert.ToInt32(await (db.ScalarAsync("SELECT version FROM portfolio_financial.schema_version WHERE singleton;",[],ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture)!=source.FinancialSchemaVersion)
                throw new InvalidDataException("Financial baseline schema differs from target.");
            foreach(var image in source.Tables)
            {
                if(Convert.ToInt64(await (db.ScalarAsync($"SELECT count(*) FROM {Qualified(image.Name)};",[],ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture)!=0)
                    throw new InvalidOperationException("Baseline requires empty target financial tables.");
                var columns=await (ColumnsAsync(db,image.Name,ct)).ConfigureAwait(false);
                if(!columns.SequenceEqual(image.Columns)) throw new InvalidDataException("Financial baseline columns differ from target.");
                var names=string.Join(",",columns.Select(x=>"\""+x.Replace("\"","\"\"",System.StringComparison.Ordinal)+"\""));
                await (db.ExecuteAsync($"INSERT INTO {Qualified(image.Name)} ({names}) SELECT {names} FROM jsonb_populate_recordset(NULL::{Qualified(image.Name)},$1::jsonb);",[image.RowsJson],ct)).ConfigureAwait(false);
            }
            await (VerifyAsync(db,ct)).ConfigureAwait(false);
            return true;
        },token);
    }

    static Task<int> LockAsync(EnlistedEventTransaction db,string[] scope,CancellationToken ct)=>db.ExecuteAsync(
        "LOCK TABLE "+string.Join(",",scope.Select(Qualified))+" IN SHARE ROW EXCLUSIVE MODE;",[],ct);
    static async Task<string[]> ColumnsAsync(EnlistedEventTransaction db,string table,CancellationToken ct)=>
        (await (db.QueryAsync("SELECT column_name FROM information_schema.columns WHERE table_schema=$2 AND table_name=$1 AND column_name<>'created_transaction_id' AND is_generated='NEVER' ORDER BY ordinal_position;",[table.Contains('.',System.StringComparison.Ordinal)?table.Split('.')[1]:table,table.Contains('.',System.StringComparison.Ordinal)?table.Split('.')[0]:"portfolio_financial"],r=>r.GetString(0),ct)).ConfigureAwait(false)).ToArray();
    static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Every journal needs at least two balanced lines; account totals must equal the aggregate of all journal entries.</summary>
    static async Task VerifyAsync(EnlistedEventTransaction db,CancellationToken ct)
    {
        var invalid=await (db.ScalarAsync("""
            SELECT EXISTS(SELECT 1 FROM portfolio_financial.ledger_journal j LEFT JOIN portfolio_financial.ledger_entry e USING(journal_id)
              GROUP BY j.journal_id HAVING count(e.ordinal)<2 OR sum(e.debit)<>sum(e.credit)) OR EXISTS(
              WITH calculated AS (SELECT book_id,account_id,fund_id,currency,sum(debit) debit_total,sum(credit) credit_total
                FROM portfolio_financial.ledger_entry GROUP BY book_id,account_id,fund_id,currency)
              SELECT 1 FROM calculated c FULL JOIN portfolio_financial.ledger_account_balance b
                ON c.book_id=b.book_id AND c.account_id=b.account_id AND c.fund_id IS NOT DISTINCT FROM b.fund_id AND c.currency=b.currency
              WHERE c.book_id IS NULL OR b.book_id IS NULL OR c.debit_total<>b.debit_total OR c.credit_total<>b.credit_total
                OR b.balance<>c.debit_total-c.credit_total);
            """,[],ct)).ConfigureAwait(false);
        if(invalid is true) throw new InvalidDataException("Financial baseline refused: journals or materialized balances do not reconcile.");
    }
}
