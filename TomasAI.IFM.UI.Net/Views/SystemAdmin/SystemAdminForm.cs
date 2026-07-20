using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.ViewModels.SystemAdmin;

namespace TomasAI.IFM.Views.SystemAdmin;

public partial class SystemAdminForm : Form, IForm<SystemAdminForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly IStatusConsoleEventProducer _statusConsoleLog;
    SystemAdminViewModel _viewModel = null!;
    Dictionary<string, Func<Control>> _controlMap;

    public SystemAdminForm(IAppRoot appRoot, IStatusConsoleEventProducer statusConsoleLog)
    {
        _appRoot = appRoot;
        _statusConsoleLog = statusConsoleLog;
        InitializeComponent();
        _controlMap = new Dictionary<string, Func<Control>>
        {
            { "BackupDatabases", () => new BackupDatabasesView(new BackupDatabasesViewModel(appRoot, statusConsoleLog)) },
        };
        
    }

    public void LoadViewModel(SystemAdminViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private void SystemAdminForm_Load(object sender, EventArgs e)
    {
        _viewModel.LoadSystemAdminFunctionTypes(sysAdminFuncTypes =>
            this.Post(() => {
                ddlFunctionSelector.Items.Clear();
                if (sysAdminFuncTypes != null && sysAdminFuncTypes.Length > 0)
                {
                    foreach (var sysAdminFuncType in sysAdminFuncTypes)
                        ddlFunctionSelector.Items.Add(sysAdminFuncType.Description);
                    ddlFunctionSelector.SelectedIndex = 0;
                }
            }));
    }

    private void ddlMarketDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        pnlSystemAdmin.Controls.Clear();
        var sysAdminFuncType = _viewModel.GetSystemAdminFunctionType(ddlFunctionSelector.SelectedIndex);
        if (sysAdminFuncType != null && _controlMap.ContainsKey(sysAdminFuncType.ShortCode))
        {
            foreach (IFormControl o in pnlSystemAdmin.Controls)
                o?.Close();
            var control = _controlMap[sysAdminFuncType.ShortCode]();
            ((IFormControl)control).Open(); 
            pnlSystemAdmin.Controls.Add(control);
        }
    }

    

    private void SystemAdminForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        foreach(IFormControl control in pnlSystemAdmin.Controls)
            control?.Close();
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
