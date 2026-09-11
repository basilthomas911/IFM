using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Services.Reference;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public partial class ReferenceForm : DarkTradingForm, IForm<ReferenceForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi? _parameterSets;
    readonly Dictionary<string, Func<IAppRoot, Control>> _controlMap;
    ReferenceViewModel? _viewModel;
    IControlCommand? _ctrlCommand;
    bool _closeComplete;
    bool _closeInProgress;
    int _selectionGeneration;
    object? _activeReferenceSelection;
    bool _restoringReferenceSelection;
    const string TradeStrategyFamiliesLabel = "trade strategy families";

    public ReferenceForm(
        IAppRoot appRoot,
        IReferenceDataService referenceDataService,
        TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi? parameterSets = null)
    {
        _appRoot = appRoot;
        _parameterSets = parameterSets;
        _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
        {
            { "LookupTypes", ar => new LookupTypeEditorView(
                new LookupTypeEditorViewModel(ar, referenceDataService))}
        };
        _ctrlCommand = null;
        InitializeComponent();
        ApplyReferenceFont(this);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // Composite child windows too: form buffering alone does not cover
            // the native lists and text boxes replaced when the selector changes.
            parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }

    static void ApplyReferenceFont(Control control) => DarkTradingTypography.Apply(control);

    /// <summary>
    /// load reference view model
    /// </summary>
    /// <param name="viewModel"></param>
    public void LoadViewModel(ReferenceViewModel viewModel)
    {
        _viewModel = viewModel;
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

        // These editors are owned by the UI and must remain available even when
        // the remote lookup-type catalogue is slow or unavailable.
        BindReferenceDataDefinitionTypes(selectDefault: false);
        try
        {
            await _viewModel.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync();
            BindReferenceDataDefinitionTypes(selectDefault: true);
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
        if (pnlMarketData.Controls.OfType<ParameterSets.ParameterSetsReferenceView>().FirstOrDefault() is { } activeParameterEditor && !activeParameterEditor.CanLeave()) return;
        if (_closeInProgress)
            return;
        _closeInProgress = true;
        ++_selectionGeneration;
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
        if (_restoringReferenceSelection) return;
        if (pnlMarketData.Controls.OfType<ParameterSets.ParameterSetsReferenceView>().FirstOrDefault() is { } activeParameterEditor && !activeParameterEditor.CanLeave())
        {
            _restoringReferenceSelection = true;
            ddlReferenceDataSelector.SelectedItem = _activeReferenceSelection;
            _restoringReferenceSelection = false;
            return;
        }
        _activeReferenceSelection = ddlReferenceDataSelector.SelectedItem;
        var generation = ++_selectionGeneration;
        UpdateSelectorAccessibility();
        await CloseActiveControlAsync();
        if (generation != _selectionGeneration || IsDisposed) return;
        pnlMarketData.Controls.Clear();
        if (string.Equals(ddlReferenceDataSelector.SelectedItem?.ToString(), TradeStrategyFamiliesLabel, StringComparison.Ordinal))
        {
            var catalog = new StrategyCatalogReferenceView(_appRoot.Services.ReferenceQueries, _appRoot.Services.ReferenceCommands) { Dock = DockStyle.Fill };
            _ctrlCommand = catalog;
            catalog.StateChanged += (_, _) => { if (ReferenceEquals(_ctrlCommand, catalog)) RefreshFamilyButtons(catalog); };
            pnlMarketData.Controls.Add(catalog);
            RefreshFamilyButtons(catalog);
            await catalog.LoadAsync();
            return;
        }
        if (string.Equals(ddlReferenceDataSelector.SelectedItem?.ToString(), "parameter sets", StringComparison.Ordinal))
        {
            if (_parameterSets is null) { this.ShowErrorMessage("Parameter Sets service is unavailable.", "Reference Data"); return; }
            var parameterEditor = new ParameterSets.ParameterSetsReferenceView(_parameterSets) { Dock = DockStyle.Fill };
            _ctrlCommand = parameterEditor;
            parameterEditor.StateChanged += (_, _) => { if (ReferenceEquals(_ctrlCommand, parameterEditor)) RefreshParameterButtons(parameterEditor); };
            pnlMarketData.Controls.Add(parameterEditor);
            ((IControlCommand)parameterEditor).Load(_appRoot, _ => this.Post(() => RefreshParameterButtons(parameterEditor)));
            RefreshParameterButtons(parameterEditor);
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
        // Dispose also removes the child. Removing a live, docked SplitContainer
        // first can trigger a repaint against its closing window handle.
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
        if (_ctrlCommand is StrategyCatalogReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
        if (_ctrlCommand is ParameterSets.ParameterSetsReferenceView parameters) { RefreshParameterButtons(parameters); return; }
        btnAdd.Text = !enabled ? "Save" : "Add";
        btnChange.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void RefreshChangeButton(bool enabled)
    {
        if (_ctrlCommand is StrategyCatalogReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
        if (_ctrlCommand is ParameterSets.ParameterSetsReferenceView parameters) { RefreshParameterButtons(parameters); return; }
        btnChange.Text = !enabled ? "Save" : "Change";
        btnAdd.Enabled = enabled;
        btnRemove.Enabled = enabled;
        btnClose.Text = !enabled ? "Cancel" : "Close";
        ddlReferenceDataSelector.Enabled = enabled;
    }

    void ResetButtons(bool enabled)
    {
        if (_ctrlCommand is StrategyCatalogReferenceView catalog) { RefreshFamilyButtons(catalog); return; }
        if (_ctrlCommand is ParameterSets.ParameterSetsReferenceView parameters) { RefreshParameterButtons(parameters); return; }
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

    void BindReferenceDataDefinitionTypes(bool selectDefault = true)
    {
        var selectedDescription = ddlReferenceDataSelector.SelectedItem?.ToString();
        _restoringReferenceSelection = true;
        ddlReferenceDataSelector.Items.Clear();
        if (_viewModel is null)
        {
            _restoringReferenceSelection = false;
            return;
        }

        foreach (var definitionType in _viewModel.ReferenceDataDefinitionTypes)
            ddlReferenceDataSelector.Items.Add(definitionType.Description);
        ddlReferenceDataSelector.Items.Add(TradeStrategyFamiliesLabel);
        ddlReferenceDataSelector.Items.Add("parameter sets");
        ddlReferenceDataSelector.AccessibleDescription = string.Join(", ",
            ddlReferenceDataSelector.Items.Cast<object>().Select(item => item.ToString()));

        if (selectedDescription is not null)
            ddlReferenceDataSelector.SelectedIndex = ddlReferenceDataSelector.FindStringExact(selectedDescription);
        _restoringReferenceSelection = false;

        if (ddlReferenceDataSelector.SelectedIndex < 0 && selectDefault && ddlReferenceDataSelector.Items.Count > 0)
            ddlReferenceDataSelector.SelectedIndex = 0;
        ddlReferenceDataSelector.Enabled = true;
        UpdateSelectorAccessibility();
    }

    void UpdateSelectorAccessibility()
        => ddlReferenceDataSelector.AccessibleName =
            $"Reference data selector; selected={ddlReferenceDataSelector.SelectedItem}; "
            + $"catalog: {ddlReferenceDataSelector.AccessibleDescription}";

    void RefreshFamilyButtons(StrategyCatalogReferenceView catalog)
    {
        btnAdd.Text = catalog.IsEditing && !catalog.IsChanging ? "Save" : "&Add";
        btnAdd.Enabled = catalog.IsEditing ? !catalog.IsChanging && catalog.CanSave : catalog.CanAdd;
        btnChange.Text = catalog.IsChanging ? "Save" : "C&hange";
        btnChange.Enabled = catalog.IsChanging ? catalog.CanSave : catalog.CanChangeRemove;
        btnRemove.Enabled = catalog.CanRemove;
        btnImport.Enabled = false;
        btnClose.Text = catalog.IsEditing ? "Cancel" : "Close";
        btnClose.Enabled = !catalog.IsSaving;
        ddlReferenceDataSelector.Enabled = !catalog.IsEditing && !catalog.IsSaving;
    }

    void RefreshParameterButtons(ParameterSets.ParameterSetsReferenceView parameters)
    {
        btnAdd.Text = parameters.IsAdding ? "Save" : "&Add";
        btnAdd.Enabled = parameters.IsAdding ? parameters.CanSave : !parameters.IsChanging && parameters.CanAdd;
        btnChange.Text = parameters.IsChanging ? "Save" : "C&hange";
        btnChange.Enabled = parameters.IsChanging ? parameters.CanSave : !parameters.IsAdding && parameters.CanChange;
        btnRemove.Enabled = parameters.CanRemove;
        btnImport.Enabled = false;
        btnClose.Text = parameters.IsEditing ? "Cancel" : "Close";
        btnClose.Enabled = !parameters.IsBusy;
        ddlReferenceDataSelector.Enabled = !parameters.IsEditing && !parameters.IsBusy;
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
