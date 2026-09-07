using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

/// <summary>Loads period metrics independently, discarding results from previous Fund/date selections.</summary>
public sealed class FundMetricsViewModel(FundQueryService queries) : ObservableObject, IDisposable
{
    CancellationTokenSource? _load;
    long _generation;
    FundPnlReportReadModel? _report;
    string _message = "Select a Fund to view metrics.";
    public FundPnlReportReadModel? Report { get => _report; private set => SetProperty(ref _report, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public void Clear()
    {
        ++_generation;
        _load?.Cancel(); _load?.Dispose(); _load = null;
        Report = null;
        Message = "Select a Fund to view metrics.";
    }

    public async Task LoadAsync(int fundId, DateOnly from, DateOnly through)
    {
        Clear();
        if (from > through)
        {
            Message = "Metrics: choose a valid From / To date range.";
            return;
        }
        var generation = _generation;
        var load = _load = new CancellationTokenSource();
        Message = $"Loading metrics for Fund {fundId}...";
        try
        {
            var report = await queries.GetFundPnlReportAsync(fundId, from, through, load.Token);
            if (generation != _generation) return;
            Report = report;
            Message = report?.HasHistory == true
                ? $"Fund {fundId} metrics: {from:yyyy-MM-dd} through {through:yyyy-MM-dd}."
                : "Metrics: no recorded history for this period (or report availability is unknown).";
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (generation == _generation) Message = $"Fund metrics unavailable: {ex.Message}";
        }
    }

    public void Dispose() => Clear();
}
