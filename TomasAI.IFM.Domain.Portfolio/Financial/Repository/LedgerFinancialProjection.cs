
using Newtonsoft.Json;


using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Applies recorded ledger facts without re-running financial decisions or contacting an external service.</summary>
public sealed class LedgerFinancialProjection(IPostgresEventTransaction transactions)
{
    /// <summary>Rebuilds one committed event atomically, including its checkpoint. Duplicate applications do not repost balances.</summary>
    public Task ReplayAsync(IFinancialCompletedEvent completed, CancellationToken token = default)
    {
        var (facts, revision, receipts) = completed switch
        {
            LedgerPostingCompletedEvent single => (single.ProjectionFacts, single.Receipt.FinancialRevision,
                new[] { new LedgerPostedTransaction { Ordinal = 1, TransactionId = single.Receipt.TransactionId,
                    JournalId = single.Receipt.JournalId, JournalHash = single.Receipt.JournalHash,
                    Source = single.Receipt.Source, ObligationId = single.Receipt.ObligationId } }),
            LedgerPostingBatchCompletedEvent batch => (batch.ProjectionFacts, batch.Receipt.FinancialRevision, batch.Receipt.Items),
            _ => throw new InvalidOperationException("Unsupported ledger event.")
        };
        if (facts is not { Version: 1 } || facts.Items.Length == 0)
            throw new InvalidDataException($"Event {completed.Id} predates complete ledger facts; an explicit baseline is required.");
        if (completed.EventId <= 0 || completed.Id == Guid.Empty || revision <= 0)
            throw new InvalidDataException("Ledger reconstruction requires a committed source event.");
        return transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01, [completed.PortfolioId], ct)).ConfigureAwait(false);
            if (!await (RecordAppliedAsync(db, completed, revision, ct)).ConfigureAwait(false)) return false;
            var current = await (LockAuthorityAsync(db, completed.PortfolioId, null, ct)).ConfigureAwait(false);
            if (revision != current.Revision + 1)
                throw new InvalidDataException("Financial ledger source events must be rebuilt in revision order.");
            foreach (var item in facts.Items.OrderBy(x => x.Ordinal))
            {
                await (ApplyPostingAsync(db, completed.PortfolioId, completed.OperationId, revision, completed.CommittedAtUtc, item, ct)).ConfigureAwait(false);
                await (ApplyEffectsAsync(db, completed.PortfolioId, item, ct)).ConfigureAwait(false);
            }
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,
                [completed.PortfolioId, completed.OperationId, facts.ActorName, completed.InputHash, completed.EventId, revision], ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01, [completed.PortfolioId, revision], ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert05,
                [completed.PortfolioId, completed.OperationId, facts.ExecutionId, completed.InputHash, completed.GetType().FullName!,
                 Json(receipts), completed.Id, completed.CommittedAtUtc, revision, Json(completed)], ct)).ConfigureAwait(false);
            return true;
        }, token);
    }

    internal static async Task<bool> RecordAppliedAsync(EnlistedEventTransaction db, IFinancialCompletedEvent completed,
        long revision, CancellationToken ct) => await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerFinancialProjection.InsertCheckpoint,
            [completed.Id, completed.PortfolioId, completed.EventId, revision], ct)).ConfigureAwait(false) == 1;

    /// <summary>Uses the accepted IDs, amounts, account versions and lines exactly as recorded.</summary>
    internal static async Task ApplyPostingAsync(EnlistedEventTransaction db, int portfolioId, Guid operationId, long revision,
        DateTime committedAtUtc, AcceptedLedgerPosting fact, CancellationToken ct)
    {
        var item = fact.Request;
        if (item.TransactionKind == LedgerTransactionKind.WithdrawalRequested)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert07,
                [fact.ObligationId, portfolioId, item.BookId, item.FundId, item.Source.SourceEventId.ToString("N"), item.Amount, revision, PayloadJson(item.MovementEvidence)], ct)).ConfigureAwait(false);
        else if (item.TransactionKind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)
            Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Update02,
                [item.Amount, item.TransactionKind == LedgerTransactionKind.WithdrawalSettled ? "Settled" : "Cancelled", revision,
                 PayloadJson(item.MovementEvidence), fact.ObligationId, portfolioId, item.BookId, item.FundId], ct)).ConfigureAwait(false) == 1,
                FinancialReasons.SourceConflict, "Required preceding withdrawal fact is missing during reconstruction.");
        await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert01,
            [fact.TransactionId, item.BookId, portfolioId, item.FundId, operationId, fact.Ordinal, (int)item.TransactionKind,
             item.Source.System, item.Source.SourceEventId.ToString("N"), item.Source.SourceContentHash, item.Amount,
             item.AccountingDate, item.ValueDate, item.SettlementDate, fact.ObligationId, item.RelatedJournalId, PayloadJson(item)], ct)).ConfigureAwait(false);
        if (fact.JournalId is { } journalId)
        {
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert02,
                [journalId, fact.TransactionId, item.BookId, portfolioId, item.FundId, operationId, item.AccountingDate, item.ValueDate,
                 item.SettlementDate, (int)item.TransactionKind, item.Source.SourceContentHash, fact.RuleId, fact.RuleVersion,
                 item.TransactionKind == LedgerTransactionKind.Reversal ? item.RelatedJournalId : null, committedAtUtc, revision, fact.JournalHash], ct)).ConfigureAwait(false);
            foreach (var line in fact.Lines.OrderBy(x => x.Account.AccountId).ThenBy(x => x.FundId).ThenBy(x => x.Ordinal))
                await (ApplyLineAsync(db, item.BookId, journalId, line, revision, ct)).ConfigureAwait(false);
        }
        await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert03,
            [item.BookId, item.Source.System, item.Source.SourceEventId.ToString("N"), item.TransactionKind.ToString(), item.Source.SourceContentHash, operationId, fact.JournalId], ct)).ConfigureAwait(false);
        if (item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert04,
                [item.BookId, item.FundId, $"{item.Source.OrderId}:{item.Source.TradeId}", item.TransactionKind == LedgerTransactionKind.RealizedPnl ? 0m : item.Amount, item.Source.SourceSequence, revision], ct)).ConfigureAwait(false);
    }

    internal static async Task ApplyLineAsync(EnlistedEventTransaction db, int bookId, long journalId, AcceptedLedgerLine line,
        long revision, CancellationToken ct)
    {
        await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert08,
            [journalId, line.Ordinal, bookId, line.Account.AccountId, line.Account.Version, line.FundId, line.Debit, line.Credit,
             line.SourceLineReference, line.OrderId, line.TradeId], ct)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert09,
            [bookId, line.Account.AccountId, line.FundId, line.Debit, line.Credit, revision], ct)).ConfigureAwait(false);
    }

    /// <summary>Canonical UTC JSON prevents event-codec timestamp normalization from changing reconstructed payloads.</summary>
    internal static System.Data.Common.DbParameter PayloadJson<T>(T value) => PostgresValues.Jsonb(JsonConvert.SerializeObject(value, new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc }));

    internal static async Task ApplyEffectsAsync(EnlistedEventTransaction db, int portfolioId, AcceptedLedgerPosting fact, CancellationToken ct)
    {
        if (fact.FundingCashPaid is { } paid)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Insert06,
                [fact.TransactionId, fact.Request.CapacityReservationId, (int)fact.Request.FundingComponent, paid], ct)).ConfigureAwait(false);
        if (fact.BecameOverdrawn)
            await (db.ExecuteAsync(PortfolioDbSql.Financial.GeneralLedgerStore.Update01, [portfolioId], ct)).ConfigureAwait(false);
    }
}
