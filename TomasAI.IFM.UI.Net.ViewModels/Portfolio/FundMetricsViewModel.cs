using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Summarizes canonical Portfolio ledger activity for a Fund and period.</summary>
public sealed record PortfolioFundMetricsReport(bool HasHistory, decimal WinRate, decimal AverageProfit,
    decimal LossRate, decimal AverageLoss, decimal WinLossRatio, decimal ActualSharpeRatio, decimal PnlAmount,
    decimal PnlPercent, decimal TradeCommission, decimal? MaximumDrawdownPercent, decimal? MaximumDrawdownAmount);

/// <summary>Loads canonical Portfolio-ledger metrics while fencing stale selections.</summary>
public sealed class FundMetricsViewModel : ObservableObject, IDisposable
{
    readonly IPortfolioFinancialApi _api;
    CancellationTokenSource? _load;
    long _generation;
    PortfolioFundMetricsReport? _report;
    string _message = "Select a Fund to view metrics.";

    /// <summary>Initializes a Portfolio Fund metrics view model.</summary>
    /// <param name="api">The canonical Portfolio financial query API.</param>
    public FundMetricsViewModel(IPortfolioFinancialApi api) => _api = api ?? throw new ArgumentNullException(nameof(api));

    /// <summary>Gets the current canonical metrics report.</summary>
    public PortfolioFundMetricsReport? Report { get => _report; private set => SetProperty(ref _report, value); }
    /// <summary>Gets the current load or availability message.</summary>
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    /// <summary>Clears the report and cancels outstanding work.</summary>
    public void Clear()
    {
        ++_generation; _load?.Cancel(); _load?.Dispose(); _load = null;
        Report = null; Message = "Select a Fund to view metrics.";
    }

    /// <summary>Loads metrics from committed Portfolio ledger transactions.</summary>
    /// <param name="portfolioId">The owning Portfolio identifier.</param>
    /// <param name="fundId">The selected Fund identifier.</param>
    /// <param name="from">The inclusive accounting start date.</param>
    /// <param name="through">The inclusive accounting end date.</param>
    public async Task LoadAsync(int portfolioId, int fundId, DateOnly from, DateOnly through)
    {
        Clear();
        if (portfolioId <= 0 || fundId <= 0 || from > through) { Message = "Metrics: choose a valid Portfolio, Fund, and date range."; return; }
        var generation = _generation; var load = _load = new CancellationTokenSource();
        Message = $"Loading metrics for Fund {fundId}...";
        try
        {
            var scope = new FinancialReadScope { PortfolioId = portfolioId, FundId = fundId,
                Access = new(Environment.UserName, ["LedgerRead"], [portfolioId]) };
            var rows = new List<FinancialTransactionRow>(); FinancialPageCursor? cursor = null;
            do
            {
                var result = await _api.GetFundTransactionsPageAsync(scope, new(200, cursor), load.Token).ConfigureAwait(false);
                if (!result.Success || result.Value?.Value is not { } page)
                    throw new InvalidOperationException(result.ErrorMessage ?? "Portfolio financial transactions are unavailable.");
                rows.AddRange(page.Items); cursor = page.NextCursor;
            } while (cursor is not null);
            if (generation != _generation) return;
            Report = Calculate(rows.Where(row => row.Transaction.AccountingDate >= from && row.Transaction.AccountingDate <= through)
                .OrderBy(row => row.FinancialRevision).ThenBy(row => row.Ordinal).ToArray());
            Message = Report.HasHistory ? $"Fund {fundId} Portfolio-ledger metrics: {from:yyyy-MM-dd} through {through:yyyy-MM-dd}."
                : "Metrics: no committed Portfolio ledger history for this period.";
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error) { if (generation == _generation) Message = $"Fund metrics unavailable: {error.Message}"; }
    }

    /// <summary>Cancels outstanding work and releases owned resources.</summary>
    public void Dispose() => Clear();

    static PortfolioFundMetricsReport Calculate(IReadOnlyList<FinancialTransactionRow> rows)
    {
        if (rows.Count == 0) return new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null);
        var realized = rows.Where(row => row.Transaction.TransactionKind == LedgerTransactionKind.RealizedPnl).Select(row => row.Transaction.Amount).ToArray();
        var wins = realized.Where(value => value > 0).ToArray(); var losses = realized.Where(value => value < 0).ToArray();
        var pnl = realized.Sum(); var count = realized.Length;
        var commission = rows.Where(row => row.Transaction.TransactionKind == LedgerTransactionKind.Commission).Sum(row => Math.Abs(row.Transaction.Amount));
        var capital = rows.Where(row => row.Transaction.TransactionKind is LedgerTransactionKind.OpeningBalance or LedgerTransactionKind.DepositConfirmed).Sum(row => Math.Abs(row.Transaction.Amount));
        var averageProfit = wins.Length == 0 ? 0 : wins.Average(); var averageLoss = losses.Length == 0 ? 0 : Math.Abs(losses.Average());
        var returns = capital == 0 ? [] : realized.Select(value => value / capital).ToArray(); var mean = returns.Length == 0 ? 0 : returns.Average();
        var variance = returns.Length < 2 ? 0 : returns.Sum(value => (value - mean) * (value - mean)) / (returns.Length - 1);
        decimal balance = 0, peak = 0, drawdown = 0;
        foreach (var row in rows) { balance += SignedAmount(row.Transaction); peak = Math.Max(peak, balance); drawdown = Math.Max(drawdown, peak - balance); }
        return new(true, count == 0 ? 0 : (decimal)wins.Length / count, averageProfit,
            count == 0 ? 0 : (decimal)losses.Length / count, averageLoss, averageLoss == 0 ? 0 : averageProfit / averageLoss,
            variance <= 0 ? 0 : mean / (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252d), pnl,
            capital == 0 ? 0 : pnl / capital, commission, peak <= 0 ? null : drawdown / peak, drawdown);
    }

    static decimal SignedAmount(LedgerPostingRequest value) => value.TransactionKind switch
    {
        LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.Commission => -Math.Abs(value.Amount),
        LedgerTransactionKind.RealizedPnl => value.Amount,
        LedgerTransactionKind.OpeningBalance or LedgerTransactionKind.DepositConfirmed => Math.Abs(value.Amount), _ => 0m
    };
}
