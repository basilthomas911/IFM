using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.SystemAdmin;

public partial class SystemAdminForm : DarkTradingForm, IForm<SystemAdminForm>, IFormControl
{
    SystemAdminViewModel _viewModel = null!;
    Dictionary<string, Func<Control>> _controlMap;
    IReadOnlyList<LookupTypeUiModel> _visibleFunctionTypes = [];
    bool _closeComplete;

    public SystemAdminForm(IDatabaseBackupService databaseBackupService, IScheduledTaskService? scheduledTaskService = null)
    {
        InitializeComponent();
        _controlMap = new Dictionary<string, Func<Control>>
        {
            { "BackupDatabases", () => new BackupDatabasesView(
                new DatabaseBackupViewModel(databaseBackupService)) },
        };
        if (scheduledTaskService is not null)
            _controlMap.Add("ScheduledTasks", () => new ScheduledTasksView(new ScheduledTasksViewModel(scheduledTaskService)));

    }

    /// <summary>Keeps the resizable administration dialog within its monitor after DPI scaling.</summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var workingArea = Screen.FromControl(this).WorkingArea;
        var width = Math.Min(Width, workingArea.Width);
        var height = Math.Min(Height, workingArea.Height);
        Bounds = new Rectangle(Math.Clamp(Left, workingArea.Left, workingArea.Right - width),
            Math.Clamp(Top, workingArea.Top, workingArea.Bottom - height), width, height);
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
        foreach (IFormControl control in pnlSystemAdmin.Controls)
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
