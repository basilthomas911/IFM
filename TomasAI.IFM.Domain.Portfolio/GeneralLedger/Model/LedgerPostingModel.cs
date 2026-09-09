using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Pure double-entry calculations; storage validates current authority/period/source under the Portfolio fence.</summary>
public static class LedgerPostingModel
{
    public static LedgerPostingPlan Calculate(LedgerPostingRequest request, LedgerPostingRule rule,
        decimal previousUnrealized = 0, IReadOnlyList<PlannedLedgerLine>? reversalLines = null,
        decimal remainingReversibleAmount = 0, bool allowRawAdjustment = false)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(rule);
        Require(request.Currency == "USD", FinancialReasons.UnsupportedCurrency, "Only explicit USD accounting is qualified.");
        Require(request.BookId > 0 && request.FundId > 0 && Enum.IsDefined(request.TransactionKind)
            && request.TransactionKind != LedgerTransactionKind.Undefined, FinancialReasons.InvalidContract, "Invalid posting identity/kind.");
        Require(request.PostingRule.RuleId == rule.RuleId && request.PostingRule.Version == rule.Version &&
            request.PostingRule.ContentHash == rule.ContentHash && request.TransactionKind == rule.Kind,
            FinancialReasons.InvalidAccount, "Posting rule identity/version/hash or kind does not match.");
        Money(request.Amount); Money(previousUnrealized);
        Require(request.Lines.Length == 0 || allowRawAdjustment && request.TransactionKind is
            LedgerTransactionKind.Adjustment or LedgerTransactionKind.OpeningBalance,
            FinancialReasons.AuthorityDenied, "Raw lines require the explicit adjustment/import permission.");
        if (rule.RequiresConfirmedMovement)
            Require(request.MovementEvidence.Status == MovementStatus.Confirmed &&
                !string.IsNullOrWhiteSpace(request.MovementEvidence.SourceReference),
                FinancialReasons.InvalidContract, "A confirmed financial movement is required.");
        var lines = new List<PlannedLedgerLine>();
        var kind = request.TransactionKind;
        Require(kind is not (LedgerTransactionKind.Adjustment or LedgerTransactionKind.OpeningBalance) || allowRawAdjustment,
            FinancialReasons.AuthorityDenied,"Adjustments and opening balances require privileged posting permission.");
        var magnitudeRequired = kind is LedgerTransactionKind.DepositConfirmed or LedgerTransactionKind.WithdrawalRequested
            or LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled or LedgerTransactionKind.FundTransfer;
        Require(!magnitudeRequired || request.Amount > 0, FinancialReasons.InvalidContract, "Business amount must be positive.");
        if (kind is LedgerTransactionKind.WithdrawalRequested or LedgerTransactionKind.WithdrawalCancelled)
        {
            Require(kind != LedgerTransactionKind.WithdrawalCancelled || request.RelatedObligationId is not null,
                FinancialReasons.InvalidContract, "Cancellation must name its original withdrawal obligation.");
            return new([], kind == LedgerTransactionKind.WithdrawalRequested ? request.Amount : -request.Amount,
                kind == LedgerTransactionKind.WithdrawalRequested, false);
        }
        if (kind == LedgerTransactionKind.WithdrawalSettled)
            Require(request.RelatedObligationId is not null, FinancialReasons.InvalidContract, "Settlement must name its original withdrawal obligation.");

