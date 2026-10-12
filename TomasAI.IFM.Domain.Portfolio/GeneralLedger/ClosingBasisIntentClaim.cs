using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>
/// Atomically reserves per-position opening basis with its immutable accounting intent.
/// Unposted claims remain reserved and block later attempts until reconciled; ambiguous failures never free basis.
/// </summary>
internal sealed class ClosingBasisIntentClaim(IPostgresEventTransaction transactions, IDbContextFactory databases)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "execution is the public operation parameter captured by its transaction callback; exceptions correctly identify that supplied execution.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "MA0051", Justification = "This single transaction owns the per-position advisory lock through validated basis allocation, immutable intent and claim writes; keeping the ordered boundary together makes its financial fence reviewable.")]
    public async Task<PostFundTransactionsCommand> ClaimAsync(OrderExecutionDefinition execution, Guid operationId,
        string evidenceHash, FinancialPostingConfiguration configuration,
        Func<IReadOnlyDictionary<Guid, decimal>?, PostFundTransactionsCommand> build,
        CancellationToken token)
    {
        if (execution.TargetPositionId is not { IsValid: true } position)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CLOSE_POSITION_REQUIRED");
        if (position.Trade.PortfolioId != execution.TradeOrderId.PortfolioId ||
            position.Trade.FundId != execution.TradeOrderId.FundId)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.POSITION_OWNER_MISMATCH");
        var trade = await (databases.TradeDb.GetEstablishedTradeAsync(position.Trade, token)).ConfigureAwait(false)
            ?? throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.OPENING_TRADE_NOT_FOUND");
        if (trade.Id != position.Trade || StrategyPositionId.Create(trade.Id, trade.StrategyKind) != position)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.POSITION_IDENTITY_MISMATCH");
        if (trade.OriginalFills.Length == 0 || trade.OriginalFills.Any(x => x.ExecutionFillId == Guid.Empty) ||
            trade.OriginalFills.Select(x => x.ExecutionFillId).Distinct().Count() != trade.OriginalFills.Length ||
            trade.OriginalFills.Any(x => string.IsNullOrWhiteSpace(x.ExternalExecutionId)) ||
            trade.OriginalFills.Select(x => x.ExternalExecutionId).Distinct(StringComparer.Ordinal).Count() != trade.OriginalFills.Length ||
            execution.Fills.Select(x => x.ExternalExecutionId).Distinct(StringComparer.Ordinal).Count() != execution.Fills.Length ||
            execution.Fills.Select(x => x.ExecutionFillId).Distinct().Count() != execution.Fills.Length)
            throw new ArgumentException("Opening and closing fills must have unique evidence identities.",nameof(execution));
        var portfolio = execution.TradeOrderId.PortfolioId;
        var key = position.Format();
        return await (transactions.ExecuteAsync(async (db, ct) =>
        {
            await (db.ScalarAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",
                [$"broker-closing-basis:{portfolio}:{key}"], ct)).ConfigureAwait(false);
            var existing = await (BrokerAccountingIntentStore.ReadAsync(db, portfolio, operationId, evidenceHash, ct)).ConfigureAwait(false);
            if (existing is not null) return existing;
            var previous = await (db.QueryAsync("""
                SELECT c.opening_leg_id,c.opening_hash,c.closed_quantity,c.allocated_signed_basis,
                       c.execution_attempt_id,r.operation_id IS NOT NULL
                FROM portfolio_financial.broker_closing_basis_claim c
                LEFT JOIN portfolio_financial.financial_operation_receipt r
                  ON r.portfolio_id=c.portfolio_id AND r.operation_id=c.operation_id
                WHERE c.portfolio_id=$1 AND c.position_key=$2;
                """, [portfolio, key], r => new Claim(r.GetGuid(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetGuid(4), r.GetBoolean(5)), ct)).ConfigureAwait(false);
            if (previous.Any(x => !x.Posted))
                throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.PRIOR_CLOSE_PENDING_RECONCILIATION");
            if (trade.ClosingFills.Any(x => previous.All(p => p.Attempt != x.ExecutionAttemptId) &&
                x.ExecutionAttemptId != execution.ExecutionAttemptId))
                throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.LEGACY_CLOSE_RECONCILIATION_REQUIRED");
            var basis = new Dictionary<Guid, decimal>();
            var claims = new List<Claim>();
            var usedOpeningLegs = new HashSet<Guid>();
            foreach (var filledLeg in execution.Fills.GroupBy(x => x.TradeLegId))
            {
                var closeLeg = execution.Order.Components.SelectMany(x => x.Legs).Single(x => x.TradeLegId == filledLeg.Key);
                var closeComponent = execution.Order.Components.Single(x => x.Legs.Any(l => l.TradeLegId == filledLeg.Key));
                // A unique contract match supports close orders that assign new leg IDs; ambiguous mappings fail closed.
                var candidates = trade.Legs.Where(x => string.Equals(x.ContractId,closeLeg.ContractId,StringComparison.Ordinal)).ToArray();
                if (candidates.Length != 1 || !usedOpeningLegs.Add(candidates[0].TradeLegId))
                    throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.OPENING_LEG_AMBIGUOUS");
                var leg = candidates[0];
                var fills = trade.OriginalFills.Where(x => x.TradeLegId == leg.TradeLegId)
                    .OrderBy(x => x.ExecutionFillId).ToArray();
                if (fills.Length == 0 || leg.CashMultiplier != closeLeg.CashMultiplier ||
                    fills.Any(x => !string.Equals(x.ContractId,leg.ContractId,StringComparison.Ordinal) || x.ExecutionAttemptId != trade.ExecutionAttemptId ||
                        x.ComponentId != trade.SourceComponentId ||
                        Math.Sign(x.SignedQuantity) != Math.Sign(leg.SignedQuantity)) ||
                    filledLeg.Any(x => x.ExecutionAttemptId != execution.ExecutionAttemptId ||
                        x.ComponentId != closeComponent.ComponentId ||
                        !string.Equals(x.ContractId,closeLeg.ContractId,StringComparison.Ordinal) || Math.Sign(x.SignedQuantity) != Math.Sign(closeLeg.SignedQuantity)))
                    throw new ArgumentException("Opening or closing evidence does not match the position leg.",nameof(execution));
                if (Math.Abs(fills.Sum(x => (decimal)x.SignedQuantity)) > Math.Abs(leg.SignedQuantity))
                    throw new ArgumentException("Opening fills exceed the approved opening leg quantity.", nameof(execution));
                var quantity = filledLeg.Sum(x => (decimal)x.SignedQuantity);
                if (Math.Abs(quantity) > Math.Abs(closeLeg.SignedQuantity))
                    throw new ArgumentException("Aggregate closing fills exceed approved leg quantity.", nameof(execution));
                var openingHash = FinancialCanonicalHash.Compute(new { leg, fills });
                var consumed = previous.Where(x => x.Leg == leg.TradeLegId).ToArray();
                if (consumed.Any(x => !string.Equals(x.Hash,openingHash,StringComparison.Ordinal)))
                    throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.OPENING_EVIDENCE_CHANGED");
                var allocation = WeightedAverageClosingBasis.Allocate(
                    fills.Select(x => new OpeningBasisLot(x.SignedQuantity, x.Price, leg.CashMultiplier)).ToArray(),
                    quantity, consumed.Sum(x => x.Quantity), consumed.Sum(x => x.Basis));
                basis.Add(closeLeg.TradeLegId, allocation.AllocatedSignedBasis);
                claims.Add(new(leg.TradeLegId, openingHash, Math.Abs(quantity), allocation.AllocatedSignedBasis, execution.ExecutionAttemptId, false));
            }
            var proposed = build(basis);
            var valuation = await (db.QueryAsync("""
                SELECT amount,source_sequence FROM portfolio_financial.ledger_valuation
                WHERE book_id=$1 AND fund_id=$2 AND position_key=$3;
                """, [proposed.Body.BookId, position.Trade.FundId, $"{position.Trade.OrderId}:{position.Trade.TradeId}"],
                r => (Amount: r.GetDecimal(0), Sequence: r.GetInt64(1)), ct)).ConfigureAwait(false);
            var previousUnrealized = (valuation.Count == 0 ? 0m : valuation[0].Amount);
            // A zero net valuation can hide offsetting leg gains/losses; it still requires a uniform close ratio.
            var remainingUnrealized = valuation.Count == 0 ? 0 : ProportionalCloseValuation.Remaining(previousUnrealized,
                trade.Legs.Select(leg => new PositionLegCloseQuantity(
                    trade.OriginalFills.Where(x => x.TradeLegId == leg.TradeLegId).Sum(x => Math.Abs((decimal)x.SignedQuantity))
                        - previous.Where(x => x.Leg == leg.TradeLegId).Sum(x => x.Quantity),
                    claims.Where(x => x.Leg == leg.TradeLegId).Sum(x => x.Quantity))).ToArray());
            var sequence = checked((valuation.Count == 0 ? 0L : valuation[0].Sequence) + 1);
            var items = proposed.Body.Items.Select(item => item with
            {
                Source = item.Source with
                {
                    OrderId = position.Trade.OrderId,
                    TradeId = position.Trade.TradeId,
                    SourceSequence = sequence
                }
            }).ToArray();
            if (remainingUnrealized != 0)
            {
                var rules = configuration.Rules.Where(x => x.Kind == LedgerTransactionKind.Valuation).ToArray();
                var realized = items.Single(x => x.TransactionKind == LedgerTransactionKind.RealizedPnl);
                var realizationRule = configuration.Rules.Single(x => x.Kind == LedgerTransactionKind.RealizedPnl);
                if (rules.Length != 1 || rules[0].RequiresConfirmedMovement ||
                    rules[0].Debit != realizationRule.ValuationAsset || rules[0].Credit != realizationRule.UnrealizedPnl)
                    throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.VALUATION_RESTORE_RULE_MISMATCH");
                var restoreId = new Guid(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"restore-close-valuation|{operationId:N}"))[..16]);
                var restore = realized with
                {
                    TransactionKind = LedgerTransactionKind.Valuation,
                    Amount = remainingUnrealized,
                    Description = "Retain unrealized P&L for the remaining position after a proportional close",
                    PostingRule = new() { RuleId = rules[0].RuleId, Version = rules[0].Version, ContentHash = rules[0].ContentHash },
                    Source = realized.Source with
                    {
                        SourceEventId = restoreId,
                        SourceSequence = checked(sequence + 1),
                        FillId = $"{execution.ExecutionAttemptId:N}:remaining-valuation",
                        SourceContentHash = FinancialCanonicalHash.Compute(new { operationId, previousUnrealized, remainingUnrealized })
                    }
                };
                // Both reversal and restoration commit within the same fenced ledger transaction.
                items = [.. items, restore];
            }
            proposed = proposed with { Body = proposed.Body with { Items = items, ManifestHash = FinancialCanonicalHash.Compute(items) } };
            proposed = proposed with { InputSha256 = FinancialCanonicalHash.Request(proposed) };
            var command = await (BrokerAccountingIntentStore.ClaimEnlistedAsync(db, proposed, evidenceHash, ct)).ConfigureAwait(false);
            foreach (var claim in claims)
                await (db.ExecuteAsync("""
                    INSERT INTO portfolio_financial.broker_closing_basis_claim
                    (portfolio_id,position_key,opening_leg_id,operation_id,execution_attempt_id,opening_hash,closed_quantity,allocated_signed_basis)
                    VALUES($1,$2,$3,$4,$5,$6,$7,$8);
                    """, [portfolio, key, claim.Leg, operationId, claim.Attempt, claim.Hash, claim.Quantity, claim.Basis], ct)).ConfigureAwait(false);
            return command;
        }, token)).ConfigureAwait(false);
    }

    private sealed record Claim(Guid Leg, string Hash, decimal Quantity, decimal Basis, Guid Attempt, bool Posted);
}
