using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using Newtonsoft.Json;


using TomasAI.IFM.Application.Storage.EventSourceDb;

using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Authoritative PostgreSQL reads and shared financial fencing. Scylla is never a balance fallback.</summary>
internal static class FinancialRepositorySupport
{
    internal static async Task CreateBookAsync(EnlistedEventTransaction db, FinancialBookConfiguration book,
        IReadOnlyList<LedgerAccountDefinition> accounts, IReadOnlyList<LedgerPostingRule> rules,
        DateOnly periodStart, DateOnly periodEnd, Guid periodId, CancellationToken cancellation)
    {
        await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select01, [book.PortfolioId], cancellation)).ConfigureAwait(false);
        Require(book.BookId > 0 && book.PortfolioId > 0 && book.AccountingEntityId != Guid.Empty && string.Equals(book.Currency,"USD",StringComparison.Ordinal) &&
            book.Funds.Length > 0 && book.Funds.All(x => x.FundId > 0) && book.Funds.Select(x => x.FundId).Distinct().Count() == book.Funds.Length,
            FinancialReasons.InvalidContract, "Invalid book/Fund configuration.");
        Require(!string.IsNullOrWhiteSpace(book.ExecutionAccountReference) && !string.IsNullOrWhiteSpace(book.Environment),
            FinancialReasons.InvalidContract, "An exclusive account/environment mapping is required.");
        await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert01, [book.BookId, book.AccountingEntityId, book.PortfolioId, book.ExecutionAccountReference, book.Environment], cancellation)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert02, [book.PortfolioId,book.BookId,book.AuthorityEpoch,book.MigrationQualified?"Active":"Importing",Json(book),
                book.SourceWatermark,book.ValuationWatermark,book.MigrationQualified?"Qualified":"Unqualified"], cancellation)).ConfigureAwait(false);
        foreach (var account in accounts)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert03, [book.BookId, account.AccountId, account.Version, account.Category, (int)account.NormalSide, account.FundDimensionRequired, account.ContentHash], cancellation)).ConfigureAwait(false);
        foreach (var rule in rules)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert04, [book.BookId, rule.RuleId, rule.Version, (int)rule.Kind, Json(rule), rule.ContentHash, periodStart], cancellation)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert05, [book.BookId, periodId, periodStart, periodEnd, book.SourceWatermark], cancellation)).ConfigureAwait(false);
    }

    internal static async Task<T?> ReadOperationAsync<T>(EnlistedEventTransaction db, int portfolioId, Guid operationId,
        string? inputHash, CancellationToken token) where T : class, IFinancialCompletedEvent
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.operation.read_receipt");
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select02, [portfolioId, operationId], reader => (Hash: reader.GetString(0), Event: new EventLogReadModel(
                reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetFieldValue<byte[]>(5), reader.GetGuid(6), reader.GetString(7), reader.GetInt64(8))), token)).ConfigureAwait(false);
        if (rows.Count == 0) return null;
        var row = rows.Single();
        Require(inputHash is null || string.Equals(inputHash,row.Hash,StringComparison.Ordinal), FinancialReasons.RequestMismatch, "Operation identity was used for different input.");
        return row.Event.ToDomainEvent() as T ?? throw new FinancialOperationException(FinancialReasons.RequestMismatch, "Operation belongs to a different financial actor/result contract.", FinancialCommitDisposition.NoNewMutation, operationId);
    }

    internal static async Task<(FinancialBookConfiguration Book, long Revision, string State)> LockAuthorityAsync(
        EnlistedEventTransaction db, int portfolioId, long? expectedRevision, CancellationToken token, FinancialDevelopmentPolicy? developmentPolicy = null)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.authority.lock");
        var lockStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select03, [portfolioId], r => (Book: Decode<FinancialBookConfiguration>(r.GetString(0)), Revision: r.GetInt64(1), State: r.GetString(2)), token)).ConfigureAwait(false);
        FinancialTelemetry.Lock(System.Diagnostics.Stopwatch.GetElapsedTime(lockStarted).TotalMilliseconds);
        Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "Financial authority is not configured.");
        var result = rows[0];
        Require(expectedRevision is null || result.Revision == expectedRevision, FinancialReasons.RevisionConflict, "Financial state changed; a new confirmed attempt must use current authority.");
        var tradingBook = developmentPolicy?.TradingBook(result.Book) ?? result.Book;
        return (tradingBook, result.Revision, tradingBook.DevelopmentQualificationsExempt ? "Active" : result.State);
    }

    internal static async Task ValidateFundSourcesAsync(EnlistedEventTransaction db, FinancialBookConfiguration book,
        int fundId, bool spending, CancellationToken token)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.authority.validate_fund_sources");
        var fund = book.Funds.SingleOrDefault(x => x.FundId == fundId);
        Require(fund is not null, FinancialReasons.AuthorityDenied, "Fund does not belong to this financial book.");
        if (!spending || book.DevelopmentQualificationsExempt) return; // Authenticated financial facts still post after a mandate is suspended.
        Require(book.MigrationQualified && fund!.CanSpend && fund.Reference.ValidUntilUtc > DateTime.UtcNow,
            FinancialReasons.AuthorityRevoked, "Current financial authority does not permit spending.");
        await (Check($"Portfolio.{book.PortfolioId}", fund.PortfolioStreamVersion)).ConfigureAwait(false);
        await (Check($"PortfolioFund.{book.PortfolioId}.{fundId}", fund.FundStreamVersion)).ConfigureAwait(false);
        await (Check($"PortfolioFinancialPolicy.{book.PortfolioId}.{fund.Reference.PolicyId}", fund.PolicyStreamVersion)).ConfigureAwait(false);
        async Task Check(string stream, long expected)
        {
            var actual = await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select04, [stream], token)).ConfigureAwait(false);
            Require(expected > 0 && actual is not null && Convert.ToInt64(actual,System.Globalization.CultureInfo.InvariantCulture) == expected,
                FinancialReasons.AuthorityRevoked, "Portfolio/Fund/policy changed since financial authority was prepared.");
        }
    }

    internal static async Task<long> SaveOutcomeAsync(EnlistedEventTransaction db, IFinancialRequest request,
        IFinancialCompletedEvent completed, long revision, bool function, CancellationToken token)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.operation.save_outcome");
        var stream = request.Subject.StreamId;
        var expected = function ? 0 : Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select05, [stream], token)).ConfigureAwait(false) ?? 0L,System.Globalization.CultureInfo.InvariantCulture);
        var eventVersion = await (db.AppendAsync(stream, request.CommandId, completed, expected, token)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06, [request.PortfolioId, request.OperationId, request.Subject.Name, request.InputSha256, eventVersion, revision], token)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01, [request.PortfolioId, revision], token)).ConfigureAwait(false);
        return eventVersion;
    }

    internal static System.Data.Common.DbParameter Json<T>(T value) => PostgresValues.Jsonb(JsonConvert.SerializeObject(value));
    internal static T Decode<T>(string value) => JsonConvert.DeserializeObject<T>(value) ?? throw new InvalidDataException($"Invalid stored {typeof(T).Name}.");
    internal static void Require(bool condition, int code, string message)
    { if (!condition) throw new FinancialOperationException(code, message); }
}
