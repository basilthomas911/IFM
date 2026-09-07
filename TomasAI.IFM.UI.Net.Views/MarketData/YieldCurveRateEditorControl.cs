using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>
/// Adapts the observable yield-curve editor state and operations to WinForms controls and dialogs.
/// </summary>
public partial class YieldCurveRateEditorControl
    : DarkTradingView, IControlCommand, IAsyncFormControl
{
    readonly YieldCurveRateEditorViewModel _viewModel;
    readonly MarketDataViewModel _marketDataViewModel;
    Action<bool>? _dataLoaded;
    Action<bool>? _addAction;
    Action<bool>? _changeAction;
    bool _isBinding;

    /// <summary>
    /// Creates a WinForms adapter for the supplied editor and Market Data shell ViewModels.
    /// </summary>
    public YieldCurveRateEditorControl(
        YieldCurveRateEditorViewModel viewModel,
        MarketDataViewModel marketDataViewModel)
    {
        InitializeComponent();
        MarketDataTypography.Apply(this);
        MarketDataInputPalette.Apply(this);
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _marketDataViewModel = marketDataViewModel ?? throw new ArgumentNullException(nameof(marketDataViewModel));
        dtmImportDate.Value = EasternTime.GetNow(TimeProvider.System).Date;
    }

    /// <summary>Gets whether the current snapshot supports change and remove actions.</summary>
    public bool CanChangeRemove => _viewModel.CanChangeRemove;

    /// <summary>Gets whether the editor supports imports.</summary>
    public bool CanImport => _viewModel.CanImport;

    void IControlCommand.Load(IAppRoot appRoot, Action<bool> dataLoaded)
    {
        _dataLoaded = dataLoaded;
        _ = LoadEditorAsync();
    }

    void IControlCommand.Unload()
        => _ = ((IAsyncFormControl)this).CloseAsync();

    /// <summary>Shows the rate dialog and submits a guarded add operation when accepted.</summary>
    public void Add(Action<bool> addAction)
    {
        if (_viewModel.AddOperation.IsRunning)
            return;
        _addAction = addAction;
        using var dialog = new YieldCurveRateEditForm(new YieldCurveRateEditViewModel(_viewModel.AppRoot));
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _viewModel.PrepareAdd(dialog.YieldCurveRate);
            _ = AddPreparedRateAsync();
        }
        else
            addAction(true);
    }

    /// <summary>Shows the rate dialog for the selected row and submits a guarded change operation.</summary>
    public void Change(Action<bool> changeAction)
    {
        if (_viewModel.ChangeOperation.IsRunning)
            return;
        var rate = GetSelectedRate();
        if (rate is null)
            return;
        _changeAction = changeAction;
        using var dialog = new YieldCurveRateEditForm(new YieldCurveRateEditViewModel(_viewModel.AppRoot));
        dialog.SetYieldCurveRate(rate);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _viewModel.PrepareChange(dialog.YieldCurveRate);
            _ = ChangePreparedRateAsync();
        }
        else
            changeAction(true);
    }

    /// <summary>Confirms and submits removal of the selected rate.</summary>
    public void Remove()
    {
        if (_viewModel.RemoveOperation.IsRunning)
            return;
        var rate = GetSelectedRate();
        if (rate is null)
            return;
        if (MessageBox.Show(
                $"Are you sure you want to remove the Yield Curve Rates for: {rate.ValueDate:yyyy-MMM-dd} ?",
                "Remove Yield Curve Rate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _viewModel.PrepareRemove(rate);
        _ = RemovePreparedRateAsync();
    }

    /// <summary>Imports external yield-curve rates through the guarded import operation.</summary>
    public void Import()
    {
        if (_viewModel.ImportOperation.IsRunning)
            return;
        _viewModel.PrepareImport(dtmImportDate.Value.Date);
        _ = ImportPreparedRatesAsync();
    }

    /// <summary>Indicates that the control has no inline edit mode to cancel.</summary>
    public bool Close(Action<bool> changeAction) => true;

    /// <summary>This control does not expose an independent open action.</summary>
    public void Open() => throw new NotImplementedException();

    /// <summary>Stops the editor lifecycle asynchronously.</summary>
    public void Close() => _ = ((IAsyncFormControl)this).CloseAsync();

    async ValueTask IAsyncFormControl.CloseAsync()
        => await _viewModel.StopAsync(CancellationToken.None);

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();

    async Task LoadEditorAsync()
        => await ExecuteOperationAsync(_viewModel.LoadOperation, BindSnapshot);

    async Task ReloadRatesAsync()
        => await ExecuteOperationAsync(_viewModel.LoadRatesOperation, BindRates);

    async Task AddPreparedRateAsync()
        => await ExecuteOperationAsync(_viewModel.AddOperation, () =>
        {
            BindSnapshot();
            _addAction?.Invoke(true);
        });

    async Task ChangePreparedRateAsync()
        => await ExecuteOperationAsync(_viewModel.ChangeOperation, () =>
        {
            BindSnapshot();
            _changeAction?.Invoke(true);
        });

    async Task RemovePreparedRateAsync()
        => await ExecuteOperationAsync(_viewModel.RemoveOperation, BindSnapshot);

    async Task ImportPreparedRatesAsync()
        => await ExecuteOperationAsync(_viewModel.ImportOperation, BindSnapshot);

    async Task ExecuteOperationAsync(IAsyncOperation operation, Action onCompleted)
    {
        SetBusy(true);
        try
        {
            await operation.ExecuteAsync();
            onCompleted();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                text: exception.Message,
                caption: "Yield Curve Rates Editor Error",
                buttons: MessageBoxButtons.OK,
                icon: MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SetBusy(bool isBusy)
    {
        Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;
        _marketDataViewModel.SetEditorBusy(isBusy);
        ddlTimePeriod.Enabled = !isBusy;
        dtmImportDate.Enabled = !isBusy;
        gridYieldCurveRates.Enabled = !isBusy;
    }

    void BindSnapshot()
    {
        _isBinding = true;
        try
        {
            ddlTimePeriod.DataSource = null;
            ddlTimePeriod.DataSource = _viewModel.TimePeriods.ToArray();
            var selectedIndex = Array.IndexOf(
                _viewModel.TimePeriods.ToArray(),
                _viewModel.SelectedTimePeriod);
            ddlTimePeriod.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }
        finally
        {
            _isBinding = false;
        }
        BindRates();
    }

    void BindRates()
    {
        yieldCurveRatesBindingSource.DataSource = _viewModel.YieldCurveRates.ToArray();
        gridYieldCurveRates.DataSource = yieldCurveRatesBindingSource;
        yieldCurveRatesBindingSource.ResetBindings(false);
        gridYieldCurveRates.Update();
        _dataLoaded?.Invoke(_viewModel.YieldCurveRates.Count > 0);
    }

    YieldCurveRateReadModel? GetSelectedRate()
        => gridYieldCurveRates.SelectedRows.Count > 0
            ? _viewModel.GetYieldCurveRate(gridYieldCurveRates.SelectedRows[0].Index)
            : null;

    void ddlTimePeriod_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_isBinding || ddlTimePeriod.SelectedIndex < 0)
            return;
        _viewModel.SelectTimePeriod(
            ddlTimePeriod.SelectedIndex,
            DateOnly.FromDateTime(EasternTime.GetNow(TimeProvider.System)));
        _ = ReloadRatesAsync();
    }
}
