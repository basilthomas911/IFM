using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.Views.Reference;

public partial class ReferenceForm : Form, IForm<ReferenceForm>, IFormControl
{
    readonly IAppRoot _appRoot;
    readonly Dictionary<string, Func<IAppRoot, Control>> _controlMap;
    ReferenceViewModel? _viewModel;
    IControlCommand? _ctrlCommand;

    public ReferenceForm(IAppRoot appRoot)
    {
        _appRoot = appRoot;
        _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
        {
            { "EconomicCalendar", ar => new EconomicCalendarEditorView( new EconomicCalendarEditorViewModel(ar) )},
            { "LookupTypes", ar => new LookupTypeEditorView( new LookupTypeEditorViewModel(ar) )}
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
        _viewModel = viewModel;
        _viewModel.OnDisableAllButtons = () => this.Post(() =>
        {
            DisableAllButtons();
        });
        _viewModel.OnEnableMarketSelector = (enabled) => this.Post(() =>
        {
            ddlReferenceDataSelector.Enabled = enabled;
        });
    }

    /// <summary>
    /// load reference view
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
     void ReferenceForm_Load(object sender, EventArgs e)
    {
        _viewModel?.LoadReferenceDataDefinitionTypes(mktDataDefTypes =>
          this.Post(() => {
              ddlReferenceDataSelector.Items.Clear();
              if (mktDataDefTypes != null && mktDataDefTypes.Length > 0)
              {
                  foreach (var mktDataDefType in mktDataDefTypes)
                      ddlReferenceDataSelector.Items.Add(mktDataDefType.Description);
                  ddlReferenceDataSelector.SelectedIndex = 0;
              }
          }));
    }

     void ReferenceForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _ctrlCommand?.Unload();
        ResetButtons(true);
    }

     void ddlReferenceDataSelector_SelectedIndexChanged(object sender, EventArgs e)
    {
        _ctrlCommand?.Unload();
        pnlMarketData.Controls.Clear();
        var mktDataDefType = _viewModel?.GetMarketDefinitionType(ddlReferenceDataSelector.SelectedIndex);
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

    public void Open()
    {
        throw new NotImplementedException();
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }
}
