using System.ComponentModel;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>WinForms adapter for the observable end-of-day workflow.</summary>
public partial class TradeEndOfDayForm : Form, IFormControl
{
    readonly EndOfDayProcessViewModel _viewModel;
    bool _closeComplete;
    bool _rendering;
    long _lastErrorSequence;

    public TradeEndOfDayForm(IAppRoot appRoot, TradeEndOfDayParameter eodParam)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentNullException.ThrowIfNull(eodParam);
        _viewModel = new EndOfDayProcessViewModel(appRoot, eodParam);
        InitializeComponent();
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    async void TradeEndOfDayForm_Load(object sender, EventArgs e)
    {
        txtFundId.Text = $"{_viewModel.FundId}";
        txtOrderId.Text = $"{_viewModel.OrderId}";
        txtTradeId.Text = $"{_viewModel.TradeId}";
        _rendering = true;
        dtpValueDate.Value = _viewModel.ValueDate.ToDateTime(TimeOnly.MinValue);
        _rendering = false;
        try
        {
            await _viewModel.StartListener();
            await LoadDataAsync();
        }
        catch (Exception exception)
        {
            HandleOperationException(exception);
        }
    }

    async void TradeEndOfDayForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        await _viewModel.DisposeAsync();
        _closeComplete = true;
        Close();
    }

    async void btnRun_Click(object sender, EventArgs e)
    {
        _viewModel.SetReference(txtReference.Text);
        try
        {
            await _viewModel.RunEndOfDayProcess();
            if (_viewModel.IsCompleted)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception exception)
        {
            HandleOperationException(exception);
        }
    }

    void btnClose_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    async void dtpValueDate_ValueChanged(object sender, EventArgs e)
    {
        if (_rendering)
            return;
        await ObserveLoadAsync();
    }

    void IFormControl.Resize(Control parentControl)
        => throw new NotImplementedException();

    async void btnLoad_Click(object sender, EventArgs e)
        => await ObserveLoadAsync();

    async Task ObserveLoadAsync()
    {
        try
        {
            await LoadDataAsync();
        }
        catch (Exception exception)
        {
            HandleOperationException(exception);
        }
    }

    async Task LoadDataAsync()
    {
        _viewModel.SetValueDate(DateOnly.FromDateTime(dtpValueDate.Value));
        await _viewModel.LoadData();
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            if (eventArgs.PropertyName == nameof(EndOfDayProcessViewModel.LastError))
                ShowLatestError();
            RenderState();
        });

    void RenderState()
    {
        var snapshot = _viewModel.Snapshot;
        txtOpenPrice.Text = snapshot is null ? string.Empty : $"{snapshot.OpenPrice:###0.00}";
        txtHighPrice.Text = snapshot is null ? string.Empty : $"{snapshot.HighPrice:###0.00}";
        txtLowPrice.Text = snapshot is null ? string.Empty : $"{snapshot.LowPrice:###0.00}";
        txtClosePrice.Text = snapshot is null ? string.Empty : $"{snapshot.ClosePrice:###0.00}";
        txtVolume.Text = snapshot is null ? string.Empty : $"{snapshot.Volume:#,###,##0}";
        txtTradePnl.Text = snapshot is null ? string.Empty : $"{snapshot.TradePnl:C}";
        btnLoad.Enabled = !_viewModel.IsBusy;
        btnRun.Enabled = _viewModel.CanRun;
        dtpValueDate.Enabled = !_viewModel.IsBusy;
        txtReference.Enabled = !_viewModel.IsBusy;
        Cursor.Current = _viewModel.IsBusy ? Cursors.WaitCursor : Cursors.Default;
    }

    void ShowLatestError()
    {
        var error = _viewModel.LastError;
        if (error is null || error.Sequence <= _lastErrorSequence)
            return;
        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    void HandleOperationException(Exception exception)
    {
        if (_viewModel.LastError?.Message == exception.Message)
            return;
        this.ShowErrorMessage(exception.Message, "End Of Day Process Error");
    }

    public void Open() => throw new NotImplementedException();
}
