using System.ComponentModel;
using System.Globalization;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>
/// Transitional WinForms adapter for observable status-console state.
/// </summary>
public partial class StatusConsoleView : DarkTradingView
{
    const int MaximumStatusLogItems = 500;
    StatusConsoleViewModel? _viewModel;
    StatusConsoleLogReadModel? _latestStatusLog;

    public StatusConsoleView()
    {
        InitializeComponent();
        DashboardTypography.ApplyFamilyAndSize(this);
        lstStatusConsole.SetDoubleBuffered(true);
    }

    /// <summary>Binds a lifecycle-owned status-console ViewModel and renders its current snapshot.</summary>
    public void LoadViewModel(StatusConsoleViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;

        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        ShowLastError();
    }

    /// <summary>Detaches this view without stopping the shell-owned ViewModel.</summary>
    public void UnloadView()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _viewModel = null;
    }

    /// <summary>Renders the bounded application status-log snapshot.</summary>
    public void RenderStatusConsole(IReadOnlyList<StatusConsoleLogReadModel> logItems)
    {
        ArgumentNullException.ThrowIfNull(logItems);
        lstStatusConsole.BeginUpdate();
        try
        {
            lstStatusConsole.Items.Clear();
            lstStatusConsole.Items.AddRange(logItems
                .Take(MaximumStatusLogItems)
                .Select(CreateStatusLogItem)
                .ToArray());
            _latestStatusLog = logItems.FirstOrDefault();
        }
        finally
        {
            lstStatusConsole.EndUpdate();
        }
    }

    /// <summary>
    /// Reconciles a newest-first snapshot by prepending only rows that arrived since the previous render.
    /// Falls back to a full render if the snapshots no longer overlap.
    /// </summary>
    public void UpdateStatusConsole(IReadOnlyList<StatusConsoleLogReadModel> logItems)
    {
        ArgumentNullException.ThrowIfNull(logItems);
        if (logItems.Count == 0)
        {
            if (lstStatusConsole.Items.Count != 0)
                RenderStatusConsole(logItems);
            return;
        }

        if (_latestStatusLog is null || lstStatusConsole.Items.Count == 0)
        {
            RenderStatusConsole(logItems);
            return;
        }

        var overlapIndex = FindOverlapIndex(logItems, _latestStatusLog);
        if (overlapIndex < 0)
        {
            RenderStatusConsole(logItems);
            return;
        }

        lstStatusConsole.BeginUpdate();
        try
        {
            // Insert oldest-to-newest at index zero so the final visual order remains newest-first.
            for (var index = overlapIndex - 1; index >= 0; index--)
                lstStatusConsole.Items.Insert(0, CreateStatusLogItem(logItems[index]));

            var targetCount = Math.Min(logItems.Count, MaximumStatusLogItems);
            while (lstStatusConsole.Items.Count > targetCount)
                lstStatusConsole.Items.RemoveAt(lstStatusConsole.Items.Count - 1);
            _latestStatusLog = logItems[0];
        }
        finally
        {
            lstStatusConsole.EndUpdate();
        }
    }

    /// <summary>Prepends one newly observed application status-log entry.</summary>
    public void AppendStatusConsole(StatusConsoleLogReadModel logItem)
    {
        if (EqualityComparer<StatusConsoleLogReadModel>.Default.Equals(_latestStatusLog, logItem))
            return;

        lstStatusConsole.BeginUpdate();
        try
        {
            lstStatusConsole.Items.Insert(0, CreateStatusLogItem(logItem));
            while (lstStatusConsole.Items.Count > MaximumStatusLogItems)
                lstStatusConsole.Items.RemoveAt(lstStatusConsole.Items.Count - 1);
            _latestStatusLog = logItem;
        }
        finally
        {
            lstStatusConsole.EndUpdate();
        }
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (_viewModel is null)
                return;

            if (eventArgs.PropertyName == nameof(StatusConsoleViewModel.LastError))
                ShowLastError();
        });

    void ShowLastError()
    {
        if (_viewModel?.LastError is { } error)
            this.ShowErrorMessage(error.Message, error.Caption);
    }

    public void ResizeView(Control parentControl)
    {
        Width = parentControl.Width;
        Height = parentControl.Height;
    }

    static ListViewItem CreateStatusLogItem(StatusConsoleLogReadModel log)
        => new([
            EasternTime.FromUtc(log.StatusDate).ToString("hh:mm:ss.fff tt", CultureInfo.InvariantCulture),
            log.Message]);

    static int FindOverlapIndex(
        IReadOnlyList<StatusConsoleLogReadModel> logItems,
        StatusConsoleLogReadModel latestStatusLog)
    {
        for (var index = 0; index < logItems.Count; index++)
        {
            if (ReferenceEquals(logItems[index], latestStatusLog))
                return index;
        }

        for (var index = 0; index < logItems.Count; index++)
        {
            if (EqualityComparer<StatusConsoleLogReadModel>.Default.Equals(logItems[index], latestStatusLog))
                return index;
        }

        return -1;
    }
}
