using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Posts linked price deltas; original fills, journals, fees and closed quantities remain audit evidence.</summary>
public static class ClosingPriceReconciliation
{
    public static LedgerPostingBatchRequest Create(OrderExecutionDefinition original, OrderExecutionDefinition corrected,
        FinancialPostingConfiguration configuration, IReadOnlyList<PostedClosingTransaction> posted,
        Guid correctionId, string quoteEvidenceSha256, long sourceSequence, DateTime nowUtc,
        LedgerPostingRule originalRealizationRule)
    {
        if (correctionId == Guid.Empty || quoteEvidenceSha256.Length != 64 || sourceSequence <= 0
            || nowUtc.Kind != DateTimeKind.Utc || original.Id != corrected.Id
            || original.Status != OrderExecutionStatus.Filled || original.PositionType != TradeOrderPositionType.Closing
            || original.Fills.Length != 4 || corrected.Fills.Length != 4 || original.Order.Id != corrected.Order.Id)
            throw new ArgumentException("Complete matching closed execution and verified source evidence required.");
        var items = new List<LedgerPostingRequest>();
        decimal cashDelta = 0;
        foreach (var fill in original.Fills)
        {
            var replacement = corrected.Fills.Single(x => x.ExecutionFillId == fill.ExecutionFillId);
            if (replacement != fill with { Price = replacement.Price, FilledAtUtc = replacement.FilledAtUtc }
                || replacement.Price <= 0 || replacement.FilledAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Only fill price and reconstructed execution time may change.");
            var multiplier = original.Components.SelectMany(x => x.Legs).Single(x => x.TradeLegId == fill.TradeLegId).CashMultiplier;
            var prior = posted.Single(x => x.Request.TransactionKind == LedgerTransactionKind.TradeSettlement
                && x.Request.Source.SourceEventId == fill.ExecutionFillId);
            if (prior.JournalId <= 0 || prior.Request.Amount != fill.Price * fill.SignedQuantity * multiplier)
                throw new ArgumentException("Original journal must match the original settlement exactly.");
            var amount = (replacement.Price - fill.Price) * fill.SignedQuantity * multiplier;
            cashDelta -= amount;
            items.Add(Correction(prior, amount, fill.ExecutionFillId.ToString("N")));
        }
        var realized = posted.Single(x => x.Request.TransactionKind == LedgerTransactionKind.RealizedPnl);
        items.Add(Correction(realized, cashDelta, "realized"));
        var settlementRule = configuration.Rules.Single(x => x.Kind == LedgerTransactionKind.TradeSettlement);
        var realizationRule = configuration.Rules.Single(x => x.Kind == LedgerTransactionKind.RealizedPnl);
        if (realizationRule.Debit != settlementRule.Debit || realizationRule.Credit != originalRealizationRule.Credit)
            throw new ArgumentException("Realization must clear the settlement asset without moving cash.");
        if (originalRealizationRule.Debit == settlementRule.Credit)
        {
            // The legacy rule credited cash a second time. Move that original recognition
            // to settlement basis, with a linked journal; never mutate the original journal.
            var reclassification = realized with { Request = realized.Request with {
                TransactionKind = LedgerTransactionKind.TradeSettlement } };
            items.Add(Correction(reclassification, realized.Request.Amount, "legacy-realization-cash-reclassification"));
        }
        else if (originalRealizationRule.Debit != settlementRule.Debit)
            throw new ArgumentException("Original realization binding is not a recognized cash or asset rule.");
        return new() { BookId = configuration.BookId, Items = [..items],
            BatchSourceReference = $"VerifiedEmulatorClose:{correctionId:N}", ManifestHash = FinancialCanonicalHash.Compute(items.ToArray()) };

        LedgerPostingRequest Correction(PostedClosingTransaction prior, decimal amount, string suffix)
        {
            var rule = configuration.Rules.Single(x => x.Kind == prior.Request.TransactionKind);
            var hash = FinancialCanonicalHash.Compute(new { correctionId, quoteEvidenceSha256, prior, amount, corrected.Fills });
            var reference = $"VerifiedEmulatorClose:{correctionId:N}:{suffix}";
            return prior.Request with { Amount = amount, RelatedJournalId = prior.JournalId,
                Description = "Verified stored bid/ask closing-price correction; original journal retained",
                Authority = configuration.Authority,
                PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
                Source = prior.Request.Source with { System = "IFM-VerifiedEmulatorClose", SourceSequence = sourceSequence,
                    SourceEventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(reference))[..16]),
                    SourceContentHash = hash, OccurredAtUtc = corrected.CompletedAtUtc!.Value, FillId = reference },
                MovementEvidence = new() { Status = MovementStatus.Confirmed, SourceReference = reference,
                    ObservedAtUtc = corrected.CompletedAtUtc!.Value, ReceivedAtUtc = nowUtc,
                    ValidUntilUtc = nowUtc.AddDays(1), ContentHash = hash } };
        }
    }
}

public sealed record PostedClosingTransaction(long JournalId, LedgerPostingRequest Request);
