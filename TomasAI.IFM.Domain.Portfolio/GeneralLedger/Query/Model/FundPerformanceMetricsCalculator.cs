using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;

/// <summary>Preserves the existing UI ledger formulas as explicitly identified server metrics.</summary>
public static class FundPerformanceMetricsCalculator
{
    public const string Basis = "LegacyLedgerPostings/v1: realized postings / period opening balances and deposits; "
        + "sample Sharpe annualized by sqrt(252) per posting (not daily returns); "
        + "drawdown includes deposits, withdrawals, commissions and realized P&L; no cash-flow adjustment.";

    public static FundPerformanceMetrics Calculate(IReadOnlyList<FinancialTransactionRow> rows, GetFundPerformanceMetricsRequest request)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        if (request.From == default || request.Through < request.From)
            throw new ArgumentException("An inclusive accounting date range is required.", nameof(request));
        var ordered = rows.Where(r => r.Transaction.AccountingDate >= request.From && r.Transaction.AccountingDate <= request.Through)
            .OrderBy(r => r.FinancialRevision).ThenBy(r => r.Ordinal).ToArray();
        var report = new FundPerformanceMetrics { From = request.From, Through = request.Through, CalculationBasis = Basis };
        if (ordered.Length == 0) return report;
        var realized = ordered.Where(r => r.Transaction.TransactionKind == LedgerTransactionKind.RealizedPnl).Select(r => r.Transaction.Amount).ToArray();
        var wins = realized.Where(v => v > 0).ToArray(); var losses = realized.Where(v => v < 0).ToArray();
        var pnl = realized.Sum(); var count = realized.Length;
        var commission = ordered.Where(r => r.Transaction.TransactionKind == LedgerTransactionKind.Commission).Sum(r => Math.Abs(r.Transaction.Amount));
        var capital = ordered.Where(r => r.Transaction.TransactionKind is LedgerTransactionKind.OpeningBalance or LedgerTransactionKind.DepositConfirmed).Sum(r => Math.Abs(r.Transaction.Amount));
        var averageProfit = wins.Length == 0 ? 0 : wins.Average(); var averageLoss = losses.Length == 0 ? 0 : Math.Abs(losses.Average());
        var returns = capital == 0 ? [] : realized.Select(v => v / capital).ToArray(); var mean = returns.Length == 0 ? 0 : returns.Average();
        var variance = returns.Length < 2 ? 0 : returns.Sum(v => (v - mean) * (v - mean)) / (returns.Length - 1);
        decimal balance = 0, peak = 0, drawdown = 0;
        foreach (var row in ordered)
        {
            balance += row.Transaction.TransactionKind switch {
                LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.Commission => -Math.Abs(row.Transaction.Amount),
                LedgerTransactionKind.RealizedPnl => row.Transaction.Amount,
                LedgerTransactionKind.OpeningBalance or LedgerTransactionKind.DepositConfirmed => Math.Abs(row.Transaction.Amount), _ => 0m };
            peak = Math.Max(peak, balance); drawdown = Math.Max(drawdown, peak - balance);
        }
        return report with { HasHistory = true, WinRate = count == 0 ? 0 : (decimal)wins.Length / count,
            AverageProfit = averageProfit, LossRate = count == 0 ? 0 : (decimal)losses.Length / count, AverageLoss = averageLoss,
            WinLossRatio = averageLoss == 0 ? 0 : averageProfit / averageLoss,
            ActualSharpeRatio = variance <= 0 ? 0 : mean / (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252d),
            PnlAmount = pnl, PnlPercent = capital == 0 ? 0 : pnl / capital, TradeCommission = commission,
            MaximumDrawdownPercent = peak <= 0 ? null : drawdown / peak, MaximumDrawdownAmount = drawdown };
    }
}
