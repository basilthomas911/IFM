using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.ViewModels.MarketData;

namespace TomasAI.IFM.Views.MarketData
{
    public partial class MarketDataForm : Form, IForm<MarketDataForm>, IFormControl
    {
        private readonly IAppRoot _appRoot;
        private readonly IStatusConsoleEventProducer _statusConsoleLog;
        private MarketDataViewModel _viewModel;
        private IControlCommand _ctrlCommand;
        private Dictionary<string, Func<IAppRoot,Control>> _controlMap;

        public MarketDataForm(IAppRoot appRoot, IStatusConsoleEventProducer statusConsoleLog)
        {
            _appRoot = appRoot;
            _statusConsoleLog = statusConsoleLog;
            _controlMap = new Dictionary<string, Func<IAppRoot, Control>>
            {
                { "FuturesOptionContract", ar => new FuturesOptionContractEditorControl( new FuturesOptionContractEditorViewModel(ar) )},
                { "FuturesContract", ar => new FuturesContractEditorControl( new FuturesContractEditorViewModel(ar) , () => ddlMarketDataSelector_SelectedIndexChanged(this,  EventArgs.Empty) )},
                { "YieldCurveRates", ar => new YieldCurveRateEditorControl( new YieldCurveRateEditorViewModel(ar) )}
            };
            _ctrlCommand = null;
            InitializeComponent();
        }

        public void LoadViewModel(MarketDataViewModel viewModel)
        {
            _viewModel = viewModel;
          
        }

        private void MarketDataForm_Load(object sender, EventArgs e)
        {
            _viewModel.LoadMarketDefinitionTypes(mktDataDefTypes =>
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
            ResetButtons(false);
        }

        private void ddlMarketDataSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetButtons(false);
            if (_ctrlCommand != null)
                _ctrlCommand.Unload();
            pnlMarketData.Controls.Clear();
            var mktDataDefType = _viewModel.GetMarketDefinitionType(ddlMarketDataSelector.SelectedIndex);
            if (_controlMap.ContainsKey(mktDataDefType.ShortCode))
            {
                var control = _controlMap[mktDataDefType.ShortCode](_appRoot);
                control.Visible = false;
                pnlMarketData.Controls.Add(control);
                _ctrlCommand = control as IControlCommand;
                _ctrlCommand.Load(_appRoot, enabled => {
                    btnChange.Enabled = _ctrlCommand.CanChangeRemove;
                    btnRemove.Enabled = _ctrlCommand.CanChangeRemove;
                    btnImport.Enabled = _ctrlCommand.CanImport;
                });
                control.Visible = true;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e) => _ctrlCommand?.Add(enabled => RefreshAddButton(enabled));

        private void btnChange_Click(object sender, EventArgs e ) => _ctrlCommand?.Change(enabled => RefreshChangeButton(enabled));

        private void btnRemove_Click(object sender, EventArgs e) => _ctrlCommand.Remove();

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (_ctrlCommand?.Close(enabled => ResetButtons(enabled)) ?? false)
                this.Close();
        }

        private void btnImport_Click(object sender, EventArgs e) => _ctrlCommand?.Import();

        private void RefreshAddButton(bool enabled)
        {
            btnAdd.Text = !enabled ? "Save" : "Add";
            btnChange.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = !enabled ? "Cancel" : "Close";
        }

        private void RefreshChangeButton(bool enabled)
        {
            btnChange.Text = !enabled ? "Save" : "Change";
            btnAdd.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = !enabled ? "Cancel" : "Close";
        }

        private void ResetButtons(bool enabled)
        {
            btnAdd.Text = @"&Add";
            btnAdd.Enabled = true;
            btnChange.Text = @"C&hange";
            btnChange.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClose.Text = "Close";
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
}
