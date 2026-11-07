using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Creates linked delta journals for verified opening multipliers without replacing fills or prior postings.</summary>
public static class OpeningMultiplierReconciliation
{
    public static LedgerPostingBatchRequest Create(EstablishedTradeDefinition original,
        IReadOnlyList<TradeLegDefinition> corrected, FinancialPostingConfiguration configuration,
        IReadOnlyDictionary<Guid, PostedOpeningSettlement> posted, Guid reconciliationId, DateTime atUtc)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(corrected);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(posted);
        if (reconciliationId == Guid.Empty || atUtc.Kind != DateTimeKind.Utc || !original.Id.IsValid
            || configuration.FundId != original.Id.FundId || original.ClosingFills.Length != 0 || corrected.Count == 0
            || corrected.Select(leg => leg.TradeLegId).Distinct().Count() != corrected.Count)
            throw new ArgumentException("An identified, unclosed opening and verified unique corrections are required.");
        var rule = configuration.Rules.Single(value => value.Kind == LedgerTransactionKind.TradeSettlement);
        if (!rule.RequiresConfirmedMovement) throw new ArgumentException("Confirmed settlement rule required.");
        var items = new List<LedgerPostingRequest>();
        foreach (var change in corrected)
        {
            var leg = original.Legs.Single(value => value.TradeLegId == change.TradeLegId);
            if (change.CashMultiplier <= 0 || leg.CashMultiplier <= 0 || change.CashMultiplier == leg.CashMultiplier
                || change != leg with { CashMultiplier = change.CashMultiplier })
                throw new ArgumentException("Only an existing leg's verified multiplier may change.");
            var fills = original.OriginalFills.Where(fill => fill.TradeLegId == leg.TradeLegId).ToArray();
            if (fills.Length == 0 || fills.Sum(fill => fill.SignedQuantity) != leg.SignedQuantity)
                throw new ArgumentException("Complete opening fill evidence is required.");
            foreach (var fill in fills)
            {
                if (!posted.TryGetValue(fill.ExecutionFillId, out var prior) || prior.JournalId <= 0
                    || prior.SignedAmount != fill.SignedQuantity * fill.Price * leg.CashMultiplier
                    || fill.ExecutionAttemptId != original.ExecutionAttemptId || fill.ComponentId != original.SourceComponentId
                    || fill.ContractId != leg.ContractId || string.IsNullOrWhiteSpace(fill.ExternalExecutionId))
                    throw new ArgumentException("Original posted settlement must match the confirmed opening fill.");
                var amount = fill.SignedQuantity * fill.Price * (change.CashMultiplier - leg.CashMultiplier);
                if (decimal.Round(amount, 2, MidpointRounding.ToEven) != amount)
                    throw new ArgumentException("Correction must resolve exactly to currency cents.");
                var evidence = FinancialCanonicalHash.Compute(new { original.Id, fill, prior, leg.CashMultiplier,
                    CorrectedMultiplier = change.CashMultiplier, reconciliationId });
                var reference = $"MultiplierReconciliation:{reconciliationId:N}:{fill.ExternalExecutionId}";
                items.Add(new() {
                    BookId = configuration.BookId, FundId = configuration.FundId, TransactionKind = LedgerTransactionKind.TradeSettlement,
                    AccountingDate = DateOnly.FromDateTime(fill.FilledAtUtc), ValueDate = DateOnly.FromDateTime(fill.FilledAtUtc),
                    Currency = "USD", Amount = amount, RelatedJournalId = prior.JournalId,
                    Description = $"Verified multiplier correction {leg.CashMultiplier} to {change.CashMultiplier}; {leg.ContractId}",
                    Authority = configuration.Authority,
                    PostingRule = new() { RuleId = rule.RuleId, Version = rule.Version, ContentHash = rule.ContentHash },
                    Source = new() { System = "IFM-MultiplierReconciliation", SourceEntityId = original.Id.Format(),
                        SourceEventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                            $"multiplier-reconciliation|{reconciliationId:N}|{fill.ExecutionFillId:N}"))[..16]),
                        SourceSequence = original.EvidenceRevision + 1,
                        SourceContentHash = evidence, OccurredAtUtc = fill.FilledAtUtc,
                        OrderId = original.Id.OrderId, TradeId = original.Id.TradeId, FillId = reference },
                    MovementEvidence = new() { Status = MovementStatus.Confirmed, SourceReference = reference,
                        ObservedAtUtc = fill.FilledAtUtc, ReceivedAtUtc = atUtc, ValidUntilUtc = atUtc.AddDays(1), ContentHash = evidence }
                });
            }
        }
        return new() { BookId = configuration.BookId, Items = [..items],
            BatchSourceReference = $"MultiplierReconciliation:{reconciliationId:N}", ManifestHash = FinancialCanonicalHash.Compute(items.ToArray()) };
    }
}
