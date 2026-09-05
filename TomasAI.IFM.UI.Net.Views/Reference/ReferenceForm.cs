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
    bool _closeInProgress;
    int _selectionGeneration;
    const string TradeStrategyFamiliesLabel = "trade strategy families";
    static readonly Font ReferenceFont = new("Microsoft Sans Serif", 10F, FontStyle.Regular, GraphicsUnit.Point);

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
        ApplyReferenceFont(this);
    }

    // Include all nested editors and controls added later, not only the designer shell.
    static void ApplyReferenceFont(Control control)
    {
        control.Font = ReferenceFont;
        control.ControlAdded -= ReferenceControlAdded;
        control.ControlAdded += ReferenceControlAdded;
        foreach (Control child in control.Controls) ApplyReferenceFont(child);
    }

    static void ReferenceControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null) ApplyReferenceFont(e.Control);
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
        if (_closeInProgress)
            return;
        _closeInProgress = true;
        ++_selectionGeneration;
        if (_viewModel is not null)
            _viewModel.LoadReferenceDataDefinitionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;
        await CloseActiveControlAsync();
        ResetButtons(true);
        _closeComplete = true;
        // ShowDialog resets the close result when this event is canceled. Let the
        // current event finish before requesting the final close on the UI queue.
        if (!IsDisposed && IsHandleCreated)
            BeginInvoke((Action)Close);
    }

     async void ddlReferenceDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        var generation = ++_selectionGeneration;
        UpdateSelectorAccessibility();
        await CloseActiveControlAsync();
        if (generation != _selectionGeneration || IsDisposed) return;
        pnlMarketData.Controls.Clear();
        if (string.Equals(ddlReferenceDataSelector.SelectedItem?.ToString(), TradeStrategyFamiliesLabel, StringComparison.Ordinal))
        {
            var catalog = new TradeStrategyFamilyReferenceView(_appRoot.Services.ReferenceQueries, _appRoot.Services.ReferenceCommands,
                _appRoot.Services.MarketDataQueries) { Dock = DockStyle.Fill };
            _ctrlCommand = catalog;
            catalog.StateChanged += (_, _) => { if (ReferenceEquals(_ctrlCommand, catalog)) RefreshFamilyButtons(catalog); };
            pnlMarketData.Controls.Add(catalog);
            RefreshFamilyButtons(catalog);
            await catalog.LoadAsync();
            return;
        }
        var mktDataDefType = _viewModel?.GetReferenceDataDefinitionType(ddlReferenceDataSelector.SelectedIndex);
        if (mktDataDefType is not null && _controlMap.ContainsKey(mktDataDefType.ShortCode))
        {
            var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
            control.Visible = false;
            pnlMarketData.Controls.Add(control);
            _ctrlCommand = (control as IControlCommand)!;
            var command = _ctrlCommand;
            command.Load(_appRoot, enabled => {
                if (!ReferenceEquals(_ctrlCommand, command) || IsDisposed) return;
                btnChange.Enabled = command.CanChangeRemove;
                btnRemove.Enabled = command.CanChangeRemove;
                btnImport.Enabled = command.CanImport;
            });
            control.Visible = true;
        }
        ResetButtons(true);
    }

    async ValueTask CloseActiveControlAsync()
    {
        if (pnlMarketData.Controls.Count == 0)
            return;
        var control = pnlMarketData.Controls[0];
        var command = _ctrlCommand;
        _ctrlCommand = null;
        if (control is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else
            command?.Unload();
        pnlMarketData.Controls.Remove(control);
        control.Dispose();
    }

    void btnAdd_Click(object sender, EventArgs e) => _ctrlCommand?.Add(enabled => this.Post(() => RefreshAddButton(enabled)));

    void btnChange_Click(object sender, EventArgs e ) => _ctrlCommand?.Change(enabled => this.Post(() =>  RefreshChangeButton(enabled)));

    void btnRemove_Click(object sender, EventArgs e) => _ctrlCommand?.Remove();

    void btnClose_Click(object sender, EventArgs e)
    {
        var action = btnClose.Text.Replace("&", string.Empty).Trim();
        if (string.Equals(action, "Close", StringComparison.OrdinalIgnoreCase))
        {
            Close();
            return;
        }
        if (string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            // Cancel ends the active edit; it must not close the containing dialog.
            if (_ctrlCommand is null) ResetButtons(true);
            else _ctrlCommand.Close(enabled => this.Post(() => ResetButtons(enabled)));
        }
    }

    void btnImport_Click(object sender, EventArgs e) => _ctrlCommand?.Import();

    void RefreshAddButton(bool enabled)
    {
        if (_ctrlCommand is TradeStrategyFamilyReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
        btnAdd.Text = !enabled ? "Save" : "Add";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void RefreshChangeButton(bool enabled)
    {
        if (_ctrlCommand is TradeStrategyFamilyReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
        btnChange.Text = !enabled ? "Save" : "Change";
        btnAdd.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void ResetButtons(bool enabled)
    {
        if (_ctrlCommand is TradeStrategyFamilyReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
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
            ddlReferenceDataSelector.Items.Add(definitionType.ShortCode == "EconomicCalendar" ? "economic calendar definitions" : definitionType.Description);
        ddlReferenceDataSelector.Items.Add(TradeStrategyFamiliesLabel);
        ddlReferenceDataSelector.AccessibleDescription = string.Join(", ",
            ddlReferenceDataSelector.Items.Cast<object>().Select(item => item.ToString()));

        if (ddlReferenceDataSelector.Items.Count > 0)
            ddlReferenceDataSelector.SelectedIndex = 0;
        UpdateSelectorAccessibility();
    }

    void UpdateSelectorAccessibility()
        => ddlReferenceDataSelector.AccessibleName =
            $"Reference data selector; selected={ddlReferenceDataSelector.SelectedItem}; "
            + $"catalog: {ddlReferenceDataSelector.AccessibleDescription}";

    void RefreshFamilyButtons(TradeStrategyFamilyReferenceView catalog)
    {
        btnAdd.Text = catalog.IsEditing && !catalog.IsChanging ? "Save" : "&Add";
        btnAdd.Enabled = catalog.IsEditing ? !catalog.IsChanging && catalog.CanSave : catalog.CanAdd;
        btnChange.Text = catalog.IsChanging ? "Save" : "C&hange";
        btnChange.Enabled = catalog.IsChanging ? catalog.CanSave : catalog.CanChangeRemove;
        btnRemove.Enabled = catalog.CanChangeRemove;
        btnImport.Enabled = false;
        btnClose.Text = catalog.IsEditing ? "Cancel" : "Close";
        btnClose.Enabled = !catalog.IsSaving;
        ddlReferenceDataSelector.Enabled = !catalog.IsEditing && !catalog.IsSaving;
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
