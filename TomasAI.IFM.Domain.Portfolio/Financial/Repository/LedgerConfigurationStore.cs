
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Configuration, period control, and independent reconciliation share canonical Portfolio authority.</summary>
/// <param name="transactions">The transactional event-store boundary.</param>
/// <param name="developmentPolicy">The optional development-only qualification policy.</param>
public sealed class LedgerConfigurationStore(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null) : ILedgerConfigurationStore
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1075", Justification = "A failed receipt probe does not establish that COMMIT failed; preserve the original ambiguous outcome for reconciliation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt, LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult, string> hash, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await (transactions.ExecuteAsync(async (db, ct) =>
        {
            var replay = await (ReadOperationAsync<LedgerConfigurationCompletedEvent>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct)).ConfigureAwait(false);
            if (replay is not null) return replay;
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01, [request.PortfolioId], ct)).ConfigureAwait(false);
            replay = await (ReadOperationAsync<LedgerConfigurationCompletedEvent>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct)).ConfigureAwait(false);
            if (replay is not null) return replay;
            var body = request.Body;
            if (body.Action == LedgerConfigurationAction.CreateBook)
            {
                Require(request.ExpectedFinancialRevision == 0 && body.Book is { MigrationQualified: false } &&
                    body.Book.PortfolioId == request.PortfolioId && body.Book.BookId == body.BookId && body.Book.Funds.All(x => !x.CanSpend),
                    FinancialReasons.AuthorityDenied, "New books require explicit migration qualification before spending is enabled.");
                foreach (var fund in body.Book!.Funds)
                {
                    await (CheckSource($"Portfolio.{request.PortfolioId}", fund.PortfolioStreamVersion)).ConfigureAwait(false);
                    await (CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}", fund.FundStreamVersion)).ConfigureAwait(false);
                }
                await (CreateBookAsync(db, body.Book!, body.Accounts, body.Rules, body.PeriodStart, body.PeriodEnd, body.PeriodId, ct)).ConfigureAwait(false);
            }
            var current = await (LockAuthorityAsync(db, request.PortfolioId, request.ExpectedFinancialRevision, ct)).ConfigureAwait(false);
            Require(current.Book.BookId == body.BookId, FinancialReasons.AuthorityDenied, "Book does not belong to this Portfolio.");
            Require(DateTime.UtcNow < request.ExpiresAtUtc, FinancialReasons.TimeExpired, "Configuration deadline expired before commit.");
            var revision = checked(current.Revision + 1);
            var state = current.State;
            Guid? reconciliationId = null;
            LedgerReconciliationResult? acceptedReconciliation = null;
            switch (body.Action)
            {
                case LedgerConfigurationAction.CreateBook: break;
                case LedgerConfigurationAction.QualifyDevelopmentBook:
                    Require(developmentPolicy?.IsDevelopmentEnvironment == true && string.Equals(current.Book.Environment,"Emulator",StringComparison.Ordinal) && !current.Book.MigrationQualified &&
                        string.Equals(current.State,"Importing",StringComparison.Ordinal) && current.Book.Funds.All(x => !x.CanSpend) && body.Book is not null &&
                        string.Equals(FinancialCanonicalHash.Compute(body.Book),FinancialCanonicalHash.Compute(current.Book),StringComparison.Ordinal), FinancialReasons.AuthorityDenied, "Only the exact unqualified development book can be qualified.");
                    await (RequireReconciled(db, body, ct)).ConfigureAwait(false);
                    foreach (var fund in current.Book.Funds.OrderBy(x => x.FundId))
                    {
                        await (CheckSource($"Portfolio.{request.PortfolioId}", fund.PortfolioStreamVersion)).ConfigureAwait(false);
                        await (CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}", fund.FundStreamVersion)).ConfigureAwait(false);
                    }
                    state = "NeedsRefresh";
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update01, [request.PortfolioId, Json(current.Book with { MigrationQualified = true }), state], ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.AddAccountVersion:
                    foreach (var account in body.Accounts)
                    {
                        var last = Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select03, [body.BookId, account.AccountId], ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
                        Require(last == body.ExpectedVersion && account.Version == last + 1, FinancialReasons.RevisionConflict, "Account version changed.");
                        var category = await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select04, [body.BookId, account.AccountId], ct)).ConfigureAwait(false);
                        Require(category is null || Equals(category, account.Category), FinancialReasons.InvalidContract, "An account's economic category cannot change across versions.");
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert02, [body.BookId, account.AccountId, account.Version, account.Category, (int)account.NormalSide, account.FundDimensionRequired, account.ContentHash], ct)).ConfigureAwait(false);
                    }
                    break;
                case LedgerConfigurationAction.AddPostingRuleVersion:
                    foreach (var rule in body.Rules)
                    {
                        var last = Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select05, [body.BookId, rule.RuleId], ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
                        Require(last == body.ExpectedVersion && rule.Version == last + 1, FinancialReasons.RevisionConflict, "Posting rule version changed.");
                        foreach (var account in new[] { rule.Debit, rule.Credit, rule.ValuationAsset, rule.UnrealizedPnl }.OfType<LedgerAccountBinding>())
                            Require(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select06, [body.BookId, account.AccountId, account.Version], ct)).ConfigureAwait(false) is "Active",
                                FinancialReasons.InvalidContract, "Posting rules require active exact account versions.");
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert03, [body.BookId, rule.RuleId, rule.Version, (int)rule.Kind, Json(rule), rule.ContentHash, body.PeriodStart], ct)).ConfigureAwait(false);
                    }
                    break;
                case LedgerConfigurationAction.RetireAccount:
                    foreach (var account in body.Accounts)
                    {
                        Require(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select07, [body.BookId, account.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture)], ct)).ConfigureAwait(false) is 0L,
                            FinancialReasons.AuthorityDenied, "Retire dependent posting rules before retiring an account.");
                        var changed = await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update02, [body.BookId, account.AccountId, account.Version], ct)).ConfigureAwait(false);
                        Require(changed == 1, FinancialReasons.RevisionConflict, "Account version is not active.");
                    }
                    break;
                case LedgerConfigurationAction.RetirePostingRule:
                    foreach (var rule in body.Rules)
                        Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update03, [body.BookId, rule.RuleId, rule.Version], ct)).ConfigureAwait(false) == 1,
                            FinancialReasons.RevisionConflict, "Posting rule version is not active.");
                    break;
                case LedgerConfigurationAction.OpenPeriod:
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert04, [body.BookId, body.PeriodId, body.PeriodStart, body.PeriodEnd, body.SourceCut, Json(new { request.OperationId, body.Reason })], ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.ClosePeriod:
                case LedgerConfigurationAction.ReopenPeriod:
                    var closing = body.Action == LedgerConfigurationAction.ClosePeriod;
                    if (closing) await (RequireReconciled(db, body, ct)).ConfigureAwait(false);
                    Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update04, [body.BookId,body.PeriodId,body.ExpectedVersion,closing?"Closed":"Open",body.SourceCut,
                            Json(new { request.OperationId,body.Reason,body.ReconciliationId }),closing?"Open":"Closed"], ct)).ConfigureAwait(false) == 1,
                        FinancialReasons.RevisionConflict, "Period state/version changed.");
                    break;
                case LedgerConfigurationAction.RefreshAuthority:
                    if (body.Book?.Funds.Any(x => x.CanSpend) == true)
                        Require(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select08, [request.PortfolioId, (int)CapacityScopeKind.Underlying], ct)).ConfigureAwait(false) is 0L,
                            FinancialReasons.AuthorityDenied, "Legacy contract-specific underlying obligations require reconciliation before new product-wide authority.");
                    Require(body.Book is { } updated && updated.BookId == current.Book.BookId && updated.PortfolioId == request.PortfolioId &&
                        updated.AccountingEntityId == current.Book.AccountingEntityId && string.Equals(updated.Environment,current.Book.Environment,StringComparison.Ordinal) &&
                        string.Equals(updated.ExecutionAccountReference,current.Book.ExecutionAccountReference,StringComparison.Ordinal) && string.Equals(updated.Currency,"USD",StringComparison.Ordinal) &&
                        updated.MigrationQualified == current.Book.MigrationQualified,
                        FinancialReasons.AuthorityDenied, "Refresh cannot replace book ownership or migration qualification.");
                    Require(body.Book!.Funds.Select(x => x.FundId).Order().SequenceEqual(current.Book.Funds.Select(x => x.FundId).Order()),
                        FinancialReasons.AuthorityDenied, "Authority refresh cannot change qualified book membership; a separate scoped qualification is required.");
                    var epoch = Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select09, [request.PortfolioId], ct)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
                    Require(body.Book!.AuthorityEpoch == epoch && body.Book.Funds.All(x => x.Reference.AuthorityEpoch == epoch), FinancialReasons.AuthorityRevoked, "Authority epoch changed.");
                    foreach (var fund in body.Book.Funds) await (ValidateFundSourcesAsync(db, body.Book, fund.FundId, fund.CanSpend, ct)).ConfigureAwait(false);
                    foreach (var fund in body.Book.Funds)
                    {
                        await (CheckSource($"Portfolio.{request.PortfolioId}", fund.PortfolioStreamVersion)).ConfigureAwait(false);
                        await (CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}", fund.FundStreamVersion)).ConfigureAwait(false);
                        if (fund.CanSpend) await (CheckSource($"PortfolioFinancialPolicy.{request.PortfolioId}.{fund.Reference.PolicyId}", fund.PolicyStreamVersion)).ConfigureAwait(false);
                    }
                    state = current.State switch { "Overdrawn" or "NeedsReconciliation" => current.State, _ when body.Book.MigrationQualified => "Active", _ => "Importing" };
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update05, [request.PortfolioId, Json(body.Book), body.Book.SourceWatermark, body.Book.ValuationWatermark, state], ct)).ConfigureAwait(false);
                    break;
                case LedgerConfigurationAction.Reconcile:
                    reconciliationId = request.OperationId;
                    var result = await (ReconstructAsync(db, body.BookId, reconciliationId.Value, current.Revision, body.SourceCut, ct)).ConfigureAwait(false);
                    result = result with { ContentHash = hash(result) };
                    acceptedReconciliation = result;
                    await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert05, [result.ReconciliationId,body.BookId,request.PortfolioId,body.SourceCut,Json(new { result.JournalCount,result.EntryCount,result.FinancialRevision }),
                            Json(new { result.Debits,result.Credits }),result.ContentHash,Json(result.Differences),result.Differences.Length==0 && result.Debits==result.Credits?"Matched":"Mismatch"], ct)).ConfigureAwait(false);
                    if (result.Differences.Length != 0 || result.Debits != result.Credits)
                    {
                        state = "NeedsReconciliation";
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update06, [request.PortfolioId, state], ct)).ConfigureAwait(false);
                    }
                    else if (current.State is "Overdrawn" or "NeedsReconciliation")
                    {
                        var solvent = true;
                        foreach (var fund in current.Book.Funds)
                            if (await (GeneralLedgerStore.AvailableCash(db, current.Book.BookId, fund.FundId, ct)).ConfigureAwait(false) < 0) solvent = false;
                        // A matching ledger is not a fresh mandate, valuation or admission authorization.
                        // Explicit authority refresh must follow reconciliation before a qualified book can spend again.
                        state = (solvent, current.Book.MigrationQualified) switch { (false, _) => "Overdrawn", (true, true) => "NeedsRefresh", _ => "Importing" };
                        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update07, [request.PortfolioId, state], ct)).ConfigureAwait(false);
                    }
                    break;
                default: throw new FinancialOperationException(FinancialReasons.InvalidContract, "Unknown configuration action.");
            }
            var accepted = await (LockAuthorityAsync(db, request.PortfolioId, null, ct)).ConfigureAwait(false);
            var migrationState = (string)(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationProjection.ReadMigrationState, [request.PortfolioId], ct)).ConfigureAwait(false))!;
            var completed = complete(new(request.OperationId, body.BookId, body.Action, revision, DateTime.UtcNow, reconciliationId, state)) with
            {
                SchemaVersion = 2,
                ProjectionFacts = new() { ActorName = request.Subject.Name, Request = body, AcceptedBook = accepted.Book,
                    MigrationState = migrationState, Reconciliation = acceptedReconciliation }
            };
            await (SaveOutcomeAsync(db, request, completed, revision, false, ct)).ConfigureAwait(false);
            await (LedgerFinancialProjection.RecordAppliedAsync(db, completed, revision, ct)).ConfigureAwait(false);
            return completed;

            async Task CheckSource(string stream, long version)
            {
                var actual = await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select10, [stream], ct)).ConfigureAwait(false);
                Require(version > 0 && actual is not null && Convert.ToInt64(actual,System.Globalization.CultureInfo.InvariantCulture) == version, FinancialReasons.AuthorityRevoked, "Source changed while preparing authority refresh.");
            }
        }, token)).ConfigureAwait(false);
        }
        catch (FunctionCommitOutcomeUnknownException)
        {
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var receipt = await (new PortfolioFinancialStore(transactions).ReadOperationAsync<LedgerConfigurationCompletedEvent>(request.PortfolioId, request.OperationId, request.InputSha256, recovery.Token)).ConfigureAwait(false);
                if (receipt is not null) return receipt;
            }
            catch (Exception) { /* The caller must reconcile the original identity after connectivity returns. */ }
            throw;
        }
    }

    static async Task RequireReconciled(EnlistedEventTransaction db, LedgerConfigurationRequest body, CancellationToken ct)
    {
        Require(body.ReconciliationId is not null, FinancialReasons.InvalidContract, "Period closure requires reconciliation evidence.");
        var matches = await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select11, [body.BookId, body.ReconciliationId, body.SourceCut], ct)).ConfigureAwait(false);
        Require(matches is true, FinancialReasons.AuthorityDenied, "Reconciliation is missing, mismatched or predates subsequent journal postings.");
    }

    static async Task<LedgerReconciliationResult> ReconstructAsync(EnlistedEventTransaction db, int bookId, Guid id, long revision, string cut, CancellationToken ct)
    {
        var totals = await (db.QueryAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select12, [bookId], r => (Journals: r.GetInt64(0), Entries: r.GetInt64(1), Debits: r.GetDecimal(2), Credits: r.GetDecimal(3)), ct)).ConfigureAwait(false);
        var differences = await (db.QueryAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.With01, [bookId], r => new LedgerBalanceDifference(r.GetInt32(0), r.IsDBNull(1) ? null : r.GetInt32(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5)), ct)).ConfigureAwait(false);
        var total = totals.Single();
        return new(id, revision, total.Journals, total.Entries, total.Debits, total.Credits, differences.ToArray(), cut, string.Empty);
    }
}
