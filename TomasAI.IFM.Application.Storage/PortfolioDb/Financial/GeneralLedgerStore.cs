using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public delegate LedgerPostingPlan LedgerCalculation(LedgerPostingRequest request, LedgerPostingRule rule,
    decimal previousUnrealized, IReadOnlyList<PlannedLedgerLine>? reversalLines, decimal remainingReversibleAmount);

public interface IGeneralLedgerStore
{
    Task<T> PostAsync<T>(IFinancialRequest request, IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate, Func<LedgerCommitInfo, T> complete, CancellationToken token = default) where T : class, IFinancialCompletedEvent;
}

/// <summary>Single and bounded batch posting share the same financial fence and event transaction.</summary>
public sealed class GeneralLedgerStore(IPostgresEventTransaction transactions,
    FinancialDevelopmentPolicy? developmentPolicy = null) : IGeneralLedgerStore
{
    public async Task<T> PostAsync<T>(IFinancialRequest request, IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate, Func<LedgerCommitInfo, T> complete, CancellationToken token = default) where T : class, IFinancialCompletedEvent
    {
        Require(items.Count is >= 1 and <= 100, FinancialReasons.InvalidContract, "Posting batch must contain 1–100 transactions.");
        Require(items.Select(x => (x.Request.Source.System, x.Request.Source.SourceEventId, x.Request.TransactionKind)).Distinct().Count() == items.Count,
            FinancialReasons.SourceConflict, "Batch contains duplicate source identities.");
        try
        {
            return await transactions.ExecuteAsync(async (db, cancellation) =>
        {
            var replay = await ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation);
            if (replay is not null) return replay;
            var authority = await LockAuthorityAsync(db, request.PortfolioId, null, cancellation);
            // A duplicate may have committed while this attempt waited on the financial fence.
            replay = await ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation);
            if (replay is not null) return replay;
            Require(authority.Revision == request.ExpectedFinancialRevision, FinancialReasons.RevisionConflict, "Financial state changed since the request was prepared.");
            var revision = checked(authority.Revision + 1); var now = DateTime.UtcNow; var eventId = Guid.NewGuid();
            Require(now < request.ExpiresAtUtc, FinancialReasons.TimeExpired, "Posting expired before acquiring financial authority.");
            var receipts = new List<LedgerPostedTransaction>();
            foreach (var prepared in items)
            {
                var item = prepared.Request;
                Require(item.BookId == authority.Book.BookId, FinancialReasons.AuthorityDenied, "Posting book does not match Portfolio authority.");
                Require(item.Source.SourceEventId != Guid.Empty && !string.IsNullOrWhiteSpace(item.Source.System) &&
                    !string.IsNullOrWhiteSpace(item.Source.SourceContentHash), FinancialReasons.InvalidContract, "Qualified source identity/hash is required.");
                await ValidateFundSourcesAsync(db, authority.Book, item.FundId, false, cancellation);
                var previous = await db.QueryAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select01, [item.BookId, item.Source.System, item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString()], r => (Hash: r.GetString(0), Operation: r.GetGuid(1)), cancellation);
                if (previous.Count > 0) throw new FinancialOperationException(previous[0].Hash == item.Source.SourceContentHash ? FinancialReasons.AlreadyPosted : FinancialReasons.SourceConflict,
                    "Source has already been committed.", FinancialCommitDisposition.NoNewMutation, previous[0].Operation);
                var period = await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select02, [item.BookId, item.AccountingDate], cancellation);
                Require(period is string state && state == "Open", FinancialReasons.ClosedPeriod, "Accounting date is not in an open period.");
                var ruleJson = await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select03, [item.BookId, item.PostingRule.RuleId, (long)item.PostingRule.Version, item.AccountingDate], cancellation);
                Require(ruleJson is string, FinancialReasons.InvalidAccount, "Exact posting rule is not active.");
                var rule = Decode<LedgerPostingRule>((string)ruleJson!);
                var valuation = await db.QueryAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select04, [item.BookId, item.FundId, PositionKey(item)], r => (Amount: r.GetDecimal(0), Sequence: r.GetInt64(1)), cancellation);
                if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    Require(valuation.Count == 0 || item.Source.SourceSequence > valuation[0].Sequence, FinancialReasons.SourceConflict, "Valuation/realization source is stale.");
                IReadOnlyList<PlannedLedgerLine>? reversal = null; decimal remaining = 0;
                if (item.TransactionKind == LedgerTransactionKind.Reversal)
                {
                    var journalOwner = await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select05, [item.BookId, item.RelatedJournalId], cancellation);
                    Require(journalOwner is int owner && owner == item.FundId, FinancialReasons.AuthorityDenied, "Original journal belongs to a different Fund/book.");
                    reversal = await ReadJournalLines(db, item.RelatedJournalId!.Value, cancellation);
                    var already = Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select06, [item.RelatedJournalId], cancellation));
                    remaining = reversal.Sum(x => x.Debit) - already;
                }
                var plan = calculate(item, rule, valuation.FirstOrDefault().Amount, reversal, remaining);
                Require((item.CapacityReservationId is null) == (item.FundingComponent == CapacityFundingComponent.Undefined),
                    FinancialReasons.InvalidContract, "Reservation and funding component must be supplied together.");
                if (item.TransactionKind == LedgerTransactionKind.OpeningBalance)
                {
                    Require(developmentPolicy?.IsDevelopmentEnvironment == true && authority.Book.Environment == "Emulator",
                        FinancialReasons.AuthorityDenied, "Opening capital is allowed only on a Development host for an Emulator book.");
                    Require(authority.State == "Importing" && !authority.Book.MigrationQualified, FinancialReasons.AuthorityDenied,
                        "Opening balances are restricted to an unqualified book's explicit import phase.");
                    Require(item.Source.System == "DevelopmentOpeningCapital", FinancialReasons.AuthorityDenied,
                        "Opening capital must be explicitly identified as DevelopmentOpeningCapital, never as a confirmed external deposit.");
                }
                var discretionary = plan.IsDiscretionarySpending;
                if (discretionary)
                {
                    Require(authority.State == "Active", FinancialReasons.AuthorityRevoked, "New spending is disabled.");
                    await ValidateFundSourcesAsync(db, authority.Book, item.FundId, true, cancellation);
                    Require(await AvailableCash(db, item.BookId, item.FundId, cancellation) >= item.Amount,
                        FinancialReasons.InsufficientCash, "Cash is unavailable after existing withdrawals and trade commitments.");
                }
                if (item.CounterpartyFundId is { } other && item.TransactionKind == LedgerTransactionKind.FundTransfer)
                    await ValidateFundSourcesAsync(db, authority.Book, other, false, cancellation);
                var obligation = await ApplyObligation(db, request, item, revision, cancellation);
                var journalId = plan.Lines.Count == 0 ? null : prepared.JournalId;
                Require(plan.Lines.Count == 0 || journalId is > 0, FinancialReasons.InvalidContract, "Generated journal identity is missing.");
                await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert01, [prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,receipts.Count+1,(int)item.TransactionKind,
                        item.Source.System,item.Source.SourceEventId.ToString("N"),item.Source.SourceContentHash,item.Amount,item.AccountingDate,item.ValueDate,
                        item.SettlementDate,obligation,item.RelatedJournalId,Json(item)], cancellation);
                // Hash is supplied by the canonical domain model through the immutable request/rule/line plan.
                var journalHash = journalId is null ? null : plan.ContentHash;
                Require(journalId is null || journalHash?.Length == 64, FinancialReasons.InvalidContract, "Canonical journal hash is required.");
                if (journalId is not null)
                {
                    await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert02, [journalId,prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,item.AccountingDate,item.ValueDate,item.SettlementDate,
                            (int)item.TransactionKind,item.Source.SourceContentHash,rule.RuleId,(long)rule.Version,
                            item.TransactionKind==LedgerTransactionKind.Reversal?item.RelatedJournalId:null,now,revision,journalHash], cancellation);
                    foreach (var line in plan.Lines.OrderBy(x => x.Account.AccountId).ThenBy(x => x.FundId).ThenBy(x => x.Ordinal))
                    {
                        if (line.FundId is { } lineFund)
                            await ValidateFundSourcesAsync(db, authority.Book, lineFund, false, cancellation);
                        await InsertLine(db, item.BookId, journalId.Value, line, revision, item.TransactionKind == LedgerTransactionKind.Reversal, cancellation);
                    }
                }
                if (item.CapacityReservationId is not null)
                    await RecordFundingSettlement(db, item, prepared.TransactionId, journalId, cancellation);
                await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert03, [item.BookId, item.Source.System, item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString(), item.Source.SourceContentHash, request.OperationId, journalId], cancellation);
                if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert04, [item.BookId, item.FundId, PositionKey(item), item.TransactionKind == LedgerTransactionKind.RealizedPnl ? 0m : item.Amount, item.Source.SourceSequence, revision], cancellation);
                if (plan.IsActualFinancialFact && await AvailableCash(db, item.BookId, item.FundId, cancellation) < 0)
                    await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Update01, [request.PortfolioId], cancellation);
                receipts.Add(new() { Ordinal = receipts.Count + 1, TransactionId = prepared.TransactionId, JournalId = journalId, JournalHash = journalHash, Source = item.Source, ObligationId = obligation });
            }
            var completed = complete(new(revision, now, eventId, receipts));
            Require(completed.Id == eventId && completed.OperationId == request.OperationId && completed.InputHash == request.InputSha256,
                FinancialReasons.RequestMismatch, "Completion factory did not preserve committed identity.");
            await SaveOutcomeAsync(db, request, completed, revision, false, cancellation);
            await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert05, [request.PortfolioId, request.OperationId, request.Subject.EntityId, request.InputSha256, typeof(T).FullName!, Json(receipts), eventId, now, revision, Json(completed)], cancellation);
            return completed;
        }, token).ConfigureAwait(false);
        }
        catch (FunctionCommitOutcomeUnknownException)
        {
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { var receipt = await transactions.ExecuteAsync((db, ct) => ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct), recovery.Token); if (receipt is not null) return receipt; }
            catch { /* Failure to reconcile cannot establish rollback. */ }
            throw;
        }
    }

    internal static async Task<decimal> AvailableCash(EnlistedEventTransaction db, int bookId, int fundId, CancellationToken token)
    {
        var cash = Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select07, [bookId, fundId], token));
        var pending = Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select08, [bookId, fundId], token));
        var held = Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.With01, [bookId, fundId], token));
        return cash - pending - held;
    }

    static async Task RecordFundingSettlement(EnlistedEventTransaction db, LedgerPostingRequest item, long transactionId, long? journalId, CancellationToken token)
    {
        Require(journalId is not null && (item.FundingComponent == CapacityFundingComponent.EntryFees && item.TransactionKind == LedgerTransactionKind.Commission ||
            item.FundingComponent is CapacityFundingComponent.SettlementCash or CapacityFundingComponent.MarginFunding && item.TransactionKind == LedgerTransactionKind.TradeSettlement),
            FinancialReasons.InvalidContract, "Funding clearance requires a posted entry fee, settlement or margin cash outflow.");
        var scope = await db.QueryAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select09, [item.CapacityReservationId, item.BookId, item.FundId], r => (Order: r.GetInt32(0), Execution: r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1), Request: Decode<CapacityReservationRequest>(r.GetString(2))), token);
        Require(scope.Count == 1 && scope[0].Execution is { } execution && Guid.TryParse(item.Source.SourceEntityId, out var sourceExecution) && sourceExecution == execution &&
            item.Source.OrderId == scope[0].Order && item.Source.TradeId is { } trade && scope[0].Request.TradeIds.Contains(trade),
            FinancialReasons.AuthorityDenied, "Funding source must identify the exact consumed execution, order, trade and Fund.");
        var paid = Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select10, [journalId, item.FundId], token));
        Require(paid > 0, FinancialReasons.InvalidContract, "Funding clearance requires an actual cash outflow in the committed journal.");
        await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert06,
            [transactionId, item.CapacityReservationId, (int)item.FundingComponent, paid], token);
    }

    static async Task<Guid?> ApplyObligation(EnlistedEventTransaction db, IFinancialRequest request, LedgerPostingRequest item, long revision, CancellationToken token)
    {
        if (item.TransactionKind == LedgerTransactionKind.WithdrawalRequested)
        {
            var id = Guid.NewGuid();
            await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert07, [id, request.PortfolioId, item.BookId, item.FundId, item.Source.SourceEventId.ToString("N"), item.Amount, revision, Json(item.MovementEvidence)], token);
            return id;
        }
        if (item.TransactionKind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)
        {
            var changed = await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Update02, [item.Amount,item.TransactionKind==LedgerTransactionKind.WithdrawalSettled?"Settled":"Cancelled",revision,Json(item.MovementEvidence),
                    item.RelatedObligationId,request.PortfolioId,item.BookId,item.FundId], token);
            Require(changed == 1, FinancialReasons.SourceConflict, "Withdrawal obligation is missing, settled or insufficient.");
            return item.RelatedObligationId;
        }
        return null;
    }

    static async Task InsertLine(EnlistedEventTransaction db, int bookId, long journalId, PlannedLedgerLine line, long revision, bool reversal, CancellationToken token)
    {
        var valid = await db.ScalarAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select11, [bookId, line.Account.AccountId, line.Account.Version, reversal], token);
        Require(valid is bool fundRequired && (!fundRequired || line.FundId is > 0), FinancialReasons.InvalidAccount, "Account version is inactive or missing its required Fund dimension.");
        await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert08, [journalId, line.Ordinal, bookId, line.Account.AccountId, line.Account.Version, line.FundId, line.Debit, line.Credit, line.SourceLineReference, line.OrderId, line.TradeId], token);
        await db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert09, [bookId, line.Account.AccountId, line.FundId, line.Debit, line.Credit, revision], token);
    }
    static string PositionKey(LedgerPostingRequest item) => $"{item.Source.OrderId}:{item.Source.TradeId}";
    static Task<IReadOnlyList<PlannedLedgerLine>> ReadJournalLines(EnlistedEventTransaction db, long journalId, CancellationToken token) => db.QueryAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Select12, [journalId], r => new PlannedLedgerLine(r.GetInt32(0), new(r.GetInt32(1), r.GetInt64(2)), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetDecimal(4), r.GetDecimal(5),
            r.IsDBNull(6) ? null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetInt32(7), r.GetString(8)), token);
}
