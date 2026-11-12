
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Single and bounded batch posting share the same financial fence and event transaction.</summary>
public sealed class GeneralLedgerStore(IPostgresEventTransaction transactions,
    FinancialDevelopmentPolicy? developmentPolicy = null, TimeProvider? timeProvider = null) : IGeneralLedgerStore
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public async Task<T> PostAsync<T>(IFinancialRequest request, IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate, Func<LedgerCommitInfo, T> complete, CancellationToken token = default) where T : class, IFinancialCompletedEvent
    {
        ArgumentNullException.ThrowIfNull(items);
        var capacity = (request as PostFundTransactionsCommand)?.Body.ExecutionCapacity;
        Require(items.Count <= 100 && (items.Count > 0 || capacity is not null), FinancialReasons.InvalidContract, "Posting requires transactions or source-verified execution capacity facts.");
        Require(items.Select(x => (x.Request.Source.System, x.Request.Source.SourceEventId, x.Request.TransactionKind)).Distinct().Count() == items.Count,
            FinancialReasons.SourceConflict, "Batch contains duplicate source identities.");
        try
        {
            return await transactions.ExecuteAsync(async (db, cancellation) =>
        {
            var replay = await (ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation)).ConfigureAwait(false);
            if (replay is not null) return replay;
            var authority = await (LockAuthorityAsync(db, request.PortfolioId, null, cancellation)).ConfigureAwait(false);
            // A duplicate may have committed while this attempt waited on the financial fence.
            replay = await (ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation)).ConfigureAwait(false);
            if (replay is not null) return replay;
            // Confirmed Trade facts settle under the current fence; they do not admit another order.
            // Their committed source and original approval are verified below in this same transaction.
            Require(capacity is not null || authority.Revision == request.ExpectedFinancialRevision, FinancialReasons.RevisionConflict, "Financial state changed since the request was prepared.");
            var revision = checked(authority.Revision + 1); var now = businessClock.GetUtcNow().UtcDateTime; var eventId = Guid.NewGuid();
            Require(now < request.ExpiresAtUtc, FinancialReasons.TimeExpired, "Posting expired before acquiring financial authority.");
            var receipts = new List<LedgerPostedTransaction>();
            var facts = new List<AcceptedLedgerPosting>();
            // Configuration writers take the same financial authority fence. Reuse immutable
            // account/rule/period evidence only within this locked transaction and exact key.
            var periods = new Dictionary<(int Book, DateOnly Date), bool>();
            var rules = new Dictionary<(int Book, Guid Rule, long Version, DateOnly Date), LedgerPostingRule>();
            var accounts = new Dictionary<(int Book, int Account, long Version, bool Reversal), bool>();
            // The authority fence serializes receipt writers, and this batch rejects duplicate source identities above.
            var existingSources = (await db.QueryAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select01,
                [authority.Book.BookId, items.Select(x => x.Request.Source.System).ToArray(),
                 items.Select(x => x.Request.Source.SourceEventId.ToString("N")).ToArray(),
                 items.Select(x => x.Request.TransactionKind.ToString()).ToArray()],
                row => (Ordinal: row.GetInt64(0), Hash: row.GetString(1), Operation: row.GetGuid(2)),
                cancellation).ConfigureAwait(false)).ToDictionary(x => x.Ordinal);
            long inputOrdinal = 0;
            foreach (var prepared in items)
            {
                inputOrdinal++;
                var item = prepared.Request;
                Require(item.BookId == authority.Book.BookId, FinancialReasons.AuthorityDenied, "Posting book does not match Portfolio authority.");
                Require(item.Source.SourceEventId != Guid.Empty && !string.IsNullOrWhiteSpace(item.Source.System) &&
                    !string.IsNullOrWhiteSpace(item.Source.SourceContentHash), FinancialReasons.InvalidContract, "Qualified source identity/hash is required.");
                await (ValidateFundSourcesAsync(db, authority.Book, item.FundId, false, cancellation)).ConfigureAwait(false);
                if (existingSources.TryGetValue(inputOrdinal, out var previous))
                {
                    if (capacity is not null && string.Equals(previous.Hash, item.Source.SourceContentHash, StringComparison.Ordinal))
                        continue; // A cumulative execution can overlap an earlier committed fill, never post it twice.
                    throw new FinancialOperationException(string.Equals(previous.Hash, item.Source.SourceContentHash, StringComparison.Ordinal)
                        ? FinancialReasons.AlreadyPosted : FinancialReasons.SourceConflict,
                        "Source has already been committed.", FinancialCommitDisposition.NoNewMutation, previous.Operation);
                }
                var periodKey = (item.BookId, item.AccountingDate);
                if (!periods.ContainsKey(periodKey))
                {
                    var period = await db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select02, [item.BookId, item.AccountingDate], cancellation).ConfigureAwait(false);
                    Require(period is string state && string.Equals(state, "Open", StringComparison.Ordinal), FinancialReasons.ClosedPeriod, "Accounting date is not in an open period.");
                    periods.Add(periodKey, true);
                }
                var ruleKey = (item.BookId, item.PostingRule.RuleId, (long)item.PostingRule.Version, item.AccountingDate);
                if (!rules.TryGetValue(ruleKey, out var rule))
                {
                    var ruleJson = await db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select03, [item.BookId, item.PostingRule.RuleId, (long)item.PostingRule.Version, item.AccountingDate], cancellation).ConfigureAwait(false);
                    Require(ruleJson is string, FinancialReasons.InvalidAccount, "Exact posting rule is not active.");
                    rule = Decode<LedgerPostingRule>((string)ruleJson!);
                    rules.Add(ruleKey, rule);
                }
                IReadOnlyList<(decimal Amount, long Sequence)> valuation = [];
                if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    valuation = await db.QueryAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select04, [item.BookId, item.FundId, PositionKey(item)], r => (Amount: r.GetDecimal(0), Sequence: r.GetInt64(1)), cancellation).ConfigureAwait(false);
                if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    Require(valuation.Count == 0 || item.Source.SourceSequence > valuation[0].Sequence, FinancialReasons.SourceConflict, "Valuation/realization source is stale.");
                IReadOnlyList<PlannedLedgerLine>? reversal = null; decimal remaining = 0;
                if (item.TransactionKind == LedgerTransactionKind.Reversal)
                {
                    var journalOwner = await (db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select05, [item.BookId, item.RelatedJournalId], cancellation)).ConfigureAwait(false);
                    Require(journalOwner is int owner && owner == item.FundId, FinancialReasons.AuthorityDenied, "Original journal belongs to a different Fund/book.");
                    reversal = await (ReadJournalLines(db, item.RelatedJournalId!.Value, cancellation)).ConfigureAwait(false);
                    var already = Convert.ToDecimal(await (db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select06, [item.RelatedJournalId], cancellation)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
                    remaining = reversal.Sum(x => x.Debit) - already;
                }
                var plan = calculate(item, rule, (valuation.Count == 0 ? 0 : valuation[0].Amount), reversal, remaining);
                Require((item.CapacityReservationId is null) == (item.FundingComponent == CapacityFundingComponent.Undefined),
                    FinancialReasons.InvalidContract, "Reservation and funding component must be supplied together.");
                if (item.TransactionKind == LedgerTransactionKind.OpeningBalance)
                {
                    Require(developmentPolicy?.IsDevelopmentEnvironment == true && string.Equals(authority.Book.Environment,"Emulator",StringComparison.Ordinal),
                        FinancialReasons.AuthorityDenied, "Opening capital is allowed only on a Development host for an Emulator book.");
                    Require(string.Equals(authority.State,"Importing",StringComparison.Ordinal) && !authority.Book.MigrationQualified, FinancialReasons.AuthorityDenied,
                        "Opening balances are restricted to an unqualified book's explicit import phase.");
                    Require(string.Equals(item.Source.System,"DevelopmentOpeningCapital",StringComparison.Ordinal), FinancialReasons.AuthorityDenied,
                        "Opening capital must be explicitly identified as DevelopmentOpeningCapital, never as a confirmed external deposit.");
                }
                var discretionary = plan.IsDiscretionarySpending;
                if (discretionary)
                {
                    Require(string.Equals(authority.State,"Active",StringComparison.Ordinal), FinancialReasons.AuthorityRevoked, "New spending is disabled.");
                    await (ValidateFundSourcesAsync(db, developmentPolicy?.TradingBook(authority.Book) ?? authority.Book, item.FundId, true, cancellation)).ConfigureAwait(false);
                    Require(await (AvailableCash(db, item.BookId, item.FundId, cancellation)).ConfigureAwait(false) >= item.Amount,
                        FinancialReasons.InsufficientCash, "Cash is unavailable after existing withdrawals and trade commitments.");
                }
                if (item.CounterpartyFundId is { } other && item.TransactionKind == LedgerTransactionKind.FundTransfer)
                    await (ValidateFundSourcesAsync(db, authority.Book, other, false, cancellation)).ConfigureAwait(false);
                var obligation = await (ApplyObligation(db, request, item, revision, cancellation)).ConfigureAwait(false);
                var journalId = plan.Lines.Count == 0 ? null : prepared.JournalId;
                Require(plan.Lines.Count == 0 || journalId is > 0, FinancialReasons.InvalidContract, "Generated journal identity is missing.");
                var writes = new List<(string Sql, object?[] Parameters)>(plan.Lines.Count + 3)
                {
                    (PortfolioDbSql.Financial_GeneralLedgerStore_Insert01, [prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,receipts.Count+1,(int)item.TransactionKind,
                        item.Source.System,item.Source.SourceEventId.ToString("N"),item.Source.SourceContentHash,item.Amount,item.AccountingDate,item.ValueDate,
                        item.SettlementDate,obligation,item.RelatedJournalId,LedgerFinancialProjection.PayloadJson(item)])
                };
                // Hash is supplied by the canonical domain model through the immutable request/rule/line plan.
                var journalHash = journalId is null ? null : plan.ContentHash;
                Require(journalId is null || journalHash?.Length == 64, FinancialReasons.InvalidContract, "Canonical journal hash is required.");
                if (journalId is not null)
                {
                    writes.Add((PortfolioDbSql.Financial_GeneralLedgerStore_Insert02, [journalId,prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,item.AccountingDate,item.ValueDate,item.SettlementDate,
                            (int)item.TransactionKind,item.Source.SourceContentHash,rule.RuleId,(long)rule.Version,
                            item.TransactionKind==LedgerTransactionKind.Reversal?item.RelatedJournalId:null,now,revision,journalHash]));
                    foreach (var line in plan.Lines.OrderBy(x => x.Account.AccountId).ThenBy(x => x.FundId).ThenBy(x => x.Ordinal))
                    {
                        if (line.FundId is { } lineFund)
                            await (ValidateFundSourcesAsync(db, authority.Book, lineFund, false, cancellation)).ConfigureAwait(false);
                        await ValidateLineAccountAsync(db, item.BookId, line, item.TransactionKind == LedgerTransactionKind.Reversal, accounts, cancellation).ConfigureAwait(false);
                        writes.Add((PortfolioDbSql.Financial_GeneralLedgerStore_InsertLineAndBalance,
                            LedgerFinancialProjection.LineParameters(item.BookId, journalId.Value,
                                new(line.Ordinal, line.Account, line.FundId, line.Debit, line.Credit, line.OrderId, line.TradeId, line.SourceLineReference), revision)));
                    }
                }
                var sourceReceipt = (Sql: PortfolioDbSql.Financial_GeneralLedgerStore_Insert03, Parameters: new object?[]
                    { item.BookId, item.Source.System, item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString(),
                      item.Source.SourceContentHash, request.OperationId, journalId });
                if (item.CapacityReservationId is null)
                    writes.Add(sourceReceipt);
                // Statements execute in order so ledger triggers can see the preceding journal insert.
                await db.ExecuteBatchAsync(writes, cancellation).ConfigureAwait(false);
                decimal? fundingCashPaid = null;
                if (item.CapacityReservationId is not null)
                {
                    fundingCashPaid = await (RecordFundingSettlement(db, item, prepared.TransactionId, journalId, cancellation)).ConfigureAwait(false);
                    await db.ExecuteAsync(sourceReceipt.Sql, sourceReceipt.Parameters, cancellation).ConfigureAwait(false);
                }
                if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Insert04, [item.BookId, item.FundId, PositionKey(item), item.TransactionKind == LedgerTransactionKind.RealizedPnl ? 0m : item.Amount, item.Source.SourceSequence, revision], cancellation)).ConfigureAwait(false);
                var becameOverdrawn = plan.IsActualFinancialFact && await (AvailableCash(db, item.BookId, item.FundId, cancellation)).ConfigureAwait(false) < 0;
                if (becameOverdrawn)
                    await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Update01, [request.PortfolioId], cancellation)).ConfigureAwait(false);
                facts.Add(new AcceptedLedgerPosting
                {
                    Ordinal = receipts.Count + 1, TransactionId = prepared.TransactionId, JournalId = journalId,
                    JournalHash = journalHash, Request = item, RuleId = rule.RuleId, RuleVersion = rule.Version,
                    Lines = plan.Lines.Select(x => new AcceptedLedgerLine(x.Ordinal, x.Account, x.FundId, x.Debit, x.Credit,
                        x.OrderId, x.TradeId, x.SourceLineReference)).ToArray(),
                    ObligationId = obligation, FundingCashPaid = fundingCashPaid, BecameOverdrawn = becameOverdrawn
                });
                receipts.Add(new() { Ordinal = receipts.Count + 1, TransactionId = prepared.TransactionId, JournalId = journalId, JournalHash = journalHash, Source = item.Source, ObligationId = obligation });
            }
            var completed = complete(new(revision, now, eventId, receipts));
            var capacityFacts = capacity is null ? null : await ExecutionCapacitySettlement.PrepareAndApplyAsync(db,
                request.PortfolioId, capacity, revision, cancellation).ConfigureAwait(false);
            var projectionFacts = new LedgerProjectionFacts
            { ActorName = request.Subject.Name, ExecutionId = request.Subject.EntityId, Items = facts.ToArray(), ExecutionCapacity = capacityFacts };
            completed = (T)(IFinancialCompletedEvent)(completed switch
            {
                LedgerPostingCompletedEvent single => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(single, single with { SchemaVersion = 2, ProjectionFacts = projectionFacts }),
                LedgerPostingBatchCompletedEvent batch => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(batch, batch with { SchemaVersion = 2, ProjectionFacts = projectionFacts }),
                _ => throw new InvalidOperationException("Ledger completion must carry accepted posting facts.")
            });
            Require(completed.Id == eventId && completed.OperationId == request.OperationId && string.Equals(completed.InputHash,request.InputSha256,StringComparison.Ordinal),
                FinancialReasons.RequestMismatch, "Completion factory did not preserve committed identity.");
            await (SaveOutcomeAsync(db, request, completed, revision, false, cancellation)).ConfigureAwait(false);
            await (LedgerFinancialProjection.RecordAppliedAsync(db, completed, revision, cancellation)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Insert05, [request.PortfolioId, request.OperationId, request.Subject.EntityId, request.InputSha256, typeof(T).FullName!, Json(receipts), eventId, now, revision, Json(completed)], cancellation)).ConfigureAwait(false);
            return completed;
        }, token).ConfigureAwait(false);
        }
        catch (FunctionCommitOutcomeUnknownException)
        {
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { var receipt = await (transactions.ExecuteAsync((db, ct) => ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct), recovery.Token)).ConfigureAwait(false); if (receipt is not null) return receipt; }
            catch { /* Failure to reconcile cannot establish rollback. */ }
            throw;
        }
    }

    internal static async Task<decimal> AvailableCash(EnlistedEventTransaction db, int bookId, int fundId, CancellationToken token)
    {
        return Convert.ToDecimal(await db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_AvailableCash,
            [bookId, fundId], token).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
    }

    static async Task<decimal> RecordFundingSettlement(EnlistedEventTransaction db, LedgerPostingRequest item, long transactionId, long? journalId, CancellationToken token)
    {
        Require(journalId is not null && (item.FundingComponent == CapacityFundingComponent.EntryFees && item.TransactionKind == LedgerTransactionKind.Commission ||
            item.FundingComponent is CapacityFundingComponent.SettlementCash or CapacityFundingComponent.MarginFunding && item.TransactionKind == LedgerTransactionKind.TradeSettlement),
            FinancialReasons.InvalidContract, "Funding clearance requires a posted entry fee, settlement or margin cash outflow.");
        var scope = await (db.QueryAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select09, [item.CapacityReservationId, item.BookId, item.FundId], r => (Order: r.GetInt32(0), Execution: r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1), Request: Decode<CapacityReservationRequest>(r.GetString(2))), token)).ConfigureAwait(false);
        Require(scope.Count == 1 && scope[0].Execution is { } execution && Guid.TryParse(item.Source.SourceEntityId, out var sourceExecution) && sourceExecution == execution &&
            item.Source.OrderId == scope[0].Order && item.Source.TradeId is { } trade && scope[0].Request.TradeIds.Contains(trade),
            FinancialReasons.AuthorityDenied, "Funding source must identify the exact consumed execution, order, trade and Fund.");
        var paid = Convert.ToDecimal(await (db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select10, [journalId, item.FundId], token)).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);
        Require(paid > 0, FinancialReasons.InvalidContract, "Funding clearance requires an actual cash outflow in the committed journal.");
        await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Insert06,
            [transactionId, item.CapacityReservationId, (int)item.FundingComponent, paid], token)).ConfigureAwait(false);
        return paid;
    }

    static async Task<Guid?> ApplyObligation(EnlistedEventTransaction db, IFinancialRequest request, LedgerPostingRequest item, long revision, CancellationToken token)
    {
        if (item.TransactionKind == LedgerTransactionKind.WithdrawalRequested)
        {
            var id = Guid.NewGuid();
            await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Insert07, [id, request.PortfolioId, item.BookId, item.FundId, item.Source.SourceEventId.ToString("N"), item.Amount, revision, LedgerFinancialProjection.PayloadJson(item.MovementEvidence)], token)).ConfigureAwait(false);
            return id;
        }
        if (item.TransactionKind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)
        {
            var changed = await (db.ExecuteAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Update02, [item.Amount,item.TransactionKind==LedgerTransactionKind.WithdrawalSettled?"Settled":"Cancelled",revision,LedgerFinancialProjection.PayloadJson(item.MovementEvidence),
                    item.RelatedObligationId,request.PortfolioId,item.BookId,item.FundId], token)).ConfigureAwait(false);
            Require(changed == 1, FinancialReasons.SourceConflict, "Withdrawal obligation is missing, settled or insufficient.");
            return item.RelatedObligationId;
        }
        return null;
    }

    static async Task ValidateLineAccountAsync(EnlistedEventTransaction db, int bookId, PlannedLedgerLine line, bool reversal,
        Dictionary<(int Book, int Account, long Version, bool Reversal), bool> accounts, CancellationToken token)
    {
        var key = (bookId, line.Account.AccountId, line.Account.Version, reversal);
        if (!accounts.TryGetValue(key, out var fundRequired))
        {
            var valid = await (db.ScalarAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select11, [bookId, line.Account.AccountId, line.Account.Version, reversal], token)).ConfigureAwait(false);
            Require(valid is bool, FinancialReasons.InvalidAccount, "Account version is inactive.");
            fundRequired = (bool)valid!;
            accounts.Add(key, fundRequired);
        }
        Require(!fundRequired || line.FundId is > 0, FinancialReasons.InvalidAccount, "Account is missing its required Fund dimension.");
    }
    static string PositionKey(LedgerPostingRequest item) => $"{item.Source.OrderId}:{item.Source.TradeId}";
    static Task<IReadOnlyList<PlannedLedgerLine>> ReadJournalLines(EnlistedEventTransaction db, long journalId, CancellationToken token) => db.QueryAsync(PortfolioDbSql.Financial_GeneralLedgerStore_Select12, [journalId], r => new PlannedLedgerLine(r.GetInt32(0), new(r.GetInt32(1), r.GetInt64(2)), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetDecimal(4), r.GetDecimal(5),
            r.IsDBNull(6) ? null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetInt32(7), r.GetString(8)), token);
}
