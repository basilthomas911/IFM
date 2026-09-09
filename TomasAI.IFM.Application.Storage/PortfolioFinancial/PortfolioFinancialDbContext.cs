using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IPortfolioFinancialDbContext
{
    Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent;
    Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken token = default);
}

/// <summary>Authoritative PostgreSQL reads and shared financial fencing. Scylla is never a balance fallback.</summary>
public sealed class PortfolioFinancialDbContext(IPostgresEventTransaction transactions) : IPortfolioFinancialDbContext
{
    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent => transactions.ExecuteAsync(
            (db, cancellation) => ReadOperationAsync<T>(db,portfolioId,operationId,inputHash,cancellation),token);

    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken token = default) => transactions.ExecuteAsync(async (db,cancellation) =>
    {
        var value=await db.ScalarAsync("SELECT policy_source_versions::text FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[portfolioId],cancellation);
        return value is string json?Decode<FinancialBookConfiguration>(json):null;
    },token);

    /// <summary>Creates a book with explicit account/rule versions; never imports capital implicitly.</summary>
    internal Task CreateBookAsync(FinancialBookConfiguration book, IReadOnlyList<LedgerAccountDefinition> accounts,
        IReadOnlyList<LedgerPostingRule> rules, DateOnly periodStart, DateOnly periodEnd, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db,cancellation) =>
        {
            await CreateBookAsync(db,book,accounts,rules,periodStart,periodEnd,Guid.NewGuid(),cancellation);
            return true;
        },token);

    internal static async Task CreateBookAsync(EnlistedEventTransaction db,FinancialBookConfiguration book,
        IReadOnlyList<LedgerAccountDefinition> accounts,IReadOnlyList<LedgerPostingRule> rules,
        DateOnly periodStart,DateOnly periodEnd,Guid periodId,CancellationToken cancellation)
    {
        await db.ScalarAsync("SELECT pg_advisory_xact_lock(34100,$1);",[book.PortfolioId],cancellation);
        Require(book.BookId>0 && book.PortfolioId>0 && book.AccountingEntityId!=Guid.Empty && book.Currency=="USD" &&
            book.Funds.Length>0 && book.Funds.All(x=>x.FundId>0) && book.Funds.Select(x=>x.FundId).Distinct().Count()==book.Funds.Length,
            FinancialReasons.InvalidContract,"Invalid book/Fund configuration.");
        Require(!string.IsNullOrWhiteSpace(book.ExecutionAccountReference) && !string.IsNullOrWhiteSpace(book.Environment),
            FinancialReasons.InvalidContract,"An exclusive account/environment mapping is required.");
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.ledger_book(book_id,accounting_entity_id,portfolio_id,base_currency,execution_account_ref,environment,version,status)
            VALUES($1,$2,$3,'USD',$4,$5,1,'Active');
            """,[book.BookId,book.AccountingEntityId,book.PortfolioId,book.ExecutionAccountReference,book.Environment],cancellation);
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.financial_authority(portfolio_id,book_id,authority_epoch,operating_state,policy_source_versions,source_watermark,valuation_watermark,migration_state)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8);
            """,[book.PortfolioId,book.BookId,book.AuthorityEpoch,book.MigrationQualified?"Active":"Importing",Json(book),
                book.SourceWatermark,book.ValuationWatermark,book.MigrationQualified?"Qualified":"Unqualified"],cancellation);
        foreach(var account in accounts)
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,fund_dimension_required,status,content_hash)
                VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
                """,[book.BookId,account.AccountId,account.Version,account.Category,(int)account.NormalSide,account.FundDimensionRequired,account.ContentHash],cancellation);
        foreach(var rule in rules)
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,effective_from)
                VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
                """,[book.BookId,rule.RuleId,rule.Version,(int)rule.Kind,Json(rule),rule.ContentHash,periodStart],cancellation);
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
            VALUES($1,$2,$3,$4,'Open',1,$5,'{}');
            """,[book.BookId,periodId,periodStart,periodEnd,book.SourceWatermark],cancellation);
    }

    internal static async Task<T?> ReadOperationAsync<T>(EnlistedEventTransaction db, int portfolioId, Guid operationId,
        string? inputHash,CancellationToken token) where T:class,IFinancialCompletedEvent
    {
        var rows=await db.QueryAsync("""
            SELECT r.input_hash,e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventdata::text,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM portfolio_financial.financial_operation_receipt r JOIN event_log e ON e.eventversion=r.event_version
            JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE r.portfolio_id=$1 AND r.operation_id=$2;
            """,[portfolioId,operationId],reader=>(Hash:reader.GetString(0),Event:new EventLogReadModel(
                reader.GetInt64(1),reader.GetString(2),reader.GetString(3),reader.GetInt64(4),reader.GetString(5),reader.GetGuid(6),reader.GetString(7),reader.GetInt64(8))),token);
        if(rows.Count==0) return null;
        var row=rows.Single();
        Require(inputHash is null || inputHash==row.Hash,FinancialReasons.RequestMismatch,"Operation identity was used for different input.");
        return row.Event.ToDomainEvent() as T ?? throw new FinancialOperationException(FinancialReasons.RequestMismatch,"Operation belongs to a different financial actor/result contract.",FinancialCommitDisposition.NoNewMutation,operationId);
    }

    internal static async Task<(FinancialBookConfiguration Book,long Revision,string State)> LockAuthorityAsync(
        EnlistedEventTransaction db,int portfolioId,long? expectedRevision,CancellationToken token)
    {
        var lockStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        var rows=await db.QueryAsync("""
            SELECT policy_source_versions::text,financial_revision,operating_state FROM portfolio_financial.financial_authority
            WHERE portfolio_id=$1 FOR UPDATE;
            """,[portfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Revision:r.GetInt64(1),State:r.GetString(2)),token);
        FinancialTelemetry.Lock(System.Diagnostics.Stopwatch.GetElapsedTime(lockStarted).TotalMilliseconds);
        Require(rows.Count==1,FinancialReasons.AuthorityDenied,"Financial authority is not configured.");
        var result=rows[0];
        Require(expectedRevision is null || result.Revision==expectedRevision,FinancialReasons.RevisionConflict,"Financial state changed; a new confirmed attempt must use current authority.");
        return result;
    }

    internal static async Task ValidateFundSourcesAsync(EnlistedEventTransaction db,FinancialBookConfiguration book,
        int fundId,bool spending,CancellationToken token)
    {
        var fund=book.Funds.SingleOrDefault(x=>x.FundId==fundId);
        Require(fund is not null,FinancialReasons.AuthorityDenied,"Fund does not belong to this financial book.");
        if(!spending) return; // Authenticated financial facts still post after a mandate is suspended.
        Require(book.MigrationQualified && fund!.CanSpend && fund.Reference.ValidUntilUtc>DateTime.UtcNow,
            FinancialReasons.AuthorityRevoked,"Current financial authority does not permit spending.");
        await Check($"Portfolio.{book.PortfolioId}",fund.PortfolioStreamVersion);
        await Check($"PortfolioFund.{book.PortfolioId}.{fundId}",fund.FundStreamVersion);
        await Check($"PortfolioFinancialPolicy.{book.PortfolioId}.{fund.Reference.PolicyId}",fund.PolicyStreamVersion);
        async Task Check(string stream,long expected)
        {
            var actual=await db.ScalarAsync("SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;",[stream],token);
            Require(expected>0 && actual is not null && Convert.ToInt64(actual)==expected,
                FinancialReasons.AuthorityRevoked,"Portfolio/Fund/policy changed since financial authority was prepared.");
        }
    }

    internal static async Task<long> SaveOutcomeAsync(EnlistedEventTransaction db,IFinancialRequest request,
        IFinancialCompletedEvent completed,long revision,bool function,CancellationToken token)
    {
        var stream=request.Subject.StreamId;
        var expected=function?0:Convert.ToInt64(await db.ScalarAsync("SELECT currentversion FROM event_stream_id WHERE eventstream=$1;",[stream],token)??0L);
        var eventVersion=await db.AppendAsync(stream,request.CommandId,completed,expected,token);
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.financial_operation_receipt(portfolio_id,operation_id,actor_name,input_hash,event_version,financial_revision)
            VALUES($1,$2,$3,$4,$5,$6);
            """,[request.PortfolioId,request.OperationId,request.Subject.Name,request.InputSha256,eventVersion,revision],token);
        await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET financial_revision=$2 WHERE portfolio_id=$1;",[request.PortfolioId,revision],token);
        return eventVersion;
    }

    internal static NpgsqlParameter Json<T>(T value)=>new() { NpgsqlDbType=NpgsqlDbType.Jsonb,Value=JsonConvert.SerializeObject(value) };
    internal static T Decode<T>(string value)=>JsonConvert.DeserializeObject<T>(value)??throw new InvalidDataException($"Invalid stored {typeof(T).Name}.");
    internal static void Require(bool condition,int code,string message)
    { if(!condition) throw new FinancialOperationException(code,message); }
}
