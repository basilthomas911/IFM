using System.ComponentModel;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
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
        pnlTradeLayout.Height = 25;
        txtTradeStatus.Enabled = false;
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
        RenderObservableState();
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

            switch (eventArgs.PropertyName)
            {
                case nameof(StatusConsoleViewModel.TradeSignals):
                    RefreshTradeConsole(_viewModel.TradeSignals);
                    break;
                case nameof(StatusConsoleViewModel.TradeStatus):
                    RefreshTradeStatus(_viewModel.TradeStatus);
                    break;
                case nameof(StatusConsoleViewModel.MDIForwardLossRatios):
                    RefreshMDIForwardLossRatios(_viewModel.MDIForwardLossRatios);
                    break;
                case nameof(StatusConsoleViewModel.LastError):
                    if (_viewModel.LastError is { } error)
                        this.ShowErrorMessage(error.Message, error.Caption);
                    break;
            }
        });

    void RenderObservableState()
    {
        if (_viewModel is null)
            return;

        RefreshTradeConsole(_viewModel.TradeSignals);
        RefreshTradeStatus(_viewModel.TradeStatus);
        RefreshMDIForwardLossRatios(_viewModel.MDIForwardLossRatios);
        if (_viewModel.LastError is { } error)
            this.ShowErrorMessage(error.Message, error.Caption);
    }

    void RefreshTradeConsole(IReadOnlyList<FuturesItiSignalV2ReadModel> futuresItiSignals)
    {
        lstTradeStatus.BeginUpdate();
        lstTradeStatus.Items.Clear();
        lstTradeStatus.Items.AddRange(futuresItiSignals.Select(CreateTradeStatusItem).ToArray());
        lstTradeStatus.EndUpdate();
    }

    void RefreshTradeStatus(FuturesTradeStatusUIViewModel tradeStatus)
    {
        txtTradeStatus.Text = tradeStatus.TradeStatus;
        txtTradeStatus.ForeColor = tradeStatus.TradeStatusForeColor.ToColor();
        txtTradeStatus.BackColor = tradeStatus.TradeStatusBackColor.ToColor();
        txtTradeStatus.Enabled = true;
    }

    void RefreshMDIForwardLossRatios(IReadOnlyList<MDIForwardLossRatioUIViewModel> ratios)
    {
        lstMDIFwdLossRatio.BeginUpdate();
        lstMDIFwdLossRatio.Items.Clear();
        lstMDIFwdLossRatio.Items.AddRange(ratios.Select(CreateForwardLossRatioItem).ToArray());
        lstMDIFwdLossRatio.EndUpdate();
    }

    public void ResizeView(Control parentControl)
    {
        Width = parentControl.Width;
        Height = parentControl.Height;
    }

    static ListViewItem CreateStatusLogItem(StatusConsoleLogReadModel log)
        => new([$"{log.StatusDate:T}", log.Message]);

    static ListViewItem CreateTradeStatusItem(FuturesItiSignalV2ReadModel signal)
        => new([
            $"{signal.IntrinsicTime:T}",
            $"{signal.ContractId} - {signal.IntrinsicTimeTrend} @ {signal.IntrinsicPrice:F2} := {signal.TargetDelta:F4}"]);

    static ListViewItem CreateForwardLossRatioItem(MDIForwardLossRatioUIViewModel ratio)
        => new([ratio.MDI, ratio.TrendDirection, ratio.TradeType, ratio.ForwardLossRatio]);

    async void tabConsoles_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (tabConsoles.SelectedIndex != 0 || _viewModel is null)
            return;

        try
        {
            await _viewModel.LoadTradeStatusOperation.ExecuteAsync();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Trade Status Error");
        }
    }
}
