using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

public partial class MarketDataForm 
    : Form, IForm<MarketDataForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly IStatusConsoleEventProducer _statusConsoleLog;
    readonly Dictionary<string, Func<IAppRoot, Control>> _controlMap;
    MarketDataViewModel? _viewModel;
    IControlCommand? _ctrlCommand;

    public MarketDataForm(IAppRoot appRoot, IStatusConsoleEventProducer statusConsoleLog)
    {
        _appRoot = appRoot;
        _statusConsoleLog = statusConsoleLog;
        _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
        {
            { "FuturesOptionContract", ar => new FuturesOptionContractEditorControl( new FuturesOptionContractEditorViewModel(ar), _viewModel!)},
            { "FuturesContract", ar => new FuturesContractEditorControl( new FuturesContractEditorViewModel(ar) , () => ddlMarketDataSelector_SelectedIndexChanged(this,  EventArgs.Empty) )},
            { "YieldCurveRates", ar => new YieldCurveRateEditorControl( new YieldCurveRateEditorViewModel(ar) )}
        };
        InitializeComponent();
    }

    public void LoadViewModel(MarketDataViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.OnDisableAllButtons = () => this.Post(() =>
        {
            DisableAllButtons();
        });
        _viewModel.OnEnableMarketSelector = (enabled) => this.Post(() =>
        {
            ddlMarketDataSelector.Enabled = enabled;
        });
    }

    private void MarketDataForm_Load(object sender, EventArgs e)
    {
        _viewModel?.LoadMarketDefinitionTypes(mktDataDefTypes =>
          this.Post(() => {
              ddlMarketDataSelector.Items.Clear();
              if (mktDataDefTypes != null && mktDataDefTypes.Length > 0)
              {
                  foreach (var mktDataDefType in mktDataDefTypes)
                      ddlMarketDataSelector.Items.Add(mktDataDefType.Description);
                  ddlMarketDataSelector.SelectedIndex = 0;
              }
          }));
    }

    private void MarketDataForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        ResetButtons(true);
    }

    private void ddlMarketDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        ResetButtons(true);
        _ctrlCommand?.Unload();
        pnlMarketData.Controls.Clear();
        var mktDataDefType = _viewModel?.GetMarketDefinitionType(ddlMarketDataSelector.SelectedIndex);
        if (mktDataDefType != null && _controlMap.ContainsKey(mktDataDefType.ShortCode))
        {
            var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
            control.Visible = false;
            pnlMarketData.Controls.Add(control);
            _ctrlCommand = (control as IControlCommand)!;
            _ctrlCommand.Load(_appRoot, enabled => this.Post(() => {
                btnChange.Enabled = _ctrlCommand.CanChangeRemove;
                btnRemove.Enabled = _ctrlCommand.CanChangeRemove;
                btnImport.Enabled = _ctrlCommand.CanImport;
                SetButtonEnabledState();
            }));
            control.Visible = true;
        }
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
        SetButtonEnabledState();
        ddlMarketDataSelector.Enabled = enabled;
    }

    void RefreshChangeButton(bool enabled)
    {
        btnChange.Text = !enabled ? "Save" : "Change";
        btnClose.Text = !enabled ? "Cancel" : "Close";
        btnAdd.Enabled = enabled;
        btnRemove.Enabled = enabled;
        SetButtonEnabledState();
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
        SetButtonEnabledState();
        ddlMarketDataSelector.Enabled = enabled;
    }

    void DisableAllButtons()
    {
         btnAdd.Enabled = false;
        btnChange.Enabled = false;
        btnRemove.Enabled = false;
        btnImport.Enabled = false;
        btnClose.Enabled = false;
        SetButtonEnabledState();    
    }

    void SetButtonEnabledState()
    {         
        btnChange.ForeColor = btnChange.Enabled ? Color.Black : Color.White;
        btnRemove.ForeColor = btnRemove.Enabled ? Color.Black : Color.White;
        btnImport.ForeColor = btnImport.Enabled ? Color.Black : Color.White;       
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
