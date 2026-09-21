using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.UI.Net.Views.Trade;

public sealed record VolatilityContextLoadRequest(
    VolatilitySeriesDefinition Definition,
    VolatilityHistoryPageRequest HistoryRequest,
    LatestVolatilityResult Current,
    OptionIvMetricSnapshot? EntryDecisionSnapshot,
    int MaximumRows = 200,
    int MaximumPages = 3);

/// <summary>Reusable read-only Stage 4 context/history view with bounded asynchronous loading.</summary>
public sealed class VolatilityContextHistoryControl : UserControl
{
    readonly Label _mode = ValueLabel("volatilityHistoryModeLabel");
    readonly Label _series = ValueLabel("volatilitySeriesLabel");
    readonly Label _current = ValueLabel("volatilityCurrentLabel");
    readonly Label _quality = ValueLabel("volatilityQualityLabel");
    readonly Label _entry = ValueLabel("volatilityEntrySnapshotLabel");
    readonly DataGridView _history = new()
    {
        Name = "volatilityHistoryGrid", Dock = DockStyle.Fill, ReadOnly = true,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.Black, ForeColor = Color.White, GridColor = Color.FromArgb(70, 70, 70),
        EnableHeadersVisualStyles = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    public VolatilityContextHistoryControl()
    {
        Name = "volatilityContextHistory";
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        var summary = new FlowLayoutPanel
        {
            Name = "volatilitySummary", Dock = DockStyle.Top, Height = 94, AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(32, 32, 32)
        };
        summary.Controls.AddRange([_mode, _series, _current, _quality, _entry]);
        _history.Columns.Add("ValueDate", "Value Date");
        _history.Columns.Add("AsOf", "As-of / Available");
        _history.Columns.Add("IV", "Comparable IV");
        _history.Columns.Add("Rank", "IV Rank");
        _history.Columns.Add("Percentile", "IV Percentile");
        _history.Columns.Add("Coverage", "Coverage / Quality");
        Controls.Add(_history);
        Controls.Add(summary);
        ShowUnavailable();
    }

    public string ModeText => _mode.Text;
    public string CurrentText => _current.Text;
    public string EntrySnapshotText => _entry.Text;
    public int HistoryRowCount => _history.Rows.Count;

    public async Task LoadAsync(IOptionVolatilityQueryApi query, VolatilityContextLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumRows is < 1 or > 500 || request.MaximumPages is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(request), "Volatility history bounds are invalid.");
        if (request.HistoryRequest.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request), "Volatility history page size must be 1-100.");

        var uiContext = SynchronizationContext.Current;
        var collected = new List<OptionIvMetricRevision>(Math.Min(request.MaximumRows, 100));
        var pageRequest = request.HistoryRequest;
        for (var pageNumber = 0; pageNumber < request.MaximumPages && collected.Count < request.MaximumRows; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await query.GetMetricHistoryAsync(pageRequest, cancellationToken).ConfigureAwait(false);
            collected.AddRange(page.Items.Take(request.MaximumRows - collected.Count));
            if (page.PagingState is null || page.PagingState.Length == 0) break;
            pageRequest = pageRequest with { PagingState = page.PagingState };
        }
        var rows = collected.ToArray();

        await OnUiAsync(uiContext, () => Bind(request, rows), cancellationToken).ConfigureAwait(false);
    }

    public void Bind(VolatilityContextLoadRequest request, IReadOnlyList<OptionIvMetricRevision> history)
    {
        var definition = request.Definition;
        var mode = request.HistoryRequest.Mode == VolatilityHistoricalMode.AsKnown ? "As-known" : "Restated";
        _mode.Text = $"Historical mode: {mode}";
        _series.Text = $"Series {definition.Identity.SeriesId} / methodology {definition.Identity.MethodologyVersion}; " +
            $"tenor {definition.Tenor.TargetCalendarDays} calendar days; lookback {definition.Metrics.HistoricalLookbackSessions} sessions; " +
            $"policy {definition.Metrics.PolicyVersion}";
        var current = request.Current.Metric?.Snapshot;
        _current.Text = current is null
            ? $"Current comparable IV: Unavailable; Rank: Unavailable; Percentile: Unavailable; freshness {request.Current.FreshnessStatus}"
            : $"Current comparable IV: {Value(current.CurrentImpliedVolatility, "P4")}; Rank: {Value(current.IvRank, "0.####")}; " +
                $"Percentile: {Value(current.IvPercentile, "0.####")}; as-of {current.ObservedAtUtc:O}; freshness {request.Current.FreshnessStatus}";
        _quality.Text = current is null ? "Coverage/quality: Unavailable" :
            $"Coverage {current.ValidHistoricalObservationCount}/{current.ExpectedHistoricalObservationCount} ({current.CoverageRatio:P2}); " +
            $"Rank {current.RankStatus}; Percentile {current.PercentileStatus}; available {current.AvailableAtUtc:O}";
        _entry.Text = request.EntryDecisionSnapshot is not { } entry ? "Exact entry-decision snapshot: Unavailable" :
            $"Exact entry-decision snapshot: {entry.SnapshotId}; digest {entry.SnapshotDigest}; series {entry.Series.SeriesId}/{entry.Series.MethodologyVersion}; " +
            $"policy {entry.MetricPolicyVersion}; observed {entry.ObservedAtUtc:O}; calculated {entry.CalculatedAtUtc:O}; recorded {entry.RecordedAtUtc:O}; available {entry.AvailableAtUtc:O}; " +
            $"Rank {entry.RankStatus}; Percentile {entry.PercentileStatus}";
        _history.Rows.Clear();
        foreach (var revision in history.OrderByDescending(x => x.Snapshot.ExchangeValueDate).ThenByDescending(x => x.Revision))
        {
            var item = revision.Snapshot;
            _history.Rows.Add(item.ExchangeValueDate, $"{item.ObservedAtUtc:O} / {item.AvailableAtUtc:O}",
                Value(item.CurrentImpliedVolatility, "P4"), Value(item.IvRank, "0.####"), Value(item.IvPercentile, "0.####"),
                $"{item.ValidHistoricalObservationCount}/{item.ExpectedHistoricalObservationCount} ({item.CoverageRatio:P2}); {item.RankStatus}/{item.PercentileStatus}; r{revision.Revision}");
        }
    }

    void ShowUnavailable()
    {
        _mode.Text = "Historical mode: not loaded (choose As-known or Restated)";
        _series.Text = "Series/tenor/lookback: Unavailable";
        _current.Text = "Current comparable IV: Unavailable; Rank: Unavailable; Percentile: Unavailable";
        _quality.Text = "Coverage/quality: Unavailable";
        _entry.Text = "Exact entry-decision snapshot: Unavailable";
    }

    static string Value(decimal? value, string format) => value?.ToString(format) ?? "Unavailable";
    static Label ValueLabel(string name) => new() { Name = name, AutoSize = true, ForeColor = Color.White, Padding = new Padding(6, 2, 6, 0) };

    static Task OnUiAsync(SynchronizationContext? context, Action action, CancellationToken token)
    {
        if (context is null) { action(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(_ =>
        {
            if (token.IsCancellationRequested) { completion.TrySetCanceled(token); return; }
            try { action(); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); }
        }, null);
        return completion.Task;
    }
}
