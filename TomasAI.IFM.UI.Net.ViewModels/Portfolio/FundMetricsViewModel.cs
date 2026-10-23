using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Loads canonical Portfolio-ledger metrics while fencing stale selections.</summary>
public sealed class FundMetricsViewModel : ObservableObject, IDisposable
{
    readonly IPortfolioFinancialApi _api;
    CancellationTokenSource? _load;
    long _generation;
    FundPerformanceMetrics? _report;
    string _message = "Select a Fund to view metrics.";

    /// <summary>Initializes a Portfolio Fund metrics view model.</summary>
    /// <param name="api">The canonical Portfolio financial query API.</param>
    public FundMetricsViewModel(IPortfolioFinancialApi api) => _api = api ?? throw new ArgumentNullException(nameof(api));

    /// <summary>Gets the current canonical metrics report.</summary>
    public FundPerformanceMetrics? Report { get => _report; private set => SetProperty(ref _report, value); }
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
            var scope = new FinancialReadScope
            {
                PortfolioId = portfolioId,
                FundId = fundId,
                Access = new(Environment.UserName, ["LedgerRead"], [portfolioId])
            };
            var result = await _api.GetFundPerformanceMetricsAsync(scope, new(from, through), load.Token).ConfigureAwait(false);
            if (!result.Success || result.Value is not { Status: FinancialReadStatus.Found, Value: { } report })
                throw new InvalidOperationException(result.ErrorMessage ?? "Portfolio financial metrics are unavailable.");
            if (generation != _generation) return;
            Report = report;
            Message = Report.HasHistory ? $"Fund {fundId} Portfolio-ledger metrics: {from:yyyy-MM-dd} through {through:yyyy-MM-dd}. {Report.CalculationBasis}"
                : "Metrics: no committed Portfolio ledger history for this period.";
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error) { if (generation == _generation) Message = $"Fund metrics unavailable: {error.Message}"; }
    }

    /// <summary>Cancels outstanding work and releases owned resources.</summary>
    public void Dispose() => Clear();

}
