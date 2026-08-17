using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;

public partial class SystemAdminForm : Form, IForm<SystemAdminForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly IStatusConsoleEventProducer _statusConsoleLog;
    SystemAdminViewModel _viewModel = null!;
    Dictionary<string, Func<Control>> _controlMap;
    IReadOnlyList<LookupTypeReadModel> _visibleFunctionTypes = [];
    bool _closeComplete;

    public SystemAdminForm(IAppRoot appRoot, IStatusConsoleEventProducer statusConsoleLog)
    {
        _appRoot = appRoot;
        _statusConsoleLog = statusConsoleLog;
        InitializeComponent();
        _controlMap = new Dictionary<string, Func<Control>>
        {
            { "BackupDatabases", () => new BackupDatabasesView(new DatabaseBackupViewModel(appRoot)) },
        };
        
    }

    public void LoadViewModel(SystemAdminViewModel viewModel)
    {
        if (_viewModel is not null)
            _viewModel.LoadFunctionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;

        _viewModel = viewModel;
        _viewModel.LoadFunctionTypesOperation.PropertyChanged += LoadOperation_PropertyChanged;
    }

    private async void SystemAdminForm_Load(object sender, EventArgs e)
    {
        try
        {
            await _viewModel.LoadFunctionTypesOperation.ExecuteAsync();
            BindFunctionTypes();
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "System Administration");
        }
    }

    private async void ddlMarketDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateSelectorAccessibility();
        foreach (IFormControl control in pnlSystemAdmin.Controls)
            await CloseControlAsync(control);
        pnlSystemAdmin.Controls.Clear();
        var sysAdminFuncType = ddlFunctionSelector.SelectedIndex >= 0
                               && ddlFunctionSelector.SelectedIndex < _visibleFunctionTypes.Count
            ? _visibleFunctionTypes[ddlFunctionSelector.SelectedIndex]
            : null;
        if (sysAdminFuncType != null && _controlMap.ContainsKey(sysAdminFuncType.ShortCode))
        {
            var control = _controlMap[sysAdminFuncType.ShortCode]();
            ((IFormControl)control).Open(); 
            pnlSystemAdmin.Controls.Add(control);
        }
    }

    

    private async void SystemAdminForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_closeComplete)
            return;
        e.Cancel = true;
        foreach(IFormControl control in pnlSystemAdmin.Controls)
            await CloseControlAsync(control);
        _viewModel.LoadFunctionTypesOperation.PropertyChanged -= LoadOperation_PropertyChanged;
        _closeComplete = true;
        Close();
    }

    void LoadOperation_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncOperation.IsRunning))
            this.Post(() => ddlFunctionSelector.Enabled = !_viewModel.LoadFunctionTypesOperation.IsRunning);
    }

    void BindFunctionTypes()
    {
        ddlFunctionSelector.Items.Clear();
        // Legacy/deferred functions may remain in reference data. Only advertise
        // destinations that this client can actually render.
        _visibleFunctionTypes = _viewModel.FunctionTypes
            .Where(functionType => _controlMap.ContainsKey(functionType.ShortCode))
            .ToArray();
        foreach (var functionType in _visibleFunctionTypes)
            ddlFunctionSelector.Items.Add(functionType.Description);
        ddlFunctionSelector.AccessibleDescription = string.Join(", ",
            _visibleFunctionTypes.Select(functionType => functionType.Description));

        if (ddlFunctionSelector.Items.Count > 0)
            ddlFunctionSelector.SelectedIndex = 0;
        UpdateSelectorAccessibility();
    }

    void UpdateSelectorAccessibility()
        => ddlFunctionSelector.AccessibleName =
            $"System administration selector; selected={ddlFunctionSelector.SelectedItem}; "
            + $"catalog: {ddlFunctionSelector.AccessibleDescription}";

    static ValueTask CloseControlAsync(IFormControl control)
        => control is IAsyncFormControl asyncControl
            ? asyncControl.CloseAsync()
            : CloseSynchronously(control);

    static ValueTask CloseSynchronously(IFormControl control)
    {
        control.Close();
        return ValueTask.CompletedTask;
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
