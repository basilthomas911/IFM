using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Services.Reference;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

public partial class MarketDataForm 
    : DarkTradingForm, IForm<MarketDataForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly IStatusConsoleEventProducer _statusConsoleLog;
    readonly Dictionary<string, Func<IAppRoot, Control>> _controlMap;
    MarketDataViewModel? _viewModel;
    IControlCommand? _ctrlCommand;
    bool _closeComplete;

    public MarketDataForm(
        IAppRoot appRoot,
        IStatusConsoleEventProducer statusConsoleLog,
        IReferenceDataService referenceDataService)
    {
        _appRoot = appRoot;
        _statusConsoleLog = statusConsoleLog;
        _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
        {
            { "FuturesOptionContract", ar => new FuturesOptionContractEditorControl(
                new FuturesOptionContractEditorViewModel(ar, referenceDataService), _viewModel!)},
            { "FuturesContract", ar => new FuturesContractEditorControl(
                new FuturesContractEditorViewModel(ar, referenceDataService),
                EnableAvailableButtons)},
            { "YieldCurveRates", ar => new YieldCurveRateEditorControl( new YieldCurveRateEditorViewModel(ar), _viewModel!)}
        };
        InitializeComponent();
        MarketDataTypography.Apply(this);
        MarketDataInputPalette.Apply(this);
        lblMarketDataSelector.AutoSize = false;
        lblMarketDataSelector.TextAlign = ContentAlignment.MiddleLeft;
        lblMarketDataSelector.UseCompatibleTextRendering = false;
        AlignSelectionLabel();
        ddlMarketDataSelector.SizeChanged += (_, _) => AlignSelectionLabel();
        ddlMarketDataSelector.LocationChanged += (_, _) => AlignSelectionLabel();
    }

    void AlignSelectionLabel()
    {
        lblMarketDataSelector.Top = ddlMarketDataSelector.Top;
        lblMarketDataSelector.Height = ddlMarketDataSelector.Height;
        lblMarketDataSelector.Width = lblMarketDataSelector.PreferredWidth;
    }

    public void LoadViewModel(MarketDataViewModel viewModel)
    {
        UnsubscribeFromViewModel();
        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.LoadDefinitionTypesOperation.PropertyChanged += LoadOperation_PropertyChanged;
    }

    private async void MarketDataForm_Load(object sender, EventArgs e)
    {
        if (_viewModel is null)
            return;
        try
        {
            await _viewModel.LoadDefinitionTypesOperation.ExecuteAsync();
            BindDefinitionTypes();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Market Data Definitions Error");
        }
    }

    private async void MarketDataForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        await CloseActiveControlAsync();
        UnsubscribeFromViewModel();
        ResetButtons(true);
        _closeComplete = true;
        Close();
    }

    private async void ddlMarketDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateSelectorAccessibility();
        ResetButtons(true);
        DisableAllButtons();
        await CloseActiveControlAsync();
        pnlMarketData.Controls.Clear();
        var mktDataDefType = _viewModel?.GetDefinitionType(ddlMarketDataSelector.SelectedIndex);
        if (mktDataDefType != null && _controlMap.ContainsKey(mktDataDefType.ShortCode))
        {
            var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
            control.Visible = false;
            pnlMarketData.Controls.Add(control);
            _ctrlCommand = (control as IControlCommand)!;
            _ctrlCommand.Load(_appRoot, enabled => this.Post(EnableAvailableButtons));
            control.Visible = true;
        }
    }

    async ValueTask CloseActiveControlAsync()
    {
        if (pnlMarketData.Controls.Count == 0)
            return;
        if (pnlMarketData.Controls[0] is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else
            _ctrlCommand?.Unload();
    }

    private void btnAdd_Click(object sender, EventArgs e) => _ctrlCommand?.Add(enabled => this.Post(() => RefreshAddButton(enabled)));

    private void btnChange_Click(object sender, EventArgs e ) => _ctrlCommand?.Change(enabled => this.Post(() => RefreshChangeButton(enabled)));

    private void btnRemove_Click(object sender, EventArgs e) => _ctrlCommand?.Remove();

    private void btnClose_Click(object sender, EventArgs e)
    {
        if (_ctrlCommand?.Close(enabled => this.Post(() => ResetButtons(enabled))) ?? false)
            this.Close();
    }

    private void btnImport_Click(object sender, EventArgs e) => _ctrlCommand?.Import();

    void RefreshAddButton(bool enabled)
    {
        btnAdd.Text = !enabled ? "Save" : "Add";
        btnClose.Text = !enabled ? "Cancel" : "Close";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        ddlMarketDataSelector.Enabled = enabled;
    }

    void RefreshChangeButton(bool enabled)
    {
        btnChange.Text = !enabled ? "Save" : "Change";
        btnClose.Text = !enabled ? "Cancel" : "Close";
        btnAdd.Enabled = enabled;
        btnRemove.Enabled = enabled;
        ddlMarketDataSelector.Enabled = enabled;
    }

    void ResetButtons(bool enabled)
    {
        btnAdd.Text = @"&Add";
        btnAdd.Enabled = true;
        btnChange.Text = @"C&hange";
        btnClose.Text = "Close";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        ddlMarketDataSelector.Enabled = enabled;
    }

    void DisableAllButtons()
    {
         btnAdd.Enabled = false;
        btnChange.Enabled = false;
        btnRemove.Enabled = false;
        btnImport.Enabled = false;
        btnClose.Enabled = false;
    }

    void BindDefinitionTypes()
    {
        ddlMarketDataSelector.Items.Clear();
        if (_viewModel is null)
            return;
        foreach (var definition in _viewModel.DefinitionTypes)
            ddlMarketDataSelector.Items.Add(definition.Description);
        ddlMarketDataSelector.AccessibleDescription = string.Join(", ",
            _viewModel.DefinitionTypes.Select(definition => definition.Description));
        if (ddlMarketDataSelector.Items.Count > 0)
            ddlMarketDataSelector.SelectedIndex = 0;
        UpdateSelectorAccessibility();
    }

    void UpdateSelectorAccessibility()
        => ddlMarketDataSelector.AccessibleName =
            $"Market data selector; selected={ddlMarketDataSelector.SelectedItem}; "
            + $"catalog: {ddlMarketDataSelector.AccessibleDescription}";

    void LoadOperation_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncOperation.IsRunning) && _viewModel is not null)
            this.Post(() => ddlMarketDataSelector.Enabled = !_viewModel.LoadDefinitionTypesOperation.IsRunning);
    }

    void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MarketDataViewModel.IsEditorBusy) || _viewModel is null)
            return;
        this.Post(() =>
        {
            if (_viewModel.IsEditorBusy)
                DisableAllButtons();
            else
                EnableAvailableButtons();
            ddlMarketDataSelector.Enabled = !_viewModel.IsEditorBusy;
        });
    }

    void EnableAvailableButtons()
    {
        btnAdd.Enabled = true;
        btnClose.Enabled = true;
        btnChange.Enabled = _ctrlCommand?.CanChangeRemove ?? false;
        btnRemove.Enabled = _ctrlCommand?.CanChangeRemove ?? false;
        btnImport.Enabled = _ctrlCommand?.CanImport ?? false;
    }

    void UnsubscribeFromViewModel()
    {
        if (_viewModel is null)
            return;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.LoadDefinitionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;
    }

    public void Open()
    {
        throw new NotImplementedException();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
}
