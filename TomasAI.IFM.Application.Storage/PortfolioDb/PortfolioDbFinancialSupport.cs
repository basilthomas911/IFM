using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Authoritative PostgreSQL reads and shared financial fencing. Scylla is never a balance fallback.</summary>
public static class PortfolioDbFinancialSupport
{
    internal static async Task CreateBookAsync(EnlistedEventTransaction db,FinancialBookConfiguration book,
        IReadOnlyList<LedgerAccountDefinition> accounts,IReadOnlyList<LedgerPostingRule> rules,
        DateOnly periodStart,DateOnly periodEnd,Guid periodId,CancellationToken cancellation)
    {
        await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select01,[book.PortfolioId],cancellation);
        Require(book.BookId>0 && book.PortfolioId>0 && book.AccountingEntityId!=Guid.Empty && book.Currency=="USD" &&
            book.Funds.Length>0 && book.Funds.All(x=>x.FundId>0) && book.Funds.Select(x=>x.FundId).Distinct().Count()==book.Funds.Length,
            FinancialReasons.InvalidContract,"Invalid book/Fund configuration.");
        Require(!string.IsNullOrWhiteSpace(book.ExecutionAccountReference) && !string.IsNullOrWhiteSpace(book.Environment),
            FinancialReasons.InvalidContract,"An exclusive account/environment mapping is required.");
        await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert01,[book.BookId,book.AccountingEntityId,book.PortfolioId,book.ExecutionAccountReference,book.Environment],cancellation);
        await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert02,[book.PortfolioId,book.BookId,book.AuthorityEpoch,book.MigrationQualified?"Active":"Importing",Json(book),
                book.SourceWatermark,book.ValuationWatermark,book.MigrationQualified?"Qualified":"Unqualified"],cancellation);
        foreach(var account in accounts)
            await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert03,[book.BookId,account.AccountId,account.Version,account.Category,(int)account.NormalSide,account.FundDimensionRequired,account.ContentHash],cancellation);
        foreach(var rule in rules)
            await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert04,[book.BookId,rule.RuleId,rule.Version,(int)rule.Kind,Json(rule),rule.ContentHash,periodStart],cancellation);
        await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert05,[book.BookId,periodId,periodStart,periodEnd,book.SourceWatermark],cancellation);
    }

    internal static async Task<T?> ReadOperationAsync<T>(EnlistedEventTransaction db, int portfolioId, Guid operationId,
        string? inputHash,CancellationToken token) where T:class,IFinancialCompletedEvent
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.operation.read_receipt");
        var rows=await db.QueryAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select02,[portfolioId,operationId],reader=>(Hash:reader.GetString(0),Event:new EventLogReadModel(
                reader.GetInt64(1),reader.GetString(2),reader.GetString(3),reader.GetInt64(4),reader.GetFieldValue<byte[]>(5),reader.GetGuid(6),reader.GetString(7),reader.GetInt64(8))),token);
        if(rows.Count==0) return null;
        var row=rows.Single();
        Require(inputHash is null || inputHash==row.Hash,FinancialReasons.RequestMismatch,"Operation identity was used for different input.");
        return row.Event.ToDomainEvent() as T ?? throw new FinancialOperationException(FinancialReasons.RequestMismatch,"Operation belongs to a different financial actor/result contract.",FinancialCommitDisposition.NoNewMutation,operationId);
    }

    internal static async Task<(FinancialBookConfiguration Book,long Revision,string State)> LockAuthorityAsync(
        EnlistedEventTransaction db,int portfolioId,long? expectedRevision,CancellationToken token)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.authority.lock");
        var lockStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        var rows=await db.QueryAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select03,[portfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Revision:r.GetInt64(1),State:r.GetString(2)),token);
        FinancialTelemetry.Lock(System.Diagnostics.Stopwatch.GetElapsedTime(lockStarted).TotalMilliseconds);
        Require(rows.Count==1,FinancialReasons.AuthorityDenied,"Financial authority is not configured.");
        var result=rows[0];
        Require(expectedRevision is null || result.Revision==expectedRevision,FinancialReasons.RevisionConflict,"Financial state changed; a new confirmed attempt must use current authority.");
        return result;
    }

    internal static async Task ValidateFundSourcesAsync(EnlistedEventTransaction db,FinancialBookConfiguration book,
        int fundId,bool spending,CancellationToken token)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.authority.validate_fund_sources");
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
            var actual=await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select04,[stream],token);
            Require(expected>0 && actual is not null && Convert.ToInt64(actual)==expected,
                FinancialReasons.AuthorityRevoked,"Portfolio/Fund/policy changed since financial authority was prepared.");
        }
    }

    internal static async Task<long> SaveOutcomeAsync(EnlistedEventTransaction db,IFinancialRequest request,
        IFinancialCompletedEvent completed,long revision,bool function,CancellationToken token)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.operation.save_outcome");
        var stream=request.Subject.StreamId;
        var expected=function?0:Convert.ToInt64(await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select05,[stream],token)??0L);
        var eventVersion=await db.AppendAsync(stream,request.CommandId,completed,expected,token);
        await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,[request.PortfolioId,request.OperationId,request.Subject.Name,request.InputSha256,eventVersion,revision],token);
        await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[request.PortfolioId,revision],token);
        return eventVersion;
    }

    internal static NpgsqlParameter Json<T>(T value)=>new() { NpgsqlDbType=NpgsqlDbType.Jsonb,Value=JsonConvert.SerializeObject(value) };
    internal static T Decode<T>(string value)=>JsonConvert.DeserializeObject<T>(value)??throw new InvalidDataException($"Invalid stored {typeof(T).Name}.");
    internal static void Require(bool condition,int code,string message)
    { if(!condition) throw new FinancialOperationException(code,message); }
}

/// <summary>Compatibility facade while financial callers move to the unified PortfolioDbContext.</summary>
public sealed class PortfolioFinancialStore(IPostgresEventTransaction transactions)
{
    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent => transactions.ExecuteAsync(
            (db, cancellation) => PortfolioDbFinancialSupport.ReadOperationAsync<T>(db, portfolioId, operationId, inputHash, cancellation), token);

    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken token = default) => transactions.ExecuteAsync(async (db, cancellation) =>
    {
        var value = await db.ScalarAsync(PortfolioDbSql.Financial.ReadBook, [portfolioId], cancellation).ConfigureAwait(false);
        return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
    }, token);

    internal Task CreateBookAsync(FinancialBookConfiguration book, IReadOnlyList<LedgerAccountDefinition> accounts,
        IReadOnlyList<LedgerPostingRule> rules, DateOnly periodStart, DateOnly periodEnd, CancellationToken token = default) =>
        transactions.ExecuteAsync(async (db, cancellation) =>
        {
            await PortfolioDbFinancialSupport.CreateBookAsync(db, book, accounts, rules, periodStart, periodEnd, Guid.NewGuid(), cancellation).ConfigureAwait(false);
            return true;
        }, token);

}
