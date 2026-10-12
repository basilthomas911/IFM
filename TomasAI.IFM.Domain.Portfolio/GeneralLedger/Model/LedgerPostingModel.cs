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
        ValidatePostingRequest(request, rule, previousUnrealized, allowRawAdjustment);
        var lines = new List<PlannedLedgerLine>();
        var kind = request.TransactionKind;
        Require(kind is not (LedgerTransactionKind.Adjustment or LedgerTransactionKind.OpeningBalance) || allowRawAdjustment,
            FinancialReasons.AuthorityDenied, "Adjustments and opening balances require privileged posting permission.");
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
            AddAdjustmentLines(request, rule, lines);
        }
        else if (kind == LedgerTransactionKind.Reversal)
        {
            AddReversalLines(request, reversalLines, remainingReversibleAmount, lines);
        }
        else
        {
            var amount = kind == LedgerTransactionKind.Valuation ? request.Amount - previousUnrealized : request.Amount;
            if (kind == LedgerTransactionKind.RealizedPnl && previousUnrealized != 0)
            {
                Require(rule.ValuationAsset is not null && rule.UnrealizedPnl is not null,
                    FinancialReasons.InvalidAccount, "Realization must clear previously recognized valuation.");
                AddPair(request, lines, rule.UnrealizedPnl!, rule.ValuationAsset!, previousUnrealized, request.FundId, request.FundId);
            }
            var otherFund = kind == LedgerTransactionKind.FundTransfer ? request.CounterpartyFundId : request.FundId;
            Require(otherFund is > 0 && (kind != LedgerTransactionKind.FundTransfer || otherFund != request.FundId),
                FinancialReasons.InvalidContract, "A transfer requires a different owned destination Fund.");
            AddPair(request, lines, rule.Debit, rule.Credit, amount, otherFund, request.FundId);
        }
        if (lines.Count > 0) ValidateBalanced(lines);
        else Require(kind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl,
            FinancialReasons.UnbalancedJournal, "Only a confirmed unchanged valuation/realization can have no journal effect.");
        return new(lines, kind == LedgerTransactionKind.WithdrawalSettled ? -request.Amount : 0,
            kind == LedgerTransactionKind.FundTransfer,
            kind is LedgerTransactionKind.TradeSettlement or LedgerTransactionKind.Commission or LedgerTransactionKind.RealizedPnl or LedgerTransactionKind.Valuation,
            FinancialCanonicalHash.Compute(lines));

    }

    /// <summary>Validates the explicit accounting identity, rule fingerprint, exact-cent values, adjustment permission and confirmed movement evidence.</summary>
    static void ValidatePostingRequest(LedgerPostingRequest request, LedgerPostingRule rule, decimal previousUnrealized, bool allowRawAdjustment)
    {
        Require(string.Equals(request.Currency,"USD",StringComparison.Ordinal), FinancialReasons.UnsupportedCurrency, "Only explicit USD accounting is qualified.");
        Require(request.BookId > 0 && request.FundId > 0 && Enum.IsDefined(request.TransactionKind)
            && request.TransactionKind != LedgerTransactionKind.Undefined, FinancialReasons.InvalidContract, "Invalid posting identity/kind.");
        Require(request.PostingRule.RuleId == rule.RuleId && request.PostingRule.Version == rule.Version &&
            string.Equals(request.PostingRule.ContentHash,rule.ContentHash,StringComparison.Ordinal) && request.TransactionKind == rule.Kind,
            FinancialReasons.InvalidAccount, "Posting rule identity/version/hash or kind does not match.");
        Money(request.Amount); Money(previousUnrealized);
        Require(request.Lines.Length == 0 || allowRawAdjustment && request.TransactionKind is
            LedgerTransactionKind.Adjustment or LedgerTransactionKind.OpeningBalance,
            FinancialReasons.AuthorityDenied, "Raw lines require the explicit adjustment/import permission.");
        if (rule.RequiresConfirmedMovement)
            Require(request.MovementEvidence.Status == MovementStatus.Confirmed &&
                !string.IsNullOrWhiteSpace(request.MovementEvidence.SourceReference),
                FinancialReasons.InvalidContract, "A confirmed financial movement is required.");
    }

    /// <summary>Validates explicit adjustment lines and resolves the exact account versions bound by the supplied rule.</summary>
    static void AddAdjustmentLines(LedgerPostingRequest request, LedgerPostingRule rule, List<PlannedLedgerLine> lines)
    {
            foreach (var line in request.Lines)
            {
                Require(string.Equals(line.Currency,"USD",StringComparison.Ordinal) && line.Amount > 0 && line.AccountId > 0 &&
                    line.PostingSide is PostingSide.Debit or PostingSide.Credit,
                    FinancialReasons.InvalidAccount, "Invalid adjustment account/side/amount.");
                Money(line.Amount);
                // Exact versions are resolved from the configured rule, never inferred as latest.
                var account = (line.AccountId == rule.Debit.AccountId) switch
{
    true => rule.Debit,
    false => line.AccountId == rule.Credit.AccountId ? rule.Credit : throw new FinancialOperationException(FinancialReasons.InvalidAccount, "Adjustment account is not bound by the rule.")};
                lines.Add(new(line.Ordinal, account, line.FundId,
                    line.PostingSide == PostingSide.Debit ? line.Amount : 0,
                    line.PostingSide == PostingSide.Credit ? line.Amount : 0, line.OrderId, line.TradeId, line.SourceLineReference));
            }
    }

    /// <summary>Reverses each original debit and credit proportionally: original line amount * requested reversal / original debit total; exact cents are required.</summary>
    static void AddReversalLines(LedgerPostingRequest request, IReadOnlyList<PlannedLedgerLine>? reversalLines, decimal remainingReversibleAmount, List<PlannedLedgerLine> lines)
    {
            if (reversalLines is null)
                throw new FinancialOperationException(FinancialReasons.ExcessReversal, "Reversal exceeds the remaining amount or original journal is missing.");
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

    /// <summary>Creates a balanced pair for the signed amount, swapping accounts and funds when the amount is negative.</summary>
    static void AddPair(LedgerPostingRequest request, List<PlannedLedgerLine> lines, LedgerAccountBinding debit, LedgerAccountBinding credit, decimal amount, int? debitFund, int? creditFund)
        {
            if (amount == 0) return;
            Money(amount);
            if (amount < 0) { (debit, credit) = (credit, debit); (debitFund, creditFund) = (creditFund, debitFund); amount = -amount; }
            lines.Add(new(lines.Count + 1, debit, debitFund, amount, 0, request.Source.OrderId, request.Source.TradeId, "debit"));
            lines.Add(new(lines.Count + 1, credit, creditFund, 0, amount, request.Source.OrderId, request.Source.TradeId, "credit"));
        }

    public static void ValidateBalanced(IReadOnlyList<PlannedLedgerLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
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

    static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, int code, string message)
    { if (!condition) throw new FinancialOperationException(code, message); }
}
