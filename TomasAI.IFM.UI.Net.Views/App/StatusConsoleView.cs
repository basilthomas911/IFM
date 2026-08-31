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
public partial class StatusConsoleView : UserControl
{
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
        lstStatusConsole.BeginUpdate();
        lstStatusConsole.Items.Clear();
        lstStatusConsole.Items.AddRange(logItems.Select(CreateStatusLogItem).ToArray());
        _latestStatusLog = logItems.FirstOrDefault();
        lstStatusConsole.EndUpdate();
    }

    /// <summary>Prepends one newly observed application status-log entry.</summary>
    public void AppendStatusConsole(StatusConsoleLogReadModel logItem)
    {
        if (EqualityComparer<StatusConsoleLogReadModel>.Default.Equals(_latestStatusLog, logItem))
            return;

        lstStatusConsole.BeginUpdate();
        lstStatusConsole.Items.Insert(0, CreateStatusLogItem(logItem));
        while (lstStatusConsole.Items.Count > 500)
            lstStatusConsole.Items.RemoveAt(lstStatusConsole.Items.Count - 1);
        _latestStatusLog = logItem;
        lstStatusConsole.EndUpdate();
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
}
