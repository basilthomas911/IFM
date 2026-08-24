using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Services.Reference;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public partial class ReferenceForm : Form, IForm<ReferenceForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly Dictionary<string, Func<IAppRoot, Control>> _controlMap;
    ReferenceViewModel? _viewModel;
    IControlCommand? _ctrlCommand;
    bool _closeComplete;

    public ReferenceForm(
        IAppRoot appRoot,
        IReferenceDataService referenceDataService,
        IEconomicCalendarService economicCalendarService)
    {
        _appRoot = appRoot;
        _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
        {
            { "EconomicCalendar", ar => new EconomicCalendarEditorView(
                new EconomicCalendarEditorViewModel(ar, economicCalendarService))},
            { "LookupTypes", ar => new LookupTypeEditorView(
                new LookupTypeEditorViewModel(ar, referenceDataService))}
        };
        _ctrlCommand = null;
        InitializeComponent();
    }

    /// <summary>
    /// load reference view model
    /// </summary>
    /// <param name="viewModel"></param>
    public void LoadViewModel(ReferenceViewModel viewModel)
    {
        if (_viewModel is not null)
            _viewModel.LoadReferenceDataDefinitionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;

        _viewModel = viewModel;
        _viewModel.LoadReferenceDataDefinitionTypesOperation.PropertyChanged += LoadOperation_PropertyChanged;
    }

    /// <summary>
    /// load reference view
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
     async void ReferenceForm_Load(object sender, EventArgs e)
    {
        if (_viewModel is null)
            return;

        try
        {
            await _viewModel.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync();
            BindReferenceDataDefinitionTypes();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Reference Data");
        }
    }

     async void ReferenceForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        if (_viewModel is not null)
            _viewModel.LoadReferenceDataDefinitionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;
        await CloseActiveControlAsync();
        ResetButtons(true);
        _closeComplete = true;
        Close();
    }

     async void ddlReferenceDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateSelectorAccessibility();
        await CloseActiveControlAsync();
        pnlMarketData.Controls.Clear();
        var mktDataDefType = _viewModel?.GetReferenceDataDefinitionType(ddlReferenceDataSelector.SelectedIndex);
        if (mktDataDefType is not null && _controlMap.ContainsKey(mktDataDefType.ShortCode))
        {
            var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
            control.Visible = false;
            pnlMarketData.Controls.Add(control);
            _ctrlCommand = (control as IControlCommand)!;
            _ctrlCommand.Load(_appRoot, enabled => {
                btnChange.Enabled = _ctrlCommand.CanChangeRemove;
                btnRemove.Enabled = _ctrlCommand.CanChangeRemove;
                btnImport.Enabled = _ctrlCommand.CanImport;
            });
            control.Visible = true;
        }
        ResetButtons(true);
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

    void btnAdd_Click(object sender, EventArgs e) => _ctrlCommand?.Add(enabled => this.Post(() => RefreshAddButton(enabled)));

    void btnChange_Click(object sender, EventArgs e ) => _ctrlCommand?.Change(enabled => this.Post(() =>  RefreshChangeButton(enabled)));

    void btnRemove_Click(object sender, EventArgs e) => _ctrlCommand?.Remove();

    void btnClose_Click(object sender, EventArgs e)
    {
        if (_ctrlCommand?.Close(enabled => this.Post(() => ResetButtons(enabled))) ?? false)
            this.Close();
    }

    void btnImport_Click(object sender, EventArgs e) => _ctrlCommand?.Import();

    void RefreshAddButton(bool enabled)
    {
        btnAdd.Text = !enabled ? "Save" : "Add";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void RefreshChangeButton(bool enabled)
    {
        btnChange.Text = !enabled ? "Save" : "Change";
        btnAdd.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void ResetButtons(bool enabled)
    {
        btnAdd.Text = @"&Add";
        btnAdd.Enabled = true;
        btnChange.Text = @"C&hange";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void DisableAllButtons()
    {
        btnAdd.Enabled = false;
        btnChange.Enabled = false;
        btnRemove.Enabled = false;
        btnImport.Enabled = false;
        btnClose.Enabled = false;
    }

    void LoadOperation_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncOperation.IsRunning) && _viewModel is not null)
            this.Post(() => ddlReferenceDataSelector.Enabled = !_viewModel.LoadReferenceDataDefinitionTypesOperation.IsRunning);
    }

    void BindReferenceDataDefinitionTypes()
    {
        ddlReferenceDataSelector.Items.Clear();
        if (_viewModel is null)
            return;

        foreach (var definitionType in _viewModel.ReferenceDataDefinitionTypes)
            ddlReferenceDataSelector.Items.Add(definitionType.Description);
        ddlReferenceDataSelector.AccessibleDescription = string.Join(", ",
            _viewModel.ReferenceDataDefinitionTypes.Select(definition => definition.Description));

        if (ddlReferenceDataSelector.Items.Count > 0)
            ddlReferenceDataSelector.SelectedIndex = 0;
        UpdateSelectorAccessibility();
    }

    void UpdateSelectorAccessibility()
        => ddlReferenceDataSelector.AccessibleName =
            $"Reference data selector; selected={ddlReferenceDataSelector.SelectedItem}; "
            + $"catalog: {ddlReferenceDataSelector.AccessibleDescription}";

    public void Open()
    {
        throw new NotImplementedException();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
}