        if (request.Lines.Length > 0)
        {
            foreach (var line in request.Lines)
            {
                Require(line.Currency == "USD" && line.Amount > 0 && line.AccountId > 0 &&
                    line.PostingSide is PostingSide.Debit or PostingSide.Credit,
                    FinancialReasons.InvalidAccount, "Invalid adjustment account/side/amount.");
                Money(line.Amount);
                // Exact versions are resolved from the configured rule, never inferred as latest.
                var account = line.AccountId == rule.Debit.AccountId ? rule.Debit :
                    line.AccountId == rule.Credit.AccountId ? rule.Credit :
                    throw new FinancialOperationException(FinancialReasons.InvalidAccount, "Adjustment account is not bound by the rule.");
                lines.Add(new(line.Ordinal, account, line.FundId,
                    line.PostingSide == PostingSide.Debit ? line.Amount : 0,
                    line.PostingSide == PostingSide.Credit ? line.Amount : 0, line.OrderId, line.TradeId, line.SourceLineReference));
            }
        }
        else if (kind == LedgerTransactionKind.Reversal)
        {
            Require(request.RelatedJournalId is > 0 && reversalLines is { Count: >= 2 } &&
                request.Amount > 0 && request.Amount <= remainingReversibleAmount,
                FinancialReasons.ExcessReversal, "Reversal exceeds the remaining amount or original journal is missing.");
            var originalMagnitude = reversalLines!.Sum(x => x.Debit);
            Require(originalMagnitude > 0, FinancialReasons.UnbalancedJournal, "Original journal has no amount.");
            foreach (var line in reversalLines)
            {
                var debit = line.Credit * request.Amount / originalMagnitude;
                var credit = line.Debit * request.Amount / originalMagnitude;
                Money(debit); Money(credit); // No silent rounding of partial corrections.
                lines.Add(line with { Debit = debit, Credit = credit });
            }
        }
        else
        {
            var amount = kind == LedgerTransactionKind.Valuation ? request.Amount - previousUnrealized : request.Amount;
            if (kind == LedgerTransactionKind.RealizedPnl && previousUnrealized != 0)
            {
                Require(rule.ValuationAsset is not null && rule.UnrealizedPnl is not null,
                    FinancialReasons.InvalidAccount, "Realization must clear previously recognized valuation.");
                Pair(rule.UnrealizedPnl!, rule.ValuationAsset!, previousUnrealized, request.FundId, request.FundId);
            }
            var otherFund = kind == LedgerTransactionKind.FundTransfer ? request.CounterpartyFundId : request.FundId;
            Require(otherFund is > 0 && (kind != LedgerTransactionKind.FundTransfer || otherFund != request.FundId),
                FinancialReasons.InvalidContract, "A transfer requires a different owned destination Fund.");
            Pair(rule.Debit, rule.Credit, amount, otherFund, request.FundId);
        }
        if(lines.Count>0) ValidateBalanced(lines);
        else Require(kind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl,
            FinancialReasons.UnbalancedJournal,"Only a confirmed unchanged valuation/realization can have no journal effect.");
        return new(lines, kind == LedgerTransactionKind.WithdrawalSettled ? -request.Amount : 0,
            kind == LedgerTransactionKind.FundTransfer,
            kind is LedgerTransactionKind.TradeSettlement or LedgerTransactionKind.Commission or LedgerTransactionKind.RealizedPnl or LedgerTransactionKind.Valuation,
            FinancialCanonicalHash.Compute(lines));

        void Pair(LedgerAccountBinding debit, LedgerAccountBinding credit, decimal amount, int? debitFund, int? creditFund)
        {
            if (amount == 0) return;
            Money(amount);
            if (amount < 0) { (debit, credit) = (credit, debit); (debitFund, creditFund) = (creditFund, debitFund); amount = -amount; }
            lines.Add(new(lines.Count + 1, debit, debitFund, amount, 0, request.Source.OrderId, request.Source.TradeId, "debit"));
            lines.Add(new(lines.Count + 1, credit, creditFund, 0, amount, request.Source.OrderId, request.Source.TradeId, "credit"));
        }
    }

    public static void ValidateBalanced(IReadOnlyList<PlannedLedgerLine> lines)
    {
        Require(lines.Count is >= 2 and <= 256 && lines.Select(x => x.Ordinal).Distinct().Count() == lines.Count,
            FinancialReasons.UnbalancedJournal, "A journal requires 2–256 uniquely numbered lines.");
        foreach (var line in lines)
        {
            Money(line.Debit); Money(line.Credit);
            Require(line.Ordinal > 0 && line.Account.AccountId > 0 && line.Account.Version > 0 &&
                (line.Debit > 0 && line.Credit == 0 || line.Credit > 0 && line.Debit == 0),
                FinancialReasons.UnbalancedJournal, "Invalid journal line.");
        }
        Require(lines.Sum(x => x.Debit) == lines.Sum(x => x.Credit), FinancialReasons.UnbalancedJournal, "Debits must equal credits.");
    }

    public static void Money(decimal value) => Require(decimal.Round(value, 2) == value && value > -100000000000000000000000000m && value < 100000000000000000000000000m,
        FinancialReasons.InvalidContract, "USD amount requires exact cents within numeric(28,2); explicit rounding policy is required otherwise.");

    static void Require(bool condition, int code, string message)
    { if (!condition) throw new FinancialOperationException(code, message); }
}
